using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Readers;

namespace Serilog.Sinks.File.Encrypt;

internal sealed class EncryptedLogReader : IAsyncDisposable, IDisposable
{
    private readonly Stream _input;
    private readonly DecryptionOptions _options;
    private readonly IFrameReader _frameReader;
    private StreamWriter? _auditLogWriter;
    private bool _disposed;
    private bool _hasFoundValidHeader;
    private DecryptionContext? _currentSession;

    public EncryptedLogReader(
        Stream input,
        DecryptionOptions options,
        IFrameReader? frameReader = null
    )
    {
        _input = input;
        _options = options;
        _frameReader = frameReader ?? new FrameReader();
        _hasFoundValidHeader = false;
        _disposed = false;
    }

    public async Task DecryptToStreamAsync(
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        var channel = Channel.CreateBounded<IDecryptionChunk>(
            new BoundedChannelOptions(_options.QueueDepth)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );

        Task producer = ProduceDecryptionChunksAsync(channel.Writer, cancellationToken);
        Task consumer = ConsumeDecryptionChunksAsync(channel.Reader, output, cancellationToken);

        await Task.WhenAll(producer, consumer);
    }

    private async Task ProduceDecryptionChunksAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
        bool completedSuccessfully = false;
        try
        {
            while (!IsEndOfStream() && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextSectionAsync(writer, cancellationToken);
                }
                catch (Exception ex) when (_options.ContinueOnError)
                {
                    await HandleErrorAsync(writer, ex, cancellationToken);
                    await TryRecoverAsync(cancellationToken);
                }
                catch (Exception ex) when (!_options.ContinueOnError)
                {
                    // For ThrowException mode, throw immediately without writing to channel
                    if (_options.ErrorHandlingMode == ErrorHandlingMode.ThrowException)
                    {
                        throw new CryptographicException(
                            $"Decryption failed at position {_input.Position}: {ex.Message}",
                            ex
                        );
                    }

                    // For other modes, write error chunk and let consumer handle it
                    await writer.WriteAsync(
                        new DecryptionErrorChunk(ex.Message, _input.Position),
                        cancellationToken
                    );
                    writer.Complete();
                    throw;
                }
            }

