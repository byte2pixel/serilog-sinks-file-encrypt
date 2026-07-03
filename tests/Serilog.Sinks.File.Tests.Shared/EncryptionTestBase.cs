namespace Serilog.Sinks.File.Tests.Shared;

/// <summary>
/// Base class for encryption/decryption tests providing common test fixtures and helper methods.
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
        LocalKeyProvider keyProvider = new(
            new Dictionary<string, string> { { "", RsaKeyPair.privateKey } }
        );
        DecryptOptions = new DecryptionOptions { KeyProvider = keyProvider };
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
    /// Creates an encrypted memory stream with the given messages using synchronous Flush.
    /// The <see cref="LogWriter"/> is disposed, so the session ends with a seal record (clean close).
    /// Stream is automatically disposed.
    /// </summary>
    protected MemoryStream CreateEncryptedStream(string[] messages)
    {
        using MemoryStream temp = new();
        using (LogWriter logWriter = new(temp, EncryptOptions))
        {
            foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
            {
                logWriter.Write(message, 0, message.Length);
                logWriter.Flush();
            }
        }

        // LogWriter.Dispose seals the session and disposes the inner stream;
        // re-wrap the bytes in a fresh expandable stream for the caller.
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);
        memoryStream.Write(temp.ToArray());
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
    /// Creates an encrypted memory stream with the given messages using asynchronous FlushAsync.
    /// The <see cref="LogWriter"/> is disposed, so the session ends with a seal record (clean close).
    /// Stream is automatically disposed.
    /// </summary>
    protected async Task<MemoryStream> CreateEncryptedStreamAsync(
        string[] messages,
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        using MemoryStream temp = new();
        await using (LogWriter logWriter = new(temp, encryptionOptions ?? EncryptOptions))
        {
            foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
            {
                await logWriter.WriteAsync(message, ct);
                await logWriter.FlushAsync(ct);
            }
        }

        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);
        memoryStream.Write(temp.ToArray());
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Creates an encrypted memory stream whose <see cref="LogWriter"/> is deliberately never
    /// disposed, simulating a writer crash: all frames are flushed but no seal record is written.
    /// Stream is automatically disposed.
    /// </summary>
    protected async Task<MemoryStream> CreateUnsealedEncryptedStreamAsync(
        string[] messages,
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        MemoryStream memoryStream = new();
        _streamsToDispose.Add(memoryStream);

        LogWriter logWriter = new(memoryStream, encryptionOptions ?? EncryptOptions);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            await logWriter.WriteAsync(message, ct);
            await logWriter.FlushAsync(ct);
        }

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

    /// <summary>
    /// Appends a complete (sealed) session with the given message to an existing stream,
    /// simulating an application restart or rolling append.
    /// </summary>
    protected async Task<MemoryStream> CreateAppendedMemoryStream(
        MemoryStream memoryStream,
        string message,
        EncryptionOptions? encryptionOptions = null,
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;

        using MemoryStream temp = new();
        await using (LogWriter logWriter = new(temp, encryptionOptions ?? EncryptOptions))
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            await logWriter.WriteAsync(messageBytes, ct);
            await logWriter.FlushAsync(ct);
        }

        memoryStream.Position = memoryStream.Length;
        memoryStream.Write(temp.ToArray());
        memoryStream.Position = 0;
        return memoryStream;
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

    private MemoryStream CreateMemoryStream()
    {
        MemoryStream stream = new();
        _streamsToDispose.Add(stream);
        return stream;
    }

    /// <summary>
    /// Encrypts and decrypts messages in one step, returning decrypted content as string.
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
    /// Encrypts and decrypts a single message in one step, returning decrypted content as string.
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
    /// Decrypts a stream and returns the decrypted content as a string.
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

        await DecryptionUtils.DecryptLogFileAsync(
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
        string? keyId = null
    )
    {
        return TestUtils.GetEncryptionOptions(publicKey ?? RsaKeyPair.publicKey, keyId);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            for (int i = _streamsToDispose.Count - 1; i >= 0; i--)
            {
                try
                {
                    _streamsToDispose[i].Dispose();
                }
                catch (ObjectDisposedException) { }
            }
            _streamsToDispose.Clear();
        }

        var keyProvider = DecryptOptions.KeyProvider as LocalKeyProvider;
        keyProvider?.Dispose();

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
            for (int i = _streamsToDispose.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _streamsToDispose[i].DisposeAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { }
            }

            _streamsToDispose.Clear();
        }

        _disposed = true;
    }
}
