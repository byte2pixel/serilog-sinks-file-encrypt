using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

/// <summary>
/// Base class for encryption tests providing common test fixtures and helper methods.
/// All streams created during tests are automatically tracked and disposed.
/// </summary>
public abstract class EncryptionTestBase : IDisposable, IAsyncDisposable
{
    protected (string publicKey, string privateKey) RsaKeyPair { get; } =
        EncryptionUtils.GenerateRsaKeyPair();
    private readonly List<Stream> _streamsToDispose = [];
    private bool _disposed;

    /// <summary>
    /// Creates an encrypted memory stream with the given messages using synchronous Flush. Stream is automatically disposed.
    /// </summary>
    protected MemoryStream CreateEncryptedStream(string[] messages, string publicKey)
    {
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        // Create EncryptedStream but don't dispose it - disposing would close the underlying MemoryStream
        // We just need to flush the data
        EncryptedStream encryptedStream = new(memoryStream, rsa);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            encryptedStream.Write(message, 0, message.Length);
            encryptedStream.Flush();
        }

        // Don't dispose encryptedStream here - let it be garbage collected
        // The MemoryStream will be disposed by the test base

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Creates an encrypted memory stream with a single message using synchronous Flush. Stream is automatically disposed.
    /// </summary>
    protected MemoryStream CreateEncryptedStream(string message, string publicKey)
    {
        return CreateEncryptedStream([message], publicKey);
    }

    /// <summary>
    /// Creates an encrypted memory stream with the given messages using asynchronous FlushAsync. Stream is automatically disposed.
    /// </summary>
    protected async Task<MemoryStream> CreateEncryptedStreamAsync(
        string[] messages,
        string publicKey,
        CancellationToken cancellationToken = default
    )
    {
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        // Create EncryptedStream but don't dispose it - disposing would close the underlying MemoryStream
        EncryptedStream encryptedStream = new(memoryStream, rsa);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            await encryptedStream.WriteAsync(message, cancellationToken);
            await encryptedStream.FlushAsync(cancellationToken);
        }

        // Don't dispose encryptedStream here - let it be garbage collected
        // The MemoryStream will be disposed by the test base

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Creates an encrypted memory stream with a single message using asynchronous FlushAsync. Stream is automatically disposed.
    /// </summary>
    protected async Task<MemoryStream> CreateEncryptedStreamAsync(
        string message,
        string publicKey,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateEncryptedStreamAsync([message], publicKey, cancellationToken);
    }

    /// <summary>
    /// Creates a memory stream and tracks it for disposal. Use for manual stream creation.
    /// </summary>
    protected MemoryStream CreateMemoryStream()
    {
        MemoryStream stream = new();
        _streamsToDispose.Add(stream);
        return stream;
    }

    /// <summary>
    /// Creates a memory stream with data and tracks it for disposal.
    /// </summary>
    protected MemoryStream CreateMemoryStream(byte[] data)
    {
        MemoryStream stream = new(data);
        _streamsToDispose.Add(stream);
        return stream;
    }

    /// <summary>
    /// Encrypts and decrypts messages in one step, returning decrypted content as string
    /// </summary>
    protected async Task<string> EncryptAndDecryptAsync(
        string[] messages,
        string publicKey,
        string privateKey,
        CancellationToken cancellationToken = default
    )
    {
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            publicKey,
            cancellationToken
        );
        return await DecryptStreamToStringAsync(encryptedStream, privateKey, cancellationToken);
    }

    /// <summary>
    /// Encrypts and decrypts a single message in one step, returning decrypted content as string
    /// </summary>
    protected async Task<string> EncryptAndDecryptAsync(
        string message,
        string publicKey,
        string privateKey,
        CancellationToken cancellationToken = default
    )
    {
        return await EncryptAndDecryptAsync([message], publicKey, privateKey, cancellationToken);
    }

    /// <summary>
    /// Decrypts a stream and returns the decrypted content as a string
    /// </summary>
    protected async Task<string> DecryptStreamToStringAsync(
        Stream inputStream,
        string privateKey,
        CancellationToken cancellationToken = default
    )
    {
        MemoryStream outputStream = CreateMemoryStream();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            privateKey,
            cancellationToken: cancellationToken
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Decrypts a stream and returns the decrypted content as a string with custom options
    /// </summary>
    protected async Task<string> DecryptStreamToStringAsync(
        Stream inputStream,
        string privateKey,
        StreamingOptions options,
        CancellationToken cancellationToken = default
    )
    {
        MemoryStream outputStream = CreateMemoryStream();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            privateKey,
            options,
            cancellationToken
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a corrupted version of encrypted data by flipping bits at the specified position
    /// </summary>
    protected static byte[] CorruptData(byte[] data, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        corrupted[position] ^= 0xFF; // Flip all bits at position
        return corrupted;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose all tracked streams
            for (int i = _streamsToDispose.Count - 1; i >= 0; i--)
            {
                try
                {
                    _streamsToDispose[i].Dispose();
                }
                catch
                {
                    // Ignore disposal errors in tests
                }
            }
            _streamsToDispose.Clear();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        // Dispose all tracked streams asynchronously
        for (int i = _streamsToDispose.Count - 1; i >= 0; i--)
        {
            try
            {
                await _streamsToDispose[i].DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _streamsToDispose.Clear();

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
