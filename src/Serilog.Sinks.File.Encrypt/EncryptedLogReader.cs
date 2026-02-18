using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Reads an encrypted log stream, decrypting messages on-the-fly using the provided decryption keys and options.
/// </summary>
public sealed class EncryptedLogReader : IAsyncDisposable, IDisposable
{
    private enum ReaderState
    {
        NotInitialized,
        ReadingHeader,
        ReadingMessages,
        Completed,
    }

    private ReaderState _state = ReaderState.NotInitialized;
    private readonly Stream _input;
    private readonly DecryptionOptions _options;
    private DecryptionContext _context = DecryptionContext.Empty;
    private StreamWriter? _auditLogWriter;
    private readonly Dictionary<string, RSA> _rsaKeyCache = new();
    private long _decryptedSessions;
    private long _decryptedMessages;
    private long _nextSyncPosition;

    /// <summary>
    /// Initializes a new instance of the EncryptedLogReader class with the specified input stream and decryption options.
    /// </summary>
    /// <param name="input">The input stream</param>
    /// <param name="options">The decryption options</param>
    public EncryptedLogReader(Stream input, DecryptionOptions options)
    {
        _input = input;
        _options = options;
        foreach (KeyValuePair<string, string> key in options.DecryptionKeys)
        {
            _rsaKeyCache.Add(key.Key, RSA.Create());
            _rsaKeyCache[key.Key].FromString(key.Value);
        }
    }

    /// <summary>
    /// Decrypts the log stream and writes the decrypted content to the provided output stream.
    /// This method processes the input stream in a streaming manner, allowing for efficient decryption of large log
    /// files without loading the entire content into memory.
    /// </summary>
    /// <param name="output">The output stream.</param>
    /// <param name="cancellationToken">The cancellation token</param>
    public async Task DecryptToStreamAsync(
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        if (_state == ReaderState.NotInitialized)
        {
            if (!string.IsNullOrWhiteSpace(_options.ErrorLogPath))
            {
                _auditLogWriter = await CreateAuditLogWriterAsync(cancellationToken);
            }
            _state = ReaderState.ReadingHeader;
        }

        while (_state != ReaderState.Completed)
        {
            await ProcessStreamAsync(output, cancellationToken);
        }

        await WriteToAuditLogAsync(
            $"Decryption completed. Sessions decrypted: {_decryptedSessions}, Messages decrypted: {_decryptedMessages}",
            cancellationToken
        );
    }

    private async Task ProcessStreamAsync(Stream output, CancellationToken cancellationToken)
    {
        switch (_state)
        {
            case ReaderState.NotInitialized:
                // Read and validate header
                _state = ReaderState.ReadingHeader;
                break;
            case ReaderState.ReadingHeader:
                // Process header and prepare for entries
                _input.Position = _nextSyncPosition;
                await ProcessHeaderAsync(cancellationToken);
                break;
            case ReaderState.ReadingMessages:
                // Read and decrypt entries, write to output
                // If end of stream is reached, set state to Completed
                await ProcessMessagesAsync(output, cancellationToken);
                break;
            case ReaderState.Completed:
                break;
            default:
                // This should never happen, but just in case...
                throw new InvalidOperationException("Invalid reader state");
        }
    }

    private async Task ProcessMessagesAsync(Stream output, CancellationToken cancellationToken)
    {
        try
        {
            // Process messages from _input, decrypt using keys from _rsaKeyCache, and write to _output
            // Continue to process messages until end of stream or an error occurs which means we might have
            // hit a new session or a corrupted message, in which case we should try to recover by going back to reading the header
            if (!_context.HasKeys)
            {
                throw new InvalidOperationException("No active session for message decryption");
            }

            // Read 4-byte length prefix (big-endian)
            byte[] lengthBuffer = new byte[sizeof(int)];
            await _input.ReadExactlyAsync(lengthBuffer, cancellationToken);

            int messageLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

            // Sanity check on message length
            if (messageLength is <= 0 or > 100 * 1024 * 1024) // Max 100MB per message
            {
                throw new InvalidDataException($"Invalid message length: {messageLength}");
            }

            // Read the encrypted message (ciphertext + tag)
            byte[] encryptedMessage = ArrayPool<byte>.Shared.Rent(messageLength);
            try
            {
                await _input.ReadExactlyAsync(
                    encryptedMessage,
                    0,
                    messageLength,
                    cancellationToken
                );

                // Decrypt the message
                int ciphertextLength = messageLength - EncryptionConstants.TagLength;
                if (ciphertextLength < 0)
                {
                    throw new InvalidDataException(
                        "Message too short to contain authentication tag"
                    );
                }

                byte[] plaintext = ArrayPool<byte>.Shared.Rent(ciphertextLength);
                try
                {
                    using var aes = new AesGcm(_context.SessionKey, EncryptionConstants.TagLength);
                    aes.Decrypt(
                        _context.Nonce,
                        encryptedMessage.AsSpan(0, ciphertextLength),
                        encryptedMessage.AsSpan(ciphertextLength, EncryptionConstants.TagLength),
                        plaintext.AsSpan(0, ciphertextLength)
                    );

                    // Increment nonce for next message
                    _context.Nonce.IncreaseNonce();

                    // Write decrypted chunk
                    await output.WriteAsync(
                        plaintext.AsMemory(0, ciphertextLength),
                        cancellationToken
                    );
                    await output.FlushAsync(cancellationToken);
                    _decryptedMessages++;
                    _nextSyncPosition = _input.Position;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(plaintext);
                }
            }
            catch (Exception ex)
            {
                throw new CryptographicException(
                    $"Failed to decrypt message at position {_input.Position}: {ex.Message}",
                    ex
                );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(encryptedMessage);
            }
        }
        catch (Exception ex)
        {
            // Handle message processing errors, try to recover if possible
            await WriteToAuditLogAsync(
                $"Message processing error: {ex.Message}",
                cancellationToken
            );
            _context = DecryptionContext.Empty;
            _state = ReaderState.ReadingHeader;
        }
    }

