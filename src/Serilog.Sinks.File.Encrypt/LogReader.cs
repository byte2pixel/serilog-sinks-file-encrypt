using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Reads an encrypted log stream, decrypting messages on-the-fly using the provided decryption keys and options.
/// This class is not thread-safe and is designed to be used for a single decryption operation on a given input stream.
/// It maintains internal state to handle the streaming nature of the log file, allowing it to recover from errors
/// and continue processing subsequent messages or sessions as needed.
/// </summary>
public sealed class LogReader : IDisposable
{
    private enum ReaderState
    {
        ReadingHeader,
        ReadingMessages,
        Completed,
    }

    private ReaderState _state = ReaderState.ReadingHeader;
    private readonly Stream _input;
    private readonly DecryptionOptions _options;
    private DecryptionContext _context = DecryptionContext.Empty;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, RSA> _rsaKeyCache = new();
    private int _decryptedSessions;
    private int _decryptedMessages;
    private int _failedHeaders;
    private int _failedMessages;
    private int _resyncAttempts;
    private long _nextSyncPosition;

    /// <summary>
    /// Initializes a new instance of the EncryptedLogReader class with the specified input stream and decryption options.
    /// </summary>
    /// <param name="input">The input stream</param>
    /// <param name="options">The decryption options</param>
    /// <param name="logger">Optional logger for auditing decryption operations and errors. If not provided, no audit logging will occur.</param>
    /// <exception cref="ArgumentException"></exception>
    /// <remarks>
    /// This class is not thread-safe and should be used for a single decryption operation on a given input stream.
    /// </remarks>
    public LogReader(Stream input, DecryptionOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        if (options.DecryptionKeys is null || options.DecryptionKeys.Count == 0)
        {
            throw new InvalidOperationException("At least one decryption key must be provided.");
        }

        _input = input;
        _options = options;
        _logger = logger;

        if (!Enum.IsDefined(options.ErrorHandlingMode))
        {
            throw new InvalidOperationException(
                $"Invalid error handling mode: {options.ErrorHandlingMode}"
            );
        }

        foreach (KeyValuePair<string, string> key in options.DecryptionKeys)
        {
            try
            {
                var rsa = RSA.Create();
                rsa.FromString(key.Value);
                _rsaKeyCache.Add(key.Key, rsa);
            }
            catch (Exception ex)
                when (ex is CryptographicException or ArgumentNullException or ArgumentException)
            {
                throw new InvalidOperationException(
                    $"Invalid RSA key for key ID '{key.Key}': {ex.Message}",
                    ex
                );
            }
        }
        WriteToAuditLog("LogReader initialized, ready to process input stream.");
    }

    /// <summary>
    /// Decrypts the log stream and writes the decrypted content to the provided output stream.
    /// This method processes the input stream in a streaming manner, allowing for efficient decryption of large log
    /// files without loading the entire content into memory.
    /// </summary>
    /// <param name="output">The output stream.</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <exception cref="CryptographicException">Thrown if decryption fails and the error handling mode is set to ThrowException, or if no messages were decrypted from the input stream.</exception>
    public async Task<DecryptionResult> DecryptToStreamAsync(
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        while (_state != ReaderState.Completed)
        {
            await ProcessStreamAsync(output, cancellationToken);
        }

        WriteToAuditLog(
            $"Decryption completed. Sessions decrypted: {_decryptedSessions}, Messages decrypted: {_decryptedMessages}"
        );

        if (_options.ErrorHandlingMode == ErrorHandlingMode.Skip)
        {
            return new DecryptionResult
            {
                DecryptedSessions = _decryptedSessions,
                DecryptedMessages = _decryptedMessages,
                FailedMessages = _failedMessages,
                FailedHeaders = _failedHeaders,
                ResyncAttempts = _resyncAttempts,
            };
        }

        if (_decryptedSessions == 0)
        {
            throw new CryptographicException("No valid sessions found in the file.");
        }

        if (_decryptedMessages == 0)
        {
            throw new CryptographicException("No messages were decrypted from the input stream.");
        }

        if (_failedMessages > 0 || _failedHeaders > 0)
        {
            throw new CryptographicException(
                $"Decryption completed with errors. Failed headers: {_failedHeaders}, Failed messages: {_failedMessages}."
            );
        }

        return new DecryptionResult
        {
            DecryptedSessions = _decryptedSessions,
            DecryptedMessages = _decryptedMessages,
            FailedMessages = _failedMessages,
            FailedHeaders = _failedHeaders,
            ResyncAttempts = _resyncAttempts,
        };
    }

