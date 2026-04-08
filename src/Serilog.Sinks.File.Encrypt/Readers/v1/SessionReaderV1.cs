using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <inheritdoc />
internal class SessionReaderV1 : ISessionReader
{
    private readonly IHeaderReader _headerReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionReaderV1"/> class with the specified header decryptor.
    /// </summary>
    /// <param name="headerDecryptor"></param>
    public SessionReaderV1(IHeaderReader? headerDecryptor = null)
    {
        _headerReader = headerDecryptor ?? new HeaderReaderV1();
    }

    /// <inheritdoc />
    public async Task<DecryptionContext> ReadSessionAsync(
        Stream input,
        IKeyProvider keyProvider,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(keyProvider);
        // Read the header from the input stream
        Memory<byte> keyId = new byte[HeaderMetadataV1.KeyIdLength];
        // lookup the RSA key based on the keyId in the header
        await input.ReadExactlyAsync(keyId, cancellationToken);
        string keyIdStr = System.Text.Encoding.UTF8.GetString(keyId.Span).TrimEnd('\0');

        int headerSize = await keyProvider.GetKeySizeAsync(keyIdStr, cancellationToken) / 8;
        Memory<byte> header = new byte[headerSize];
        await input.ReadExactlyAsync(header, cancellationToken);

        // Decrypt the header to get the session key and nonce
        (byte[] aesKey, byte[] nonce) = await _headerReader.Decrypt(
            keyProvider,
            keyIdStr,
            header,
            cancellationToken
        );

        return new DecryptionContext(nonce, aesKey);
    }
}