    private async Task ProcessHeaderAsync(CancellationToken cancellationToken)
    {
        while (_state == ReaderState.ReadingHeader)
        {
            try
            {
                // Read and validate header from _input
                // If valid, prepare for reading messages
                // No active session - look for a session header
                await ReadMarkerAndHeaderAsync(cancellationToken);
                _state = ReaderState.ReadingMessages;
            }
            catch (Exception ex)
            {
                // Handle header processing errors, try to recover if possible
                await WriteToAuditLogAsync(
                    $"Header processing error: {ex.Message}",
                    cancellationToken
                );
                _state = ReaderState.ReadingHeader;
                _input.Position = _nextSyncPosition;
                _nextSyncPosition++;
                if (IsEndOfStream())
                {
                    _state = ReaderState.Completed;
                }
            }
        }
    }

    private async Task ReadMarkerAndHeaderAsync(CancellationToken cancellationToken)
    {
        var markerBuffer = new Memory<byte>(new byte[EncryptionConstants.MagicBytes.Length]);
        await _input.ReadExactlyAsync(markerBuffer, cancellationToken);
        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessSessionHeaderAsync(cancellationToken);
        }
        else
        {
            throw new InvalidDataException(
                $"Invalid header marker at position {_input.Position - EncryptionConstants.MagicBytes.Length}"
            );
        }
    }

    private async Task ProcessSessionHeaderAsync(CancellationToken cancellationToken)
    {
        await WriteToAuditLogAsync(
            $"Session header marker found at position {_input.Position - EncryptionConstants.MagicBytes.Length}",
            cancellationToken
        );
        byte[] versionBuffer = new byte[1];
        await _input.ReadExactlyAsync(versionBuffer, cancellationToken);
        try
        {
            ISessionReader sessionReader = SessionReaderFactory.GetSessionReader(versionBuffer[0]);
            _context = await sessionReader.ReadSessionAsync(
                _input,
                _rsaKeyCache,
                cancellationToken
            );
            _decryptedSessions++;
            _nextSyncPosition = _input.Position;
        }
        catch (Exception ex)
        {
            throw new CryptographicException(
                $"Failed to process session at position {_input.Position}: {ex.Message}",
                ex
            );
        }
    }

    private static bool IsHeaderMarker(Memory<byte> markerBuffer) =>
        markerBuffer.Span.SequenceEqual(EncryptionConstants.MagicBytes);

    private bool IsEndOfStream() => _input.Position >= _input.Length;

    private async Task WriteToAuditLogAsync(string message, CancellationToken cancellationToken)
    {
        if (_auditLogWriter is null)
        {
            return;
        }
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] {message}";
        await _auditLogWriter.WriteLineAsync(logEntry.AsMemory(), cancellationToken);
        await _auditLogWriter.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a StreamWriter for the error log file
    /// </summary>
    private async Task<StreamWriter> CreateAuditLogWriterAsync(CancellationToken cancellationToken)
    {
        string errorLogPath = GetErrorLogPath();

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(errorLogPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create or append to the error log file
        FileStream fileStream = new(
            errorLogPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true
        );

        StreamWriter writer = new(fileStream, Encoding.UTF8)
        {
            AutoFlush = false, // We'll flush manually for better control
        };

        // Write header if file is new/empty
        if (fileStream.Position != 0)
        {
            return writer;
        }

        await writer.WriteLineAsync(
            $"Decryption Error Log - Started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC".AsMemory(),
            cancellationToken
        );
        await writer.WriteLineAsync(new string('-', 80).AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);

        return writer;
    }

    /// <summary>
    /// Gets the error log file path, using the configured path or generating a default one
    /// </summary>
    private string GetErrorLogPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ErrorLogPath))
        {
            return _options.ErrorLogPath;
        }

        // Generate a default error log path based on timestamp
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return Path.Join(
            Path.GetTempPath(),
            $"decryption_errors_{timestamp}_{Guid.NewGuid():N}.log"
        );
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _auditLogWriter?.Flush();
        _auditLogWriter?.Dispose();
        foreach (RSA value in _rsaKeyCache.Values)
        {
            value.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (_auditLogWriter is not null)
        {
            await _auditLogWriter.FlushAsync();
            await _auditLogWriter.DisposeAsync();
        }
    }
}
