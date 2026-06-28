using System.Buffers;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Header encoder that constructs a header containing the AES session key and nonce,
/// RSA-encrypted using the public key from the provided encryption options.
/// </summary>
internal sealed class HeaderWriter : IHeaderWriter
{
    private readonly RSA _rsa;

    /// <summary>
    /// Initializes the header encoder with the RSA public key.
    /// </summary>
    /// <param name="options">The encryption options containing the RSA public key.</param>
    /// <exception cref="ArgumentNullException">Thrown when the options or public key is null.</exception>
    internal HeaderWriter(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rsa = options.Rsa;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> Encrypt(ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce)
    {
        // Rent buffer from pool for RSA payload
        byte[] payload = ArrayPool<byte>.Shared.Rent(HeaderMetadata.RsaPayloadLength);
        try
        {
            int offset = 0;

            // Write AES key
            aesKey.CopyTo(payload.AsSpan(offset));
            offset += HeaderMetadata.AesKeyLength;

            // Write Nonce
            nonce.CopyTo(payload.AsSpan(offset));

            // RSA encrypt the payload
            byte[] encryptedPayload = _rsa.Encrypt(
                payload.AsSpan(0, HeaderMetadata.RsaPayloadLength),
                HeaderMetadata.Padding
            );

            return encryptedPayload.AsSpan();
        }
        finally
        {
            // Clear sensitive data and return to pool
            CryptographicOperations.ZeroMemory(payload.AsSpan(0, HeaderMetadata.RsaPayloadLength));
            ArrayPool<byte>.Shared.Return(payload);
        }
    }
}
