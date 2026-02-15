using System.Buffers;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers;

/// <inheritdoc />
public class MessageDecryptorV1 : IMessageDecryptor
{
    /// <inheritdoc />
    public ReadOnlyMemory<byte> ReadAndDecrypt(DecryptionSessionChunk sessionChunk)
    {
        using var aes = new AesGcm(sessionChunk.AesKey, EncryptionConstants.TagLength);
        // The encrypted message is structured as: [ciphertext][tag]
        int ciphertextLength = sessionChunk.Payload.Length - EncryptionConstants.TagLength;
        if (ciphertextLength < 0)
        {
            throw new InvalidOperationException(
                $"Encrypted message is too short to contain a valid tag of length {EncryptionConstants.TagLength}"
            );
        }

        // Rent buffers from pool
        byte[] plaintext = ArrayPool<byte>.Shared.Rent(ciphertextLength);
        try
        {
            // Decrypt directly into pooled buffer
            aes.Decrypt(
                sessionChunk.Nonce,
                sessionChunk.Payload.Slice(0, ciphertextLength).ToArray(),
                sessionChunk
                    .Payload.Slice(ciphertextLength, EncryptionConstants.TagLength)
                    .ToArray(),
                plaintext.AsSpan(0, ciphertextLength)
            );

            return plaintext.AsMemory(0, ciphertextLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(plaintext);
        }
    }
}
