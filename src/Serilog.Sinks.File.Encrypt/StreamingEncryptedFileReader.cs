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
    private static readonly byte[] HeaderMarker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
    private static readonly byte[] MessageMarker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x42, 0x44, 0x00, 0x02];
    // csharpier-ignore-end
    private static readonly int MarkerLength = HeaderMarker.Length;

    private readonly Stream _inputStream;
    private readonly RSA _rsa;
    private readonly StreamingOptions _options;
    private DecryptionContext _context;
    private bool _disposed;

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
    private async Task ProduceDecryptionChunksAsync(
        ChannelWriter<IDecryptionChunk> writer,
        CancellationToken cancellationToken
    )
    {
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
                    await writer.WriteAsync(
                        new DecryptionErrorChunk(ex.Message, _inputStream.Position),
                        cancellationToken
                    );
                    throw;
                }
            }
        }
        finally
        {
            await writer.WriteAsync(EndOfStreamChunk.Instance, cancellationToken);
            writer.Complete();
        }
    }

    /// <summary>
    /// Consumer task that writes decrypted chunks to the output stream
    /// </summary>
    private static async Task ConsumeDecryptionChunksAsync(
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
                    string errorMessage =
                        $"[Decryption error at position {errorChunk.Position}: {errorChunk.ErrorMessage}]\n";
                    byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                    await outputStream.WriteAsync(errorBytes, cancellationToken);
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
            return;

        if (IsHeaderMarker(markerBuffer))
        {
            await ProcessHeaderSectionAsync(markerBuffer, cancellationToken);
        }
        else if (IsBodyMarker(markerBuffer))
        {
            await ProcessBodySectionAsync(writer, cancellationToken);
        }
        else
        {
            SkipUnknownData(markerBuffer);
        }
    }

    /// <summary>
    /// Processes a header section to extract encryption keys
    /// </summary>
    private async Task ProcessHeaderSectionAsync(
        byte[] markerBuffer,
        CancellationToken cancellationToken
    )
    {
        long markerPosition = _inputStream.Position - markerBuffer.Length;

        if (!await IsValidHeaderAsync(markerPosition, cancellationToken))
        {
            SkipUnknownData(markerBuffer);
            return;
        }

        HeaderSection header = await ReadHeaderSectionAsync(markerPosition, cancellationToken);
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
        if (!_context.HasKeys)
            return;

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
        long markerPosition,
        CancellationToken cancellationToken
    )
    {
        _inputStream.Position = markerPosition + MarkerLength;

        // Read key and IV lengths
        byte[] keyLengthBytes = new byte[4];
        byte[] ivLengthBytes = new byte[4];
        await _inputStream.ReadExactlyAsync(keyLengthBytes, cancellationToken);
        await _inputStream.ReadExactlyAsync(ivLengthBytes, cancellationToken);

        int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

        // Read encrypted key and IV
        byte[] encryptedKey = new byte[keyLength];
        byte[] encryptedIv = new byte[ivLength];
        await _inputStream.ReadExactlyAsync(encryptedKey, cancellationToken);
        await _inputStream.ReadExactlyAsync(encryptedIv, cancellationToken);

        return new HeaderSection(encryptedKey, encryptedIv);
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
        byte[] key = _rsa.Decrypt(header.EncryptedKey, RSAEncryptionPadding.OaepSHA256);
        byte[] iv = _rsa.Decrypt(header.EncryptedIv, RSAEncryptionPadding.OaepSHA256);
        return new DecryptionContext(key, iv);
    }

    /// <summary>
    /// Decrypts message content from the input stream using streaming
    /// </summary>
    private async Task<string> DecryptMessageContentAsync(
        int dataLength,
        CancellationToken cancellationToken
    )
    {
        ValidateMessageSize(dataLength);

        byte[] encryptedData = new byte[dataLength];
        await _inputStream.ReadExactlyAsync(encryptedData, cancellationToken);

        return await DecryptDataAsync(encryptedData, _context.Key, _context.Iv, cancellationToken);
    }

    /// <summary>
    /// Validates that the message size is reasonable
    /// </summary>
    private static void ValidateMessageSize(int dataLength)
    {
        const int maxLogMessageSize = 10_000_000; // 10 MB should be more than enough for any single log message
        if (dataLength > maxLogMessageSize)
        {
            throw new InvalidOperationException(
                $"Log message size ({dataLength} bytes) is unexpectedly large (>{maxLogMessageSize} bytes). This may indicate file corruption."
            );
        }
    }

    /// <summary>
    /// Decrypts data using AES with the provided key and IV using streaming approach
    /// </summary>
    private static async Task<string> DecryptDataAsync(
        byte[] encryptedData,
        byte[] key,
        byte[] iv,
        CancellationToken cancellationToken
    )
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using MemoryStream memoryStream = new();
        await using CryptoStream cryptoStream = new(
            memoryStream,
            decryptor,
            CryptoStreamMode.Write
        );

        await cryptoStream.WriteAsync(encryptedData, cancellationToken);
        await cryptoStream.FlushFinalBlockAsync(cancellationToken);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    // Helper methods
    private async Task<byte[]?> ReadMarkerBufferAsync(CancellationToken cancellationToken)
    {
        byte[] markerBuffer = new byte[MarkerLength];
        int bytesRead = await _inputStream.ReadAsync(markerBuffer, cancellationToken);
        return bytesRead == markerBuffer.Length ? markerBuffer : null;
    }

    private bool IsEndOfStream() => _inputStream.Position >= _inputStream.Length;

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(HeaderMarker);

    private static bool IsBodyMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(MessageMarker);

    private void SkipUnknownData(byte[] markerBuffer) =>
        _inputStream.Position -= markerBuffer.Length - 1;

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
        byte[] searchBuffer = new byte[MarkerLength];

        while (_inputStream.Position < _inputStream.Length - MarkerLength)
        {
            // Read potential marker
            int bytesRead = await _inputStream.ReadAsync(searchBuffer, cancellationToken);
            if (bytesRead < MarkerLength)
                break;

            // Check if it's a valid marker
            if (IsHeaderMarker(searchBuffer) || IsBodyMarker(searchBuffer))
            {
                // Move back to start of marker
                _inputStream.Position -= MarkerLength;
                return;
            }

            // Move back and advance by 1 byte to continue searching
            _inputStream.Position -= MarkerLength - 1;
        }
    }

    /// <summary>
    /// Validates that a potential header marker is followed by reasonable key/IV length values
    /// </summary>
    private async Task<bool> IsValidHeaderAsync(
        long markerPosition,
        CancellationToken cancellationToken
    )
    {
        long originalPosition = _inputStream.Position;
        try
        {
            _inputStream.Position = markerPosition + MarkerLength;

            byte[] keyLengthBytes = new byte[4];
            byte[] ivLengthBytes = new byte[4];

            if (
                await _inputStream.ReadAsync(keyLengthBytes, cancellationToken) != 4
                || await _inputStream.ReadAsync(ivLengthBytes, cancellationToken) != 4
            )
            {
                return false;
            }

            int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
            int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

            // Validate the lengths are reasonable for RSA encrypted AES keys/IVs
            const int minKeyIvLength = 256;
            const int maxKeyIvLength = 4096;
            return keyLength is >= minKeyIvLength and <= maxKeyIvLength
                && ivLength is >= minKeyIvLength and <= maxKeyIvLength;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Restore original position so calling code can read the header data
            _inputStream.Position = originalPosition;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _rsa.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _rsa.Dispose();

        if (_inputStream is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            await _inputStream.DisposeAsync();

        _disposed = true;
    }
}