            // Validate that we found at least one valid encryption header
            if (!_hasFoundValidHeader)
            {
                // audit log?
            }
            completedSuccessfully = true;
        }
        finally
        {
            if (completedSuccessfully)
            {
                await writer.WriteAsync(EndOfStreamChunk.Instance, cancellationToken);
            }
            writer.Complete();
        }
    }

    private async Task HandleErrorAsync(
        ChannelWriter<IDecryptionChunk> writer,
        Exception ex,
        CancellationToken cancellationToken
    )
    {
        await writer.WriteAsync(
            new DecryptionErrorChunk(ex.Message, _input.Position),
            cancellationToken
        );
    }

    private async Task ProcessNextSectionAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
        // If we have an active session, try to read a message first
        if (_currentSession != null && _currentSession.HasKeys)
        {
            // Peek to check if we're at a new session header
            byte[] peekBuffer = new byte[EncryptionConstants.MagicBytes.Length];
            int bytesRead = await _input.ReadAsync(peekBuffer, cancellationToken);

            if (bytesRead < EncryptionConstants.MagicBytes.Length)
            {
                return; // EOF
            }

            // Check if this is a new session header
            if (IsHeaderMarker(peekBuffer))
            {
                // New session - process it
                await ProcessSessionHeaderAsync(cancellationToken);
                return;
            }

            // Not a header - rewind and process as message
            _input.Position -= bytesRead;
            await ProcessMessageAsync(writer, cancellationToken);
            return;
        }

        // No active session - look for a session header
        byte[]? markerBuffer = await ReadMarkerBufferAsync(cancellationToken);

        if (markerBuffer == null)
        {
            return;
        }

        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessSessionHeaderAsync(cancellationToken);
            return;
        }

        // Not a header and no session - try to recover
        await TryRecoverAsync(cancellationToken);
    }

    private async Task ProcessSessionHeaderAsync(CancellationToken cancellationToken)
    {
        try
        {
            _input.Position += EncryptionConstants.MagicBytes.Length; // Move past the marker
            (byte version, RSA rsa, ReadOnlyMemory<byte> header) = _frameReader.ReadHeader(
                _input,
                _options.DecryptionKeys
            );
            ISessionReader sessionReader = SessionReaderFactory.GetSessionReader(version);
            _currentSession = sessionReader.ReadSession(rsa, header);
            _hasFoundValidHeader = true;
        }
        catch (Exception ex)
        {
            throw new CryptographicException(
                $"Failed to process session at position {_input.Position}: {ex.Message}",
                ex
            );
        }
    }

    private async Task ProcessMessageAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
        if (_currentSession == null || !_currentSession.HasKeys)
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
        if (messageLength <= 0 || messageLength > 100 * 1024 * 1024) // Max 100MB per message
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
            int ciphertextLength = messageLength - _currentSession.TagLength;
            if (ciphertextLength < 0)
            {
                throw new InvalidDataException("Message too short to contain authentication tag");
            }

            byte[] plaintext = ArrayPool<byte>.Shared.Rent(ciphertextLength);
            try
            {
                using var aes = new AesGcm(_currentSession.SessionKey, _currentSession.TagLength);
                aes.Decrypt(
                    _currentSession.Nonce,
                    encryptedMessage.AsSpan(0, ciphertextLength),
                    encryptedMessage.AsSpan(ciphertextLength, _currentSession.TagLength),
                    plaintext.AsSpan(0, ciphertextLength)
                );

                // Increment nonce for next message
                _currentSession.Nonce.IncreaseNonce();

                // Write decrypted chunk
                await writer.WriteAsync(
                    new DecryptedMessageChunk(plaintext.AsMemory(0, ciphertextLength).ToArray()),
                    cancellationToken
                );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(plaintext);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encryptedMessage);
        }
    }

    private async Task<byte[]?> ReadMarkerBufferAsync(CancellationToken cancellationToken)
    {
        byte[] markerBuffer = new byte[EncryptionConstants.MagicBytes.Length];
        int bytesRead = await _input.ReadAsync(markerBuffer, cancellationToken);
        _input.Position -= bytesRead != EncryptionConstants.MagicBytes.Length ? 0 : bytesRead;
        return bytesRead == EncryptionConstants.MagicBytes.Length ? markerBuffer : null;
    }

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(EncryptionConstants.MagicBytes);

    private bool IsEndOfStream() => _input.Position >= _input.Length;

    private async Task ConsumeDecryptionChunksAsync(
        ChannelReader<IDecryptionChunk> channelReader,
        Stream output,
        CancellationToken cancellationToken
    )
    {
        await foreach (IDecryptionChunk chunk in channelReader.ReadAllAsync(cancellationToken))
        {
            switch (chunk)
            {
                case DecryptedMessageChunk messageChunk:
                    await output.WriteAsync(messageChunk.Data, cancellationToken);
                    break;
                case DecryptionErrorChunk errorChunk:
                    await HandleErrorChunkAsync(errorChunk, output, cancellationToken);
                    break;
                case EndOfStreamChunk:
                    await output.FlushAsync(cancellationToken);
                    return;
            }
        }
    }

    /// <summary>
    /// Handles an error chunk according to the configured error handling mode
    /// </summary>
    /// <exception cref="CryptographicException"></exception>
    private async Task HandleErrorChunkAsync(
        DecryptionErrorChunk errorChunk,
        Stream outputStream,
        CancellationToken cancellationToken
    )
    {
        switch (_options.ErrorHandlingMode)
        {
            case ErrorHandlingMode.WriteInline:
                string errorMessage =
                    $"[Decryption error at position {errorChunk.Position}: {errorChunk.ErrorMessage}]\n";
                byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                await outputStream.WriteAsync(errorBytes, cancellationToken);
                break;

            case ErrorHandlingMode.WriteToErrorLog:
                await WriteToErrorLogAsync(errorChunk, cancellationToken);
                break;

            case ErrorHandlingMode.ThrowException:
                throw new CryptographicException(
                    $"Decryption failed at position {errorChunk.Position}: {errorChunk.ErrorMessage}"
                );
        }
    }

    /// <summary>
    /// Writes an error chunk to the error log file
    /// </summary>
    private async Task WriteToErrorLogAsync(
        DecryptionErrorChunk errorChunk,
        CancellationToken cancellationToken
    )
    {
        _auditLogWriter ??= await CreateErrorLogWriterAsync(cancellationToken);

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry =
            $"[{timestamp}] Decryption error at position {errorChunk.Position}: {errorChunk.ErrorMessage}";
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

        DisposeRsa();
        _auditLogWriter?.Dispose();
        _disposed = true;
    }

    private void DisposeRsa()
    {
        foreach (KeyValuePair<string, RSA> keyInfo in _options.DecryptionKeys)
        {
            keyInfo.Value.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        DisposeRsa();

        if (_auditLogWriter != null)
        {
            await _auditLogWriter.DisposeAsync();
        }

        await _input.DisposeAsync();

        _disposed = true;
    }
}
