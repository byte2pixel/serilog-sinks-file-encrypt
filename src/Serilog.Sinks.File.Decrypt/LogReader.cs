using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt.Interfaces;
using Serilog.Sinks.File.Decrypt.Models;
using Serilog.Sinks.File.Encrypt;

namespace Serilog.Sinks.File.Decrypt;

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
    private int _decryptedSessions;
    private int _decryptedMessages;
    private int _failedHeaders;
    private int _failedMessages;
    private int _resyncAttempts;
    private long _nextSyncPosition;
    private readonly List<SessionResult> _sessions = [];

    /// <summary>
    /// Initializes a new instance of the EncryptedLogReader class with the specified input stream and decryption options.
    /// </summary>
    /// <param name="input">The input stream</param>
    /// <param name="options">The decryption options</param>
    /// <param name="logger">Optional logger for auditing decryption operations and errors. If not provided, no audit logging will occur.</param>
    /// <exception cref="ArgumentNullException">Thrown if input or options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if KeyProvider is null</exception>
    /// <remarks>
    /// This class is not thread-safe and should be used for a single decryption operation on a given input stream.
    /// The caller retains ownership of the input stream and <see cref="DecryptionOptions.KeyProvider"/>
    /// </remarks>
    public LogReader(Stream input, DecryptionOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        if (options.KeyProvider is null)
        {
            throw new InvalidOperationException(
                "A KeyProvider must be supplied in DecryptionOptions."
            );
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

        _logger?.Information("LogReader initialized, ready to process input stream.");
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

        _logger?.Information(
            "Decryption completed. Sessions decrypted: {DecryptedSessions}, Messages decrypted: {DecryptedMessages}",
            _decryptedSessions,
            _decryptedMessages
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
                Sessions = _sessions,
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
            Sessions = _sessions,
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
                    FinalizeSession();
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
                _logger?.Information(
                    "Session header marker likely encountered while reading messages."
                );
                FinalizeSession();
                ReplaceContext(DecryptionContext.Empty);
                _state = ReaderState.ReadingHeader;
                _nextSyncPosition = _input.Position - sizeof(int); // Rewind to start of potential header
                return;
            }

            // SealMarkerDetection introduces the v2 end-of-log seal record where a length
            // prefix would otherwise be. Only meaningful within a v2 session; in a v1 session
            // the value falls through to the length validation below and is treated as corruption.
            if (
                _context.Version == EncryptionConstants.FormatVersionV2
                && messageLength == EncryptionConstants.SealMarkerDetection
            )
            {
                await ProcessSealAsync(cancellationToken);
                return;
            }

            // Nothing but a new session header (handled above) may legitimately follow the seal.
            if (_context.SealSeen)
            {
                _context.SealStatus = SealStatus.SealInvalid;
                throw new CryptographicException(
                    "Encrypted data encountered after the end-of-log seal record."
                );
            }

            // Validate the length prefix before allocating. A corrupt or malicious value
            // (negative, zero, too small to hold the authentication tag, or larger than the
            // bytes remaining in the stream) must not crash decryption or trigger an
            // unbounded allocation. Treat it as corruption so Skip mode can resync instead
            // of throwing an unhandled ArgumentOutOfRangeException/OutOfMemoryException.
            long remainingBytes = _input.Length - _input.Position;
            if (messageLength < EncryptionConstants.TagLength || messageLength > remainingBytes)
            {
                throw new InvalidDataException(
                    $"Invalid message length {messageLength} at position {_input.Position - sizeof(int)}."
                );
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
            // Handle message processing errors, try to recover if possible. The failed session is
            // poisoned: after one authentication failure neither the nonce lockstep nor the frame
            // sequence can be trusted, so the reader only re-establishes decryption at the next
            // session header (Boyer-Moore resync). The remainder of the failed session is never
            // re-entered, which is what guarantees a frame can never be silently accepted with
            // the wrong sequence after an error.
            _logger?.Error(ex, "Message processing error at position: {Position}", _input.Position);
            _resyncAttempts++;
            _failedMessages++;
            _context.FailedMessages++;
            FinalizeSession();
            ReplaceContext(DecryptionContext.Empty);
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
            DecryptFrame(encryptedMessage, ciphertextLength, plaintext);

            // Increment nonce for next message
            _context.Nonce.IncreaseNonce();
            _context.FrameSequence++;
            _context.DecryptedMessages++;

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

    /// <summary>
    /// Decrypts a single data frame. For v2 sessions the frame's associated data
    /// (header hash + frame sequence + frame type) is reconstructed from the reader's own state,
    /// so any dropped, reordered, duplicated, or cross-session-spliced frame fails authentication.
    /// v1 frames carry no associated data.
    /// </summary>
    private void DecryptFrame(byte[] encryptedMessage, int ciphertextLength, byte[] plaintext)
    {
        using var aes = new AesGcm(_context.SessionKey, EncryptionConstants.TagLength);
        if (_context.Version == EncryptionConstants.FormatVersionV2)
        {
            Span<byte> aad = stackalloc byte[EncryptionConstants.AadLength];
            BuildAad(aad, _context.FrameSequence, EncryptionConstants.FrameTypeData);
            aes.Decrypt(
                _context.Nonce,
                encryptedMessage.AsSpan(0, ciphertextLength),
                encryptedMessage.AsSpan(ciphertextLength, EncryptionConstants.TagLength),
                plaintext.AsSpan(0, ciphertextLength),
                aad
            );
        }
        else
        {
            aes.Decrypt(
                _context.Nonce,
                encryptedMessage.AsSpan(0, ciphertextLength),
                encryptedMessage.AsSpan(ciphertextLength, EncryptionConstants.TagLength),
                plaintext.AsSpan(0, ciphertextLength)
            );
        }
    }

    /// <summary>
    /// Composes the associated data for a v2 record from the active session's header hash,
    /// the given frame sequence, and the record type.
    /// </summary>
    private void BuildAad(Span<byte> aad, ulong frameSequence, byte frameType)
    {
        _context.HeaderHash.CopyTo(aad);
        BinaryPrimitives.WriteUInt64BigEndian(
            aad.Slice(EncryptionConstants.HeaderHashLength),
            frameSequence
        );
        aad[EncryptionConstants.AadLength - 1] = frameType;
    }

    /// <summary>
    /// Processes the v2 end-of-log seal record whose 4-byte marker has just been consumed.
    /// A fully present, authenticated seal resolves the session to <see cref="SealStatus.Sealed"/>
    /// (or <see cref="SealStatus.SealCountMismatch"/> when its declared frame count differs from
    /// the frames actually decrypted — the fingerprint of tail truncation of a cleanly closed log).
    /// A partially written seal is an unclean close, not tampering: the session stays unsealed and
    /// no error is raised. A seal that fails authentication, or a second seal, is tampering.
    /// </summary>
    private async Task ProcessSealAsync(CancellationToken cancellationToken)
    {
        if (_context.SealSeen)
        {
            _context.SealStatus = SealStatus.SealInvalid;
            throw new CryptographicException("Duplicate end-of-log seal record encountered.");
        }

        long remainingBytes = _input.Length - _input.Position;

        // A marker immediately followed by a new session header means the writer crashed after
        // persisting only the 4-byte marker and the file was appended to on restart: a partially
        // written seal, not tampering. Leave the stream positioned at the header so the next
        // read finds the magic bytes and the appended session processes normally; this session
        // finalizes as unsealed.
        if (remainingBytes >= CryptographicUtils.MagicBytes.Length)
        {
            byte[] peek = new byte[CryptographicUtils.MagicBytes.Length];
            await _input.ReadExactlyAsync(peek, cancellationToken);
            _input.Position -= CryptographicUtils.MagicBytes.Length;
            if (peek.AsSpan().SequenceEqual(CryptographicUtils.MagicBytes))
            {
                _logger?.Information(
                    "Seal marker followed by a new session header at position {Position}; treating as a partially written seal, session remains unsealed.",
                    _input.Position
                );
                return;
            }
        }

        if (remainingBytes < EncryptionConstants.SealRecordRemainderLength)
        {
            // Partially written seal: the writer was interrupted mid-close. Indistinguishable
            // from a crash, so report the session as unsealed rather than tampered.
            _logger?.Information(
                "Partially written seal record at position {Position}; session remains unsealed.",
                _input.Position
            );
            _input.Position = _input.Length;
            return;
        }

        byte[] sealRecord = new byte[EncryptionConstants.SealRecordRemainderLength];
        await _input.ReadExactlyAsync(sealRecord, cancellationToken);

        ulong declaredFrameCount;
        try
        {
            declaredFrameCount = DecryptSeal(sealRecord);
        }
        catch (CryptographicException)
        {
            _context.SealSeen = true;
            _context.SealStatus = SealStatus.SealInvalid;
            throw;
        }

        _context.SealSeen = true;
        _context.DeclaredFrameCount = declaredFrameCount;
        _context.SealStatus =
            declaredFrameCount == _context.FrameSequence
                ? SealStatus.Sealed
                : SealStatus.SealCountMismatch;
        _nextSyncPosition = _input.Position;

        if (_context.SealStatus == SealStatus.SealCountMismatch)
        {
            _logger?.Error(
                "Seal record declares {Declared} frames but {Decrypted} were decrypted — the session tail was truncated.",
                declaredFrameCount,
                _context.FrameSequence
            );
        }
    }

    /// <summary>
    /// Authenticates and decrypts the seal record's payload using the session's reserved seal
    /// nonce (initial nonce counter − 1), which keeps the seal verifiable regardless of how many
    /// data frames survived. Returns the declared final frame count.
    /// </summary>
    private ulong DecryptSeal(byte[] sealRecord)
    {
        Span<byte> aad = stackalloc byte[EncryptionConstants.AadLength];
        BuildAad(aad, 0, EncryptionConstants.FrameTypeSeal);

        Span<byte> plaintext = stackalloc byte[EncryptionConstants.SealPlaintextLength];
        using var aes = new AesGcm(_context.SessionKey, EncryptionConstants.TagLength);
        aes.Decrypt(
            _context.SealNonce,
            sealRecord.AsSpan(0, EncryptionConstants.SealPlaintextLength),
            sealRecord.AsSpan(
                EncryptionConstants.SealPlaintextLength,
                EncryptionConstants.TagLength
            ),
            plaintext,
            aad
        );

        return BinaryPrimitives.ReadUInt64BigEndian(plaintext);
    }

    /// <summary>
    /// Records the outcome of the active session (if any) as a <see cref="SessionResult"/>.
    /// Called when a session ends: a new session header is found, the end of the stream is
    /// reached, or the session is abandoned after an error. In
    /// <see cref="ErrorHandlingMode.ThrowException"/> mode this is also where a positively
    /// detected truncation (seal count mismatch) and — when
    /// <see cref="DecryptionOptions.RequireSealed"/> is set — any non-sealed session fail the run.
    /// </summary>
    private void FinalizeSession()
    {
        if (!_context.HasKeys)
        {
            return;
        }

        SealStatus status = _context.Version switch
        {
            EncryptionConstants.FormatVersionV1 => SealStatus.NotApplicable,
            _ => _context.SealSeen ? _context.SealStatus : SealStatus.Unsealed,
        };

        var session = new SessionResult
        {
            Index = _sessions.Count,
            FormatVersion = _context.Version,
            KeyId = _context.KeyId,
            SealStatus = status,
            DeclaredFrameCount = _context.DeclaredFrameCount,
            DecryptedMessages = _context.DecryptedMessages,
            FailedMessages = _context.FailedMessages,
        };
        _sessions.Add(session);

        if (status is not SealStatus.Sealed and not SealStatus.NotApplicable)
        {
            _logger?.Warning(
                "Session {Index} completed with seal status {SealStatus}.",
                session.Index,
                status
            );
        }

        if (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException)
        {
            return;
        }

        if (status == SealStatus.SealCountMismatch)
        {
            throw new CryptographicException(
                $"Session {session.Index}: seal record declares {session.DeclaredFrameCount} frames "
                    + $"but {session.DecryptedMessages} were decrypted — the log tail was truncated."
            );
        }

        if (_options.RequireSealed && status != SealStatus.Sealed)
        {
            throw new CryptographicException(
                $"Session {session.Index} is not verified as sealed (status: {status}) and RequireSealed is enabled."
            );
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
                _logger?.Error(ex, "End of stream reached while reading header");
                _state = ReaderState.Completed;
            }
            catch (InvalidDataException ex)
                when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException)
            {
                _logger?.Error(ex, "Invalid data encountered while reading header");
                HandleHeaderError();
            }
            catch (CryptographicException ex)
                when (_options.ErrorHandlingMode != ErrorHandlingMode.ThrowException)
            {
                _logger?.Error(
                    ex,
                    "Cryptographic error encountered while processing header, likely due to invalid session key or corrupted header data."
                );
                HandleHeaderError();
            }
        }
    }

    private void HandleHeaderError()
    {
        _state = ReaderState.ReadingHeader;

        _logger?.Information(
            "Header processing error at position {InputPosition}, attempting to resync using Boyer-Moore search.",
            _input.Position
        );

        // Try Boyer-Moore search from current position
        int foundPos = BoyerMooreSearch();

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
        var markerBuffer = new Memory<byte>(new byte[CryptographicUtils.MagicBytes.Length]);
        await _input.ReadExactlyAsync(markerBuffer, cancellationToken);
        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessSessionHeaderAsync(cancellationToken);
        }
        else
        {
            _nextSyncPosition++;
            throw new InvalidDataException(
                $"Invalid header marker at position {_input.Position - CryptographicUtils.MagicBytes.Length}"
            );
        }
    }

    private async Task ProcessSessionHeaderAsync(CancellationToken cancellationToken)
    {
        _logger?.Information(
            "Possible header marker found at position attempting to read session header."
        );
        byte[] version = new byte[1];
        await _input.ReadExactlyAsync(version, cancellationToken);
        try
        {
            if (
                version[0]
                is not (EncryptionConstants.FormatVersionV1 or EncryptionConstants.FormatVersionV2)
            )
            {
                throw new NotSupportedException($"Unsupported encryption version: {version[0]}");
            }
            ISessionReader sessionReader = new SessionReader();
            ReplaceContext(
                await sessionReader.ReadSessionAsync(
                    _input,
                    _options.KeyProvider,
                    version[0],
                    cancellationToken
                )
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
        markerBuffer.Span.SequenceEqual(CryptographicUtils.MagicBytes);

    private bool IsEndOfStream() => _input.Position >= _input.Length;

    private bool IsRoomForMagicBytes() =>
        _input.Length - _input.Position >= CryptographicUtils.MagicBytes.Length;

    /// <summary>
    /// Replaces the active decryption context, zeroing the previous session's key material first
    /// so it does not linger in managed memory.
    /// </summary>
    private void ReplaceContext(DecryptionContext next)
    {
        _context.Clear();
        _context = next;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Wipe any remaining session key material.
        _context.Clear();
    }

    private int BoyerMooreSearch()
    {
        // Consider making this more efficient by reading directly from the stream in chunks and handling buffer overlaps,
        // rather than reading byte-by-byte, make it asynchronous, and consider edge cases like
        // the pattern being split across buffer boundaries
        int patternLength = CryptographicUtils.MagicBytes.Length;
        int[] badCharShift = new int[256];

        // Preprocessing
        for (int i = 0; i < 256; i++)
        {
            badCharShift[i] = patternLength;
        }

        for (int i = 0; i < patternLength - 1; i++)
        {
            badCharShift[CryptographicUtils.MagicBytes[i]] = patternLength - 1 - i;
        }

        // Search
        _input.Position = _nextSyncPosition;
        byte[] buffer = new byte[8192];
        int bufferPos = 0;
        int bytesRead = _input.Read(buffer, 0, buffer.Length);

        while (bytesRead > 0)
        {
            int i = bufferPos + patternLength - 1;
            while (i < bytesRead)
            {
                int j = patternLength - 1;
                while (j >= 0 && buffer[i] == CryptographicUtils.MagicBytes[j])
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
