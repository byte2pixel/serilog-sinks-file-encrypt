using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers.v1;

/// <inheritdoc />
public class HeaderDecryptorV1 : IHeaderDecryptor
{
    /// <inheritdoc />
    public (byte[] AesKey, byte[] Nonce) Decrypt(RSA rsa, ReadOnlyMemory<byte> headerData)
    {
        // Decrypt the RSA payload
        byte[] decryptedPayload = rsa.Decrypt(
            headerData.ToArray(),
            RSAEncryptionPadding.OaepSHA256
        );
        // Read AES key length + key
        if (decryptedPayload.Length < 1)
        {
            throw new InvalidOperationException(
                "Decrypted payload is too short to read AES key length"
            );
        }

        int offset = 0;
        byte[] aesKey = decryptedPayload[offset..(HeaderMetadataV1.AesKeyLength)];
        offset += HeaderMetadataV1.AesKeyLength;

        if (decryptedPayload.Length < offset + HeaderMetadataV1.NonceLength)
        {
            throw new InvalidOperationException(
                $"Decrypted payload is too short to read nonce of length {HeaderMetadataV1.NonceLength}"
            );
        }

        byte[] nonce = decryptedPayload[offset..(offset + HeaderMetadataV1.NonceLength)];

        return (aesKey, nonce);
    }
}
