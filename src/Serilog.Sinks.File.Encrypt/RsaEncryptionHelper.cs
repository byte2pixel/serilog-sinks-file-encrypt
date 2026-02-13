using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Helper class for calculating RSA encryption limits and validating payload sizes.
/// </summary>
/// <remarks>
/// RSA encryption with OAEP padding has strict size limitations based on the key size and hash algorithm.
/// This class provides methods to calculate these limits and validate payloads before encryption.
/// </remarks>
internal static class RsaEncryptionHelper
{
    /// <summary>
    /// Calculates the maximum plaintext size that can be encrypted with RSA-OAEP.
    /// </summary>
    /// <param name="keySize">The RSA key size in bits.</param>
    /// <param name="padding">The RSA encryption padding mode.</param>
    /// <returns>The maximum number of bytes that can be encrypted.</returns>
    /// <exception cref="NotSupportedException">Thrown when the padding mode is not supported.</exception>
    /// <remarks>
    /// Formula for OAEP: MaxPlaintextSize = (KeySize / 8) - 2 - (2 × HashSize / 8)
    /// <list type="bullet">
    /// <item>OAEP-SHA1: KeySize / 8 - 42 bytes</item>
    /// <item>OAEP-SHA256: KeySize / 8 - 66 bytes</item>
    /// <item>OAEP-SHA384: KeySize / 8 - 98 bytes</item>
    /// <item>OAEP-SHA512: KeySize / 8 - 130 bytes</item>
    /// </list>
    /// Examples:
    /// <list type="bullet">
    /// <item>2048-bit with OAEP-SHA256: 256 - 66 = 190 bytes</item>
    /// <item>4096-bit with OAEP-SHA256: 512 - 66 = 446 bytes</item>
    /// </list>
    /// </remarks>
    public static int GetMaxPlaintextSize(int keySize, RSAEncryptionPadding padding)
    {
        int keySizeBytes = keySize / 8;

        if (padding == RSAEncryptionPadding.OaepSHA256)
        {
            // SHA-256 produces 32-byte hash
            // OAEP overhead: 2 + (2 × hashSize) = 2 + 64 = 66 bytes
            return keySizeBytes - 66;
        }

        if (padding == RSAEncryptionPadding.OaepSHA1)
        {
            // SHA-1 produces 20-byte hash
            // OAEP overhead: 2 + (2 × hashSize) = 2 + 40 = 42 bytes
            return keySizeBytes - 42;
        }

        if (padding == RSAEncryptionPadding.OaepSHA384)
        {
            // SHA-384 produces 48-byte hash
            // OAEP overhead: 2 + (2 × hashSize) = 2 + 96 = 98 bytes
            return keySizeBytes - 98;
        }

        if (padding == RSAEncryptionPadding.OaepSHA512)
        {
            // SHA-512 produces 64-byte hash
            // OAEP overhead: 2 + (2 × hashSize) = 2 + 128 = 130 bytes
            return keySizeBytes - 130;
        }

        if (padding == RSAEncryptionPadding.Pkcs1)
        {
            // PKCS#1 v1.5 overhead: 11 bytes minimum
            return keySizeBytes - 11;
        }

        throw new NotSupportedException(
            $"Padding mode {padding} is not supported for size calculation."
        );
    }

    /// <summary>
    /// Validates that a payload can be encrypted with the given RSA key and padding.
    /// </summary>
    /// <param name="payloadSize">The size of the payload to encrypt in bytes.</param>
    /// <param name="keySize">The RSA key size in bits.</param>
    /// <param name="padding">The RSA encryption padding mode.</param>
    /// <exception cref="ArgumentException">Thrown when the payload exceeds the maximum size for the key.</exception>
    public static void ValidatePayloadSize(
        int payloadSize,
        int keySize,
        RSAEncryptionPadding padding
    )
    {
        int maxSize = GetMaxPlaintextSize(keySize, padding);

        if (payloadSize > maxSize)
        {
            throw new ArgumentException(
                $"Payload size ({payloadSize} bytes) exceeds maximum size ({maxSize} bytes) "
                    + $"for RSA-{keySize} with {padding.Mode} padding. "
                    + $"Consider using a larger key size or reducing the payload (e.g., shorter KeyId).",
                nameof(payloadSize)
            );
        }
    }
}
