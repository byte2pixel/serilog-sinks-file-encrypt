using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers.v1;

/// <inheritdoc />
public class HeaderDecryptorV1 : IHeaderDecryptor
{
    /// <inheritdoc />
    public (byte[] AesKey, byte[] Nonce) Decrypt(RSA rsa, ReadOnlySpan<byte> headerData)
    {
        int offset = 0;

        // Decrypt the RSA payload
        byte[] decryptedPayload = new byte[rsa.KeySize / 8];
        if (!rsa.TryDecrypt(headerData, decryptedPayload, RSAEncryptionPadding.OaepSHA256, out int _))
        {
            throw new CryptographicException("RSA decryption of header failed.");
        }
        // Read AES key
        if (decryptedPayload.Length < HeaderMetadataV1.AesKeyLength)
        {
            throw new InvalidDataException("Decrypted payload is too short to read AES key");
        }

        byte[] aesKey = decryptedPayload[offset..(HeaderMetadataV1.AesKeyLength)];
        offset += HeaderMetadataV1.AesKeyLength;

        if (decryptedPayload.Length < offset + HeaderMetadataV1.NonceLength)
        {
            throw new InvalidDataException("Decrypted payload is too short to read the nonce.");
        }

        byte[] nonce = decryptedPayload[offset..(offset + HeaderMetadataV1.NonceLength)];

        return (aesKey, nonce);
    }
}
