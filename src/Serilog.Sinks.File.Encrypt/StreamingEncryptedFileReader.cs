using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Handles the reading and parsing of encrypted log files with streaming support
/// </summary>
internal sealed class StreamingEncryptedFileReader : IDisposable, IAsyncDisposable
{
    private readonly Stream _inputStream;
    private readonly RSA _rsa;
    private readonly StreamingOptions _options;
    private DecryptionContext _context;
    private StreamWriter? _errorLogWriter;
    private bool _disposed;
    private bool _hasFoundValidHeader;

    private int EncryptedHeaderSize => _rsa.KeySize / 8;

    public StreamingEncryptedFileReader(
        Stream inputStream,
        string privateKey,
        StreamingOptions options
    )
    {
        _inputStream = inputStream;
        _rsa = RSA.Create();
        _rsa.FromString(privateKey);
        _options = options;
        _context = DecryptionContext.Empty;
        _hasFoundValidHeader = false;
    }

    /// <summary>
    /// Decrypts the entire stream and writes to the output stream
    /// </summary>
    public async Task DecryptToStreamAsync(
        Stream outputStream,
        CancellationToken cancellationToken = default
    )
    {
        // Create a bounded channel for producer-consumer pattern
        var channel = Channel.CreateBounded<IDecryptionChunk>(
            new BoundedChannelOptions(_options.QueueDepth)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );

        // Start producer and consumer tasks
        Task producerTask = ProduceDecryptionChunksAsync(channel.Writer, cancellationToken);
        Task consumerTask = ConsumeDecryptionChunksAsync(
            channel.Reader,
            outputStream,
            cancellationToken
        );

        // Wait for both tasks to complete
        await Task.WhenAll(producerTask, consumerTask);
    }