    private async Task ProcessStreamAsync(Stream output, CancellationToken cancellationToken)
    {
        switch (_state)
        {
            case ReaderState.ReadingHeader:
                // Process header and prepare for entries
                _input.Position = _nextSyncPosition;
                await ProcessHeaderAsync(cancellationToken);
                break;
            case ReaderState.ReadingMessages:
                // Read and decrypt entries, write to output
                // If end of stream is reached, set state to Completed
                if (IsEndOfStream())
                {
                    _state = ReaderState.Completed;
                    break;
                }
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

            // MagicByteDetection is a special negative value that indicates we've likely encountered
            // a new session header marker instead of a valid message length.
            if (messageLength == EncryptionConstants.MagicByteDetection)
            {
                WriteToAuditLog("Session header marker likely encountered while reading messages.");
                _context = DecryptionContext.Empty;
                _state = ReaderState.ReadingHeader;
                _nextSyncPosition = _input.Position - sizeof(int); // Rewind to start of potential header
                return;
            }

            // Read the encrypted message (ciphertext + tag)
            byte[] encryptedMessage = ArrayPool<byte>.Shared.Rent(messageLength);
            try
            {
                await ReadAndDecryptMessage(
                    output,
                    encryptedMessage,
                    messageLength,
                    cancellationToken
                );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(encryptedMessage);
            }
        }
        catch (Exception ex)
            when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException
                && ex is not OperationCanceledException
                && ex
                    is CryptographicException
                        or FormatException
                        or InvalidDataException
                        or EndOfStreamException
            )
        {
            // Handle message processing errors, try to recover if possible
            WriteToAuditLog($"Message processing error: {ex.Message}");
            _resyncAttempts++;
            _failedMessages++;
            _context = DecryptionContext.Empty;
            _state = ReaderState.ReadingHeader;
        }
    }

    private async Task ReadAndDecryptMessage(
        Stream output,
        byte[] encryptedMessage,
        int messageLength,
        CancellationToken cancellationToken
    )
    {
        await _input.ReadExactlyAsync(encryptedMessage, 0, messageLength, cancellationToken);

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
            await output.WriteAsync(plaintext.AsMemory(0, ciphertextLength), cancellationToken);
            await output.FlushAsync(cancellationToken);
            _decryptedMessages++;
            _nextSyncPosition = _input.Position;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(plaintext);
        }
    }

    private async Task ProcessHeaderAsync(CancellationToken cancellationToken)
    {
        while (_state == ReaderState.ReadingHeader)
        {
            if (!IsRoomForMagicBytes())
            {
                _state = ReaderState.Completed;
                return;
            }
            try
            {
                await ReadMarkerAndHeaderAsync(cancellationToken);
                _state = ReaderState.ReadingMessages;
            }
            catch (EndOfStreamException ex)
            {
                WriteToAuditLog($"End of stream reached while reading header: {ex.Message}");
                _state = ReaderState.Completed;
            }
            catch (InvalidDataException ex)
                when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException)
            {
                WriteToAuditLog($"Invalid data encountered while reading header: {ex.Message}");
                HandleHeaderError();
            }
            catch (CryptographicException)
                when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException)
            {
                HandleHeaderError();
            }
        }
    }

    private void HandleHeaderError()
    {
        _state = ReaderState.ReadingHeader;

        WriteToAuditLog(
            $"Header processing error at position {_input.Position}, attempting to resync using Boyer-Moore search."
        );

        // Try Boyer-Moore search from current position
        int foundPos = BoyerMooreSearch(EncryptionConstants.MagicBytes, _nextSyncPosition);

        if (foundPos >= 0)
        {
            _nextSyncPosition = foundPos;
            _resyncAttempts++;
            _input.Position = _nextSyncPosition;
        }
        else
        {
            // No header found - we're done
            _state = ReaderState.Completed;
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
            _nextSyncPosition++;
            throw new InvalidDataException(
                $"Invalid header marker at position {_input.Position - EncryptionConstants.MagicBytes.Length}"
            );
        }
    }

    private async Task ProcessSessionHeaderAsync(CancellationToken cancellationToken)
    {
        WriteToAuditLog("Possible header marker found staring processing after header.");
        byte[] version = new byte[1];
        await _input.ReadExactlyAsync(version, cancellationToken);
        try
        {
            ISessionReader sessionReader = SessionReaderFactory.GetSessionReader(version[0]);
            _context = await sessionReader.ReadSessionAsync(
                _input,
                _rsaKeyCache,
                cancellationToken
            );
            _decryptedSessions++;
            _nextSyncPosition = _input.Position;
        }
        catch (Exception ex)
            when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException
                && (
                    ex
                    is CryptographicException
                        or InvalidDataException
                        or NotSupportedException
                        or InvalidOperationException
                )
            )
        {
            _failedHeaders++;
            _nextSyncPosition++;
            throw new CryptographicException(
                $"Failed to process session at position {_input.Position}: {ex.Message}",
                ex
            );
        }
    }

    private static bool IsHeaderMarker(Memory<byte> markerBuffer) =>
        markerBuffer.Span.SequenceEqual(EncryptionConstants.MagicBytes);

    private bool IsEndOfStream() => _input.Position >= _input.Length;

    private bool IsRoomForMagicBytes() =>
        _input.Length - _input.Position >= EncryptionConstants.MagicBytes.Length;

    private void WriteToAuditLog(string message)
    {
        if (_logger is null)
        {
            return;
        }
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [Position: {_input.Position}] {message}";
        _logger.Information(logEntry);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeRsaKeys();
    }

    private void DisposeRsaKeys()
    {
        foreach (RSA value in _rsaKeyCache.Values)
        {
            value.Dispose();
        }
    }

    private int BoyerMooreSearch(byte[] pattern, long startPosition)
    {
        // Consider making this more efficient by reading directly from the stream in chunks and handling buffer overlaps,
        // rather than reading byte-by-byte, make it asynchronous, and consider edge cases like
        // the pattern being split across buffer boundaries
        int patternLength = pattern.Length;
        int[] badCharShift = new int[256];

        // Preprocessing
        for (int i = 0; i < 256; i++)
        {
            badCharShift[i] = patternLength;
        }

        for (int i = 0; i < patternLength - 1; i++)
        {
            badCharShift[pattern[i]] = patternLength - 1 - i;
        }

        // Search
        _input.Position = startPosition;
        byte[] buffer = new byte[8192];
        int bufferPos = 0;
        int bytesRead = _input.Read(buffer, 0, buffer.Length);

        while (bytesRead > 0)
        {
            int i = bufferPos + patternLength - 1;
            while (i < bytesRead)
            {
                int j = patternLength - 1;
                while (j >= 0 && buffer[i] == pattern[j])
                {
                    i--;
                    j--;
                }

                if (j < 0)
                {
                    return (int)(_input.Position - bytesRead + i + 1);
                }

                i += Math.Max(badCharShift[buffer[i]], patternLength - j);
            }

            // Handle buffer overlap
            bufferPos = bytesRead - patternLength + 1;
            Array.Copy(buffer, bufferPos, buffer, 0, patternLength - 1);
            int newBytesRead = _input.Read(
                buffer,
                patternLength - 1,
                buffer.Length - patternLength + 1
            );

            // Check for EOF
            if (newBytesRead == 0)
            {
                break;
            }

            bytesRead = newBytesRead + patternLength - 1;
        }

        return -1; // Not found
    }
}
