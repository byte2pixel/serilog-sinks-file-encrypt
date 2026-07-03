using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt.Interfaces;
using Serilog.Sinks.File.Decrypt.Models;
using Serilog.Sinks.File.Encrypt;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Decrypt;

/// <inheritdoc />
internal class SessionReader : ISessionReader
{
    private readonly IHeaderReader _headerReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionReader"/> class with the specified header decryptor.
    /// </summary>
    /// <param name="headerDecryptor">The header decryptor to use. Defaults to <see cref="HeaderReader"/> if not provided.</param>
    public SessionReader(IHeaderReader? headerDecryptor = null)
    {
        _headerReader = headerDecryptor ?? new HeaderReader();
    }

    /// <inheritdoc />
    public async Task<DecryptionContext> ReadSessionAsync(
        Stream input,
        IKeyProvider keyProvider,
        byte version,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(keyProvider);
        // Read the header from the input stream
        Memory<byte> keyId = new byte[HeaderMetadata.KeyIdLength];
        // lookup the RSA key based on the keyId in the header
        await input.ReadExactlyAsync(keyId, cancellationToken);
        string keyIdStr = System.Text.Encoding.UTF8.GetString(keyId.Span).TrimEnd('\0');

        int headerSize = await keyProvider.GetKeySizeAsync(keyIdStr, cancellationToken) / 8;
        Memory<byte> header = new byte[headerSize];
        await input.ReadExactlyAsync(header, cancellationToken);

        // For v2, hash the exact header bytes as they appear on disk
        // (magic + version + keyId + RSA payload); every frame's associated data is bound to it.
        byte[]? headerHash = null;
        if (version == EncryptionConstants.FormatVersionV2)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(CryptographicUtils.MagicBytes);
            hash.AppendData([version]);
            hash.AppendData(keyId.Span);
            hash.AppendData(header.Span);
            headerHash = hash.GetHashAndReset();
        }

        // Decrypt the header to get the session key and nonce
        (byte[] aesKey, byte[] nonce) = await _headerReader.Decrypt(
            keyProvider,
            keyIdStr,
            header,
            cancellationToken
        );

        // For v2, derive the reserved seal nonce (initial counter − 1) before the rolling nonce
        // is advanced, so the end-of-log seal can be authenticated no matter how many data
        // frames are actually present.
        byte[]? sealNonce = null;
        if (version == EncryptionConstants.FormatVersionV2)
        {
            sealNonce = (byte[])nonce.Clone();
            sealNonce.DecreaseNonce();
        }

        return new DecryptionContext(nonce, aesKey, version, headerHash, sealNonce, keyIdStr);
    }
}