    /// <summary>
    /// Producer task that reads and decrypts sections from the input stream
    /// </summary>
    /// <exception cref="CryptographicException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
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
                            $"Decryption failed at position {_inputStream.Position}: {ex.Message}",
                            ex
                        );
                    }

                    // For other modes, write error chunk and let consumer handle it
                    await writer.WriteAsync(
                        new DecryptionErrorChunk(ex.Message, _inputStream.Position),
                        cancellationToken
                    );
                    writer.Complete();
                    throw;
                }
            }

            // Validate that we found at least one valid encryption header
            if (!_hasFoundValidHeader)
            {
                throw new InvalidOperationException(
                    "The file does not contain valid encryption markers. "
                        + "This may not be an encrypted log file or the file is corrupted."
                );
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

    /// <summary>
    /// Consumer task that writes decrypted chunks to the output stream
    /// </summary>
    private async Task ConsumeDecryptionChunksAsync(
        ChannelReader<IDecryptionChunk> reader,
        Stream outputStream,
        CancellationToken cancellationToken
    )
    {
        await foreach (IDecryptionChunk chunk in reader.ReadAllAsync(cancellationToken))
        {
            switch (chunk)
            {
                case DecryptedMessageChunk messageChunk:
                    byte[] bytes = Encoding.UTF8.GetBytes(messageChunk.Content);
                    await outputStream.WriteAsync(bytes, cancellationToken);
                    break;

                case DecryptionErrorChunk errorChunk:
                    await HandleErrorChunkAsync(errorChunk, outputStream, cancellationToken);
                    break;

                case EndOfStreamChunk:
                    await outputStream.FlushAsync(cancellationToken);
                    return;
            }
        }
    }

    /// <summary>
    /// Processes the next section in the file (header or body)
    /// </summary>
    private async Task ProcessNextSectionAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
        byte[]? markerBuffer = await ReadMarkerBufferAsync(cancellationToken);

        if (markerBuffer == null)
        {
            return;
        }

        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessHeaderSectionAsync(cancellationToken);
        }
        else if (_context.HasKeys)
        {
            try
            {
                await ProcessBodySectionAsync(writer, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                _inputStream.Position++; // Move forward assuming corrupted data
            }
        }
        else
        {
            // move the stream position forward by 1 byte to continue searching
            // for the first valid header marker
            _inputStream.Position++;
        }
    }

    /// <summary>
    /// Processes a header section to extract encryption keys
    /// </summary>
    private async Task ProcessHeaderSectionAsync(CancellationToken cancellationToken)
    {
        HeaderSection header = await ReadHeaderSectionAsync(cancellationToken);

        // Mark that we found a valid header marker (before attempting decryption)
        // This way, if decryption fails with wrong key, we get a proper crypto error
        // rather than "no valid markers" error
        _hasFoundValidHeader = true;

        _context = DecryptKeys(header);
    }

    /// <summary>
    /// Processes a body section to decrypt and queue message content
    /// </summary>
    private async Task ProcessBodySectionAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
        MessageSection body = await ReadMessageSectionAsync(cancellationToken);

        string decryptedText = await DecryptMessageContentAsync(
            body.MessageLength,
            cancellationToken
        );
        await writer.WriteAsync(new DecryptedMessageChunk(decryptedText), cancellationToken);
    }

    /// <summary>
    /// Reads header section data from the input stream
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<HeaderSection> ReadHeaderSectionAsync(CancellationToken cancellationToken)
    {
        // skip the header marker
        _inputStream.Position += EncryptionConstants.SizeOfInt;

        // Read the tag, nonce, and session key lengths
        byte[] versionBuffer = new byte[EncryptionConstants.Version.Length];
        await _inputStream.ReadExactlyAsync(versionBuffer, cancellationToken);
        if (!versionBuffer.SequenceEqual(EncryptionConstants.Version))
        {
            // Can refactor to support multiple versions in the future.
            throw new InvalidOperationException(
                $"Unsupported stream version: {BitConverter.ToString(versionBuffer)}"
            );
        }
        byte[] encryptedHeader = new byte[EncryptedHeaderSize];
        await _inputStream.ReadExactlyAsync(encryptedHeader, cancellationToken);
        return new HeaderSection(encryptedHeader);
    }

    /// <summary>
    /// Reads body section metadata from the input stream
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<MessageSection> ReadMessageSectionAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[EncryptionConstants.SizeOfInt];
        try
        {
            await _inputStream.ReadExactlyAsync(lengthBytes, cancellationToken);
            int messageLength = BitConverter.ToInt32(lengthBytes, 0);
            if (messageLength > 0) // not sure if this should allow zero-length messages
            {
                return new MessageSection(messageLength);
            }

            _inputStream.Position -= lengthBytes.Length;
            throw new InvalidOperationException(
                $"Invalid message length: {messageLength} at position {_inputStream.Position - EncryptionConstants.SizeOfInt}"
            );
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidOperationException(
                "Unexpected end of stream while reading message length.",
                ex
            );
        }
    }

    /// <summary>
    /// Decrypts the AES keys from the header section
    /// </summary>
    private DecryptionContext DecryptKeys(HeaderSection header)
    {
        byte[] decryptedHeader = _rsa.Decrypt(
            header.EncryptedHeader,
            RSAEncryptionPadding.OaepSHA256
        );
        byte[] tagLengthBytes = decryptedHeader[..EncryptionConstants.SizeOfInt];
        byte[] nonce = decryptedHeader[
            EncryptionConstants.SizeOfInt..(EncryptionConstants.HeaderSessionKeyOffset)
        ];
        byte[] sessionKey = decryptedHeader[
            (EncryptionConstants.HeaderSessionKeyOffset)..(
                EncryptionConstants.HeaderSessionKeyOffset + EncryptionConstants.SessionKeyLength
            )
        ];

        int tag = BitConverter.ToInt32(tagLengthBytes);

        return new DecryptionContext(tag, nonce, sessionKey);
    }

    /// <summary>
    /// Decrypts message content from the input stream using streaming
    /// </summary>
    private async Task<string> DecryptMessageContentAsync(
        int messageLength,
        CancellationToken cancellationToken
    )
    {
        byte[] message = new byte[messageLength];
        await _inputStream.ReadExactlyAsync(message, cancellationToken);

        byte[] cypherText = message[..^_context.TagLength];
        byte[] hmac = message[^_context.TagLength..];

        string plainText = DecryptData(
            cypherText,
            _context.SessionKey,
            _context.Nonce,
            hmac,
            _context.TagLength
        );

        _context.Nonce.IncreaseNonce();

        return plainText;
    }

    /// <summary>
    /// Decrypts data using AES with the provided key and IV using streaming approach
    /// </summary>
    private static string DecryptData(
        byte[] cypherText,
        byte[] sessionKey,
        byte[] nonce,
        byte[] hmac,
        int tagLength
    )
    {
        using AesGcm aes = new AesGcm(sessionKey, tagLength);
        using MemoryStream memoryStream = new();

        byte[] plainText = new byte[cypherText.Length];

        aes.Decrypt(nonce, cypherText, hmac, plainText);

        return Encoding.UTF8.GetString(plainText);
    }

    /// <summary>
    /// Handles an error chunk according to the configured error handling mode
    /// </summary>
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
            case ErrorHandlingMode.Skip:
            default:
                break;
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
        _errorLogWriter ??= await CreateErrorLogWriterAsync(cancellationToken);

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logEntry =
            $"[{timestamp}] Decryption error at position {errorChunk.Position}: {errorChunk.ErrorMessage}";

        await _errorLogWriter.WriteLineAsync(logEntry.AsMemory(), cancellationToken);
        await _errorLogWriter.FlushAsync(cancellationToken);
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

    // Helper methods
    private async Task<byte[]?> ReadMarkerBufferAsync(CancellationToken cancellationToken)
    {
        byte[] markerBuffer = new byte[EncryptionConstants.SizeOfInt];
        int bytesRead = await _inputStream.ReadAsync(markerBuffer, cancellationToken);
        _inputStream.Position -= bytesRead != EncryptionConstants.SizeOfInt ? 0 : bytesRead;
        return bytesRead == EncryptionConstants.SizeOfInt ? markerBuffer : null;
    }

    private bool IsEndOfStream() => _inputStream.Position >= _inputStream.Length;

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(EncryptionConstants.Marker);

    private async Task HandleErrorAsync(
        ChannelWriter<IDecryptionChunk> writer,
        Exception ex,
        CancellationToken cancellationToken
    )
    {
        await writer.WriteAsync(
            new DecryptionErrorChunk(ex.Message, _inputStream.Position),
            cancellationToken
        );
    }

    /// <summary>
    /// Tries to recover from decryption errors by seeking to the next valid marker
    /// </summary>
    private async Task TryRecoverAsync(CancellationToken cancellationToken)
    {
        // Try to find the next valid marker
        byte[] searchBuffer = new byte[EncryptionConstants.SizeOfInt];

        while (_inputStream.Position < _inputStream.Length - EncryptionConstants.SizeOfInt)
        {
            // Read potential marker
            int bytesRead = await _inputStream.ReadAsync(searchBuffer, cancellationToken);
            if (bytesRead < EncryptionConstants.SizeOfInt)
            {
                break;
            }

            // Check if it's a valid marker
            if (IsHeaderMarker(searchBuffer))
            {
                // Move back to start of marker
                _inputStream.Position -= EncryptionConstants.SizeOfInt;
                return;
            }

            // Move back and advance by 1 byte to continue searching
            _inputStream.Position -= EncryptionConstants.SizeOfInt - 1;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rsa.Dispose();
        _errorLogWriter?.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _rsa.Dispose();

        if (_errorLogWriter != null)
        {
            await _errorLogWriter.DisposeAsync();
        }

        await _inputStream.DisposeAsync();

        _disposed = true;
    }
}
