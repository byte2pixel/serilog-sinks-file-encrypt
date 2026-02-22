namespace Serilog.Sinks.File.Encrypt.Tests;

/// <summary>
/// Base class for encryption tests providing common test fixtures and helper methods.
/// All streams created during tests are automatically tracked and disposed.
/// </summary>
public abstract class EncryptionTestBase : IDisposable, IAsyncDisposable
{
    protected (string publicKey, string privateKey) RsaKeyPair { get; } =
        CryptographicUtils.GenerateRsaKeyPair();
    private readonly List<Stream> _streamsToDispose = [];
    private bool _disposed;
    protected readonly EncryptionOptions EncryptOptions;
    protected readonly DecryptionOptions DecryptOptions;
    protected readonly ILogger Log = Substitute.For<ILogger>();

    protected EncryptionTestBase()
    {
        EncryptOptions = CreateEncryptionOptions();
        DecryptOptions = TestUtils.GetDecryptionOptions(RsaKeyPair.privateKey);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates an encrypted memory stream with the given messages using synchronous Flush. Stream is automatically disposed.
    /// </summary>
    protected MemoryStream CreateEncryptedStream(string[] messages)
    {
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);

        // Create EncryptedStream but don't dispose it - disposing would close the underlying MemoryStream
        // We just need to flush the data
        LogWriter logWriter = new(memoryStream, EncryptOptions);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            logWriter.Write(message, 0, message.Length);
            logWriter.Flush();
        }

        // Don't dispose encryptedStream here - let it be garbage collected
        // The MemoryStream will be disposed by the test base

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Creates an encrypted memory stream with a single message using synchronous Flush. Stream is automatically disposed.
    /// </summary>
    protected MemoryStream CreateEncryptedStream(string message)
    {
        return CreateEncryptedStream([message]);
    }

    /// <summary>
    /// Creates an encrypted memory stream with the given messages using asynchronous FlushAsync. Stream is automatically disposed.
    /// </summary>
    protected async Task<MemoryStream> CreateEncryptedStreamAsync(
        string[] messages,
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);

        // Create EncryptedStream but don't dispose it - disposing would close the underlying MemoryStream
        LogWriter logWriter = new(memoryStream, encryptionOptions ?? EncryptOptions);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            await logWriter.WriteAsync(message, ct);
            await logWriter.FlushAsync(ct);
        }

        await logWriter.FlushAsync(ct);

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
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        return await CreateEncryptedStreamAsync([message], encryptionOptions, cancellationToken);
    }

    protected async Task<MemoryStream> CreateAppendedMemoryStream(
        MemoryStream memoryStream,
        string message,
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        memoryStream.Position = memoryStream.Length;

        LogWriter logWriter = new(memoryStream, encryptionOptions ?? EncryptOptions);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        await logWriter.WriteAsync(messageBytes, ct);
        await logWriter.FlushAsync(ct);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Creates a memory stream and tracks it for disposal. Use for manual stream creation.
    /// </summary>
    private MemoryStream CreateMemoryStream()
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
        EncryptionOptions? encryptionOptions = null,
        DecryptionOptions? decryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            encryptionOptions,
            cancellationToken
        );
        return await DecryptStreamToStringAsync(
            encryptedStream,
            decryptionOptions,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Encrypts and decrypts a single message in one step, returning decrypted content as string
    /// </summary>
    protected async Task<string> EncryptAndDecryptAsync(
        string message,
        EncryptionOptions? encryptionOptions = null,
        DecryptionOptions? decryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        return await EncryptAndDecryptAsync(
            [message],
            encryptionOptions,
            decryptionOptions,
            cancellationToken
        );
    }

    /// <summary>
    /// Decrypts a stream and returns the decrypted content as a string
    /// </summary>
    protected async Task<string> DecryptStreamToStringAsync(
        Stream inputStream,
        DecryptionOptions? options = null,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        MemoryStream outputStream = CreateMemoryStream();

        await CryptographicUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            options ?? DecryptOptions,
            logger,
            cancellationToken: ct
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    protected EncryptionOptions CreateEncryptionOptions(
        string? publicKey = null,
        string? keyId = null,
        int version = 1
    )
    {
        return TestUtils.GetEncryptionOptions(publicKey ?? RsaKeyPair.publicKey, keyId, version);
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
                catch (ObjectDisposedException)
                {
                    // Ignore disposal errors in tests
                }
            }
            _streamsToDispose.Clear();
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose all tracked streams asynchronously
            for (int i = _streamsToDispose.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _streamsToDispose[i].DisposeAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // Ignore disposal errors in tests
                }
            }

            _streamsToDispose.Clear();
        }

        _disposed = true;
    }
}
