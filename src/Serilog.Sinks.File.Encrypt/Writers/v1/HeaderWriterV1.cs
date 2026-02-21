using System.Buffers;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers.v1;

/// <summary>
/// Version 1 header encoder that constructs a header containing the AES session key, nonce, and optional KeyId,
/// </summary>
internal sealed class HeaderWriterV1 : IHeaderWriter
{
    private readonly RSA _rsa;

    /// <summary>
    /// Initializes the header encoder with the RSA public key and optional KeyId.
    /// </summary>
    /// <param name="options">The encryption options containing the RSA public key and optional KeyId.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the KeyId is too long for the RSA key size.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the options or public key is null.</exception>
    internal HeaderWriterV1(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rsa = options.Rsa;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> Encrypt(ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce)
    {
        // Rent buffer from pool for RSA payload
        byte[] payload = ArrayPool<byte>.Shared.Rent(HeaderMetadataV1.RsaPayloadLength);
        try
        {
            int offset = 0;

            // Write AES key
            aesKey.CopyTo(payload.AsSpan(offset));
            offset += HeaderMetadataV1.AesKeyLength;

            // Write Nonce
            nonce.CopyTo(payload.AsSpan(offset));

            // RSA encrypt the payload
            byte[] encryptedPayload = _rsa.Encrypt(
                payload.AsSpan(0, HeaderMetadataV1.RsaPayloadLength),
                HeaderMetadataV1.Padding
            );

            return encryptedPayload.AsSpan();
        }
        finally
        {
            // Clear sensitive data and return to pool
            CryptographicOperations.ZeroMemory(
                payload.AsSpan(0, HeaderMetadataV1.RsaPayloadLength)
            );
            ArrayPool<byte>.Shared.Return(payload);
        }
    }
}
