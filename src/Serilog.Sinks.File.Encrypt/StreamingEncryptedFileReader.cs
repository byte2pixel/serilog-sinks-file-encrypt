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
    // csharpier-ignore-start
    private static readonly byte[] _marker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
    private static readonly byte _escapeMarker = 0x00;
    // csharpier-ignore-end
    private static readonly int _markerLength = _marker.Length;

    private readonly Stream _inputStream;
    private readonly RSA _rsa;
    private readonly StreamingOptions _options;
    private DecryptionContext _context;
    private StreamWriter? _errorLogWriter;
    private bool _disposed;
    private bool _hasFoundValidHeader;

    public StreamingEncryptedFileReader(
        Stream inputStream,
        string rsaPrivateKey,
        StreamingOptions options
    )
    {
        _inputStream = inputStream;
        _rsa = RSA.Create();
        _rsa.FromXmlString(rsaPrivateKey);
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
            await ProcessBodySectionAsync(writer, cancellationToken);
        }
        else
        {
            // move the stream position forward by 1 byte to continue searching
            // for the first valid header marker
            _inputStream.Position += 1;
        }
    }

    /// <summary>
    /// Processes a header section to extract encryption keys
    /// </summary>
    private async Task ProcessHeaderSectionAsync(
        CancellationToken cancellationToken
    )
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
    private async Task<HeaderSection> ReadHeaderSectionAsync(
        CancellationToken cancellationToken
    )
    {
        // skip the header marker
        _inputStream.Position += _markerLength;

        // Read the tag, nonce, and session key lengths
        byte[] tagLengthBytes = new byte[sizeof(int)];
        byte[] nonceLengthBytes = new byte[sizeof(int)];
        byte[] sessionKeyLengthBytes = new byte[sizeof(int)];
        await _inputStream.ReadExactlyAsync(tagLengthBytes, cancellationToken);
        await _inputStream.ReadExactlyAsync(nonceLengthBytes, cancellationToken);
        await _inputStream.ReadExactlyAsync(sessionKeyLengthBytes, cancellationToken);

        int tagBytesLength = BitConverter.ToInt32(tagLengthBytes, 0);
        int nonceLength = BitConverter.ToInt32(nonceLengthBytes, 0);
        int sessionKeyLength = BitConverter.ToInt32(sessionKeyLengthBytes, 0);

        byte[] headerSection = new byte[tagBytesLength + nonceLength + sessionKeyLength];

        await _inputStream.ReadExactlyAsync(headerSection, cancellationToken);

        Unescape(ref headerSection);

        return new HeaderSection(headerSection[..tagBytesLength],
            headerSection[tagBytesLength..^nonceLength],
            headerSection[^sessionKeyLength..]);
    }

    /// <summary>
    /// Reads body section metadata from the input stream
    /// </summary>
    private async Task<MessageSection> ReadMessageSectionAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[4];
        await _inputStream.ReadExactlyAsync(lengthBytes, cancellationToken);
        int messageLength = BitConverter.ToInt32(lengthBytes, 0);
        return new MessageSection(messageLength);
    }

    /// <summary>
    /// Decrypts the AES keys from the header section
    /// </summary>
    private DecryptionContext DecryptKeys(HeaderSection header)
    {
        byte[] tagLengthBytes = _rsa.Decrypt(header.TagLength, RSAEncryptionPadding.OaepSHA256);
        byte[] nonce = _rsa.Decrypt(header.Nonce, RSAEncryptionPadding.OaepSHA256);
        byte[] sessionkey = _rsa.Decrypt(header.SessionKey, RSAEncryptionPadding.OaepSHA256);

        int tag = BitConverter.ToInt32(tagLengthBytes);

        return new DecryptionContext(tag, nonce, sessionkey);
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
        Unescape(ref message);

        byte[] cypherText = message[..^_context.TagLength];
        byte[] hmac = message[^_context.TagLength..];

        string plainText = DecryptData(cypherText, _context.SessionKey, _context.Nonce, hmac, _context.TagLength);

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
        byte[] markerBuffer = new byte[_markerLength];
        int bytesRead = await _inputStream.ReadAsync(markerBuffer, cancellationToken);
        _inputStream.Position -= bytesRead != _markerLength ? 0 : _markerLength;
        return bytesRead == _markerLength ? markerBuffer : null;
    }

    /// <summary>
    /// Unescapes occurrences of the specified marker in the data by removing in-place the escape byte after each occurrence.
    /// </summary>
    /// <param name="data">The data to unescape.</param>
    private void Unescape(ref byte[] data)
    {
        byte[] markerWithEscape = new byte[_marker.Length + 1];

        markerWithEscape[0] = _marker[0];
        markerWithEscape[1] = _escapeMarker;
        Array.Copy(_marker, 1, markerWithEscape, 2, _marker.Length - 1);

        while (true)
        {
            int pos = data.IndexOf(markerWithEscape);

            if (pos != -1)
            {
                Array.Copy(data, pos + 2, data, pos + 1, data.Length - (pos + 2));
                Array.Resize(ref data, data.Length - 1);
            }
            else
            {
                break;
            }
        }
    }

    private bool IsEndOfStream() => _inputStream.Position >= _inputStream.Length;

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(_marker);

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
        byte[] searchBuffer = new byte[_markerLength];

        while (_inputStream.Position < _inputStream.Length - _markerLength)
        {
            // Read potential marker
            int bytesRead = await _inputStream.ReadAsync(searchBuffer, cancellationToken);
            if (bytesRead < _markerLength)
            {
                break;
            }

            // Check if it's a valid marker
            if (IsHeaderMarker(searchBuffer))
            {
                // Move back to start of marker
                _inputStream.Position -= _markerLength;
                return;
            }

            // Move back and advance by 1 byte to continue searching
            _inputStream.Position -= _markerLength - 1;
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
