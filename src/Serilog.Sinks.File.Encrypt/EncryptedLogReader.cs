using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

internal sealed class EncryptedLogReader : IAsyncDisposable, IDisposable
{
    private readonly Stream _input;
    private readonly DecryptionOptions _options;
    private StreamWriter? _auditLogWriter;
    private bool _disposed;
    private DecryptionContext _context = DecryptionContext.Empty;
    private readonly Dictionary<string, RSA> _rsaKeyCache = new();
    private long _decryptedSessions = 0;
    private long _decryptedMessages = 0;

    public EncryptedLogReader(Stream input, DecryptionOptions options)
    {
        _input = input;
        _options = options;
        foreach (KeyValuePair<string, string> key in options.DecryptionKeys)
        {
            _rsaKeyCache.Add(key.Key, RSA.Create());
            _rsaKeyCache[key.Key].FromString(key.Value);
        }
        _disposed = false;
    }

    public async Task DecryptToStreamAsync(
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        while (!IsEndOfStream() && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextSectionAsync(output, cancellationToken);
            }
            catch (Exception ex) when (_options.ContinueOnError)
            {
                await HandleErrorChunkAsync(ex, _input.Position, output, cancellationToken);
                await TryRecoverAsync(cancellationToken);
            }
            catch (Exception ex) when (!_options.ContinueOnError)
            {
                if (_options.ErrorHandlingMode == ErrorHandlingMode.ThrowException)
                {
                    throw new CryptographicException(
                        $"Decryption failed at position {_input.Position}: {ex.Message}",
                        ex
                    );
                }
                await HandleErrorChunkAsync(ex, _input.Position, output, cancellationToken);
                throw;
            }
        }
        await output.FlushAsync(cancellationToken);
        switch (_options.ContinueOnError)
        {
            case false when _decryptedSessions == 0:
                throw new FormatException("No valid sessions found in the file.");
            case false when _decryptedMessages == 0:
                throw new CryptographicException(
                    "No valid messages decrypted from the input stream"
                );
        }
    }

    private async Task ProcessNextSectionAsync(Stream output, CancellationToken cancellationToken)
    {
        // If we have an active session, try to read a message first
        if (_context.HasKeys)
        {
            long startingPosition = _input.Position;
            try
            {
                await ProcessMessageAsync(output, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                // If message processing fails, handle the error and try to recover
                await HandleErrorChunkAsync(ex, startingPosition, output, cancellationToken);
                _input.Position = startingPosition;
                _context = DecryptionContext.Empty; // Clear session context on error
                await TryRecoverAsync(cancellationToken);
                return;
            }
        }

        // No active session - look for a session header
        (byte[]? markerBuffer, int read) = await ReadMarkerBufferAsync(cancellationToken);

        if (markerBuffer == null)
        {
            return;
        }

        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessSessionHeaderAsync(cancellationToken);
            return;
        }

        // Not a header marker - reset position and try to recover
        _input.Position -= read;
        await TryRecoverAsync(cancellationToken);
    }

    private async Task ProcessSessionHeaderAsync(CancellationToken cancellationToken)
    {
        int version = _input.ReadByte();
        if (version == -1)
        {
            throw new EndOfStreamException(
                "Unexpected end of stream while reading session header version"
            );
        }
        try
        {
            ISessionReader sessionReader = SessionReaderFactory.GetSessionReader(version);
            _context = await sessionReader.ReadSessionAsync(
                _input,
                _rsaKeyCache,
                cancellationToken
            );
            _decryptedSessions++;
        }
        catch (Exception ex)
        {
            throw new CryptographicException(
                $"Failed to process session at position {_input.Position}: {ex.Message}",
                ex
            );
        }
    }

    private async Task ProcessMessageAsync(Stream output, CancellationToken cancellationToken)
    {
        if (!_context.HasKeys)
        {
            throw new InvalidOperationException("No active session for message decryption");
        }

        // Read 4-byte length prefix (big-endian)
        byte[] lengthBuffer = new byte[sizeof(int)];
        int bytesRead = await _input.ReadAsync(lengthBuffer, cancellationToken);
        if (bytesRead < sizeof(int))
        {
            return; // EOF
        }

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
            bytesRead = await _input.ReadAsync(
                encryptedMessage.AsMemory(0, messageLength),
                cancellationToken
            );
            if (bytesRead < messageLength)
            {
                throw new EndOfStreamException(
                    $"Unexpected end of stream. Expected {messageLength} bytes, got {bytesRead}"
                );
            }

            // Decrypt the message
            int ciphertextLength = messageLength - EncryptionConstants.TagLength;
            if (ciphertextLength < 0)
            {
                throw new InvalidDataException("Message too short to contain authentication tag");
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
                    plaintext.AsMemory(0, ciphertextLength).ToArray(),
                    cancellationToken
                );
                _decryptedMessages++;
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

    private async Task<(byte[]?, int bytesRead)> ReadMarkerBufferAsync(
        CancellationToken cancellationToken
    )
    {
        byte[] markerBuffer = new byte[EncryptionConstants.MagicBytes.Length];
        int bytesRead = await _input.ReadAsync(markerBuffer, cancellationToken);
        return (
            bytesRead == EncryptionConstants.MagicBytes.Length ? markerBuffer : null,
            bytesRead
        );
    }

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(EncryptionConstants.MagicBytes);

    private bool IsEndOfStream() => _input.Position >= _input.Length;

    /// <summary>
    /// Handles an error chunk according to the configured error handling mode
    /// </summary>
    /// <exception cref="CryptographicException"></exception>
    private async Task HandleErrorChunkAsync(
        Exception exception,
        long position,
        Stream outputStream,
        CancellationToken cancellationToken
    )
    {
        switch (_options.ErrorHandlingMode)
        {
            case ErrorHandlingMode.WriteInline:
                string errorMessage =
                    $"[Decryption error at position {position}: {exception.Message}]\n";
                byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                await outputStream.WriteAsync(errorBytes, cancellationToken);
                break;

            case ErrorHandlingMode.WriteToErrorLog:
                await WriteToErrorLogAsync(exception, position, cancellationToken);
                break;

            case ErrorHandlingMode.ThrowException:
                throw new CryptographicException(
                    $"Decryption failed at position {position}: {exception.Message}"
                );
        }
    }

    /// <summary>
    /// Writes an error chunk to the error log file
    /// </summary>
    private async Task WriteToErrorLogAsync(
        Exception exception,
        long position,
        CancellationToken cancellationToken
    )
    {
        _auditLogWriter ??= await CreateErrorLogWriterAsync(cancellationToken);

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry =
            $"[{timestamp}] Decryption error at position {position}: {exception.Message}";
        await _auditLogWriter.WriteLineAsync(logEntry.AsMemory(), cancellationToken);
        await _auditLogWriter.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a StreamWriter for the error log file
    /// </summary>
    private async Task<StreamWriter> CreateErrorLogWriterAsync(CancellationToken cancellationToken)
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
            FileMode.Append,
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
        if (fileStream.Position == 0)
        {
            await writer.WriteLineAsync(
                $"Decryption Error Log - Started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC".AsMemory(),
                cancellationToken
            );
            await writer.WriteLineAsync(new string('-', 80).AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }

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

    /// <summary>
    /// Tries to recover from decryption errors by seeking to the next valid marker
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task TryRecoverAsync(CancellationToken cancellationToken)
    {
        // Try to find the next valid marker
        byte[] searchBuffer = new byte[EncryptionConstants.MagicBytes.Length];

        while (_input.Position < _input.Length - EncryptionConstants.MagicBytes.Length)
        {
            // Read potential marker
            int bytesRead = await _input.ReadAsync(searchBuffer, cancellationToken);
            if (bytesRead < EncryptionConstants.MagicBytes.Length)
            {
                break;
            }

            // Check if it's a valid marker
            if (IsHeaderMarker(searchBuffer))
            {
                // Move back to start of marker
                _input.Position -= EncryptionConstants.MagicBytes.Length;
                return;
            }

            // Move back and advance by 1 byte to continue searching
            _input.Position -= EncryptionConstants.MagicBytes.Length - 1;
        }
        _input.Position = _input.Length;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        foreach (KeyValuePair<string, RSA> keyValuePair in _rsaKeyCache)
        {
            keyValuePair.Value.Dispose();
        }

        _auditLogWriter?.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_auditLogWriter != null)
        {
            await _auditLogWriter.DisposeAsync();
        }

        foreach (KeyValuePair<string, RSA> keyValuePair in _rsaKeyCache)
        {
            keyValuePair.Value.Dispose();
        }

        await _input.DisposeAsync();

        _disposed = true;
    }
}
