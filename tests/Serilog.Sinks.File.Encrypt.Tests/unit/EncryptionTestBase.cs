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
    private RSA? _rsa;
    protected EncryptionOptions EncryptOptions;
    protected DecryptionOptions DecryptOptions;

    protected EncryptionTestBase()
    {
        EncryptOptions = CreateEncryptionOptions();
        DecryptOptions = CreateDecryptionOptions();
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
        EncryptedLogStream encryptedStream = new(memoryStream, EncryptOptions);

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
        EncryptedLogStream encryptedStream = new(memoryStream, encryptionOptions ?? EncryptOptions);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            await encryptedStream.WriteAsync(message, ct);
            await encryptedStream.FlushAsync(ct);
        }

        await encryptedStream.FlushAsync(ct);

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

        EncryptedLogStream encryptedStream = new(memoryStream, encryptionOptions ?? EncryptOptions);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        await encryptedStream.WriteAsync(messageBytes, ct);
        await encryptedStream.FlushAsync(ct);
        memoryStream.Position = 0;
        return memoryStream;
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
            cancellationToken
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
        CancellationToken? cancellationToken = null
    )
    {
        CancellationToken ct = cancellationToken ?? TestContext.Current.CancellationToken;
        MemoryStream outputStream = CreateMemoryStream();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            options ?? DecryptOptions,
            cancellationToken: ct
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
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

    /// <summary>
    /// Corrupts data by inserting specific marker bytes at the given position
    /// </summary>
    /// <param name="data">The data to corrupt</param>
    /// <param name="position">The position to insert the marker</param>
    /// <param name="marker">The marker to insert</param>
    /// <returns>
    /// The corrupted data with the marker inserted at the specified position
    /// </returns>
    protected static byte[] CorruptDataAddingMarker(byte[] data, byte[] marker, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        // Insert marker at position
        for (int i = 0; i < marker.Length && (position + i) < corrupted.Length; i++)
        {
            corrupted[position + i] = marker[i];
        }
        return corrupted;
    }

    protected DecryptionOptions CreateDecryptionOptions(
        Dictionary<string, string>? decryptionKeys = null
    )
    {
        if (decryptionKeys is not null && decryptionKeys.Count == 0)
        {
            throw new ArgumentException(
                "Decryption keys dictionary cannot be empty",
                nameof(decryptionKeys)
            );
        }
        Dictionary<string, string> keyMap =
            decryptionKeys ?? new Dictionary<string, string> { { "", RsaKeyPair.privateKey } };
        return new DecryptionOptions { DecryptionKeys = keyMap };
    }

    protected EncryptionOptions CreateEncryptionOptions(
        string? publicKey = null,
        string? keyId = null
    )
    {
        _rsa?.Dispose();
        _rsa = RSA.Create();
        _rsa.FromString(publicKey ?? RsaKeyPair.publicKey);
        return new EncryptionOptions(_rsa, keyId ?? "");
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
                catch
                {
                    // Ignore disposal errors in tests
                }
            }

            _streamsToDispose.Clear();
        }

        _disposed = true;
    }
}
