using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Utilities for generating RSA key pairs for use with Serilog.Sinks.File.Encrypt.
/// </summary>
/// <remarks>
/// This class provides static methods for key management.
/// All methods are thread-safe and can be called concurrently.
/// </remarks>
/// <example>
/// <code>
/// // Generate a key pair
/// var (publicKey, privateKey) = CryptographicUtils.GenerateRsaKeyPair(4096);
/// </code>
/// </example>
public static class CryptographicUtils
{
    /// <summary>
    /// The fixed magic bytes that identify the file format.
    /// 0xFF: Reserved byte (must be 0xFF) to easily detect when parsing messages, that a new session started.
    /// 0x42, 0x32, 0x50: ASCII "B2P" (stands for "Byte2Pixel")
    /// 0xFF, 0xDA, 0x7E: Random bytes for additional uniqueness
    /// 0x00: Reserved byte (must be 0)
    /// </summary>
    /// <remarks>
    /// Treat this as read-only. Only the array reference is <c>readonly</c>; its elements are not, so mutating
    /// them would corrupt the file-format marker process-wide. Do not modify the contents.
    /// </remarks>
    public static readonly byte[] MagicBytes = [0xFF, 0x42, 0x32, 0x50, 0xFF, 0xDA, 0x7E, 0x00];

    /// <summary>
    /// Imports an RSA key into an <see cref="RSA"/> instance from a string in either XML or PEM format.
    /// </summary>
    /// <param name="rsa">The <see cref="RSA"/> instance to import the key into.</param>
    /// <param name="key">The RSA key as a string.</param>
    /// <exception cref="CryptographicException">Unknown or invalid key format.</exception>
    /// <exception cref="ArgumentException">Thrown if the key is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the key is null.</exception>
    public static void FromString(this RSA rsa, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            switch (key[0])
            {
                case '<':
                    rsa.FromXmlString(key);
                    break;
                case '-':
                    rsa.ImportFromPem(key);
                    break;
                default:
                    throw new CryptographicException(
                        "Invalid RSA key format. Key must be in XML or PEM format."
                    );
            }
        }
        catch (Exception ex)
            when (ex is FormatException or ArgumentException or ArgumentNullException)
        {
            throw new CryptographicException(
                "Failed to import RSA key. See inner exception for details.",
                ex
            );
        }
    }

    /// <summary>
    /// AES-GCM encryption requires a unique nonce for each encryption operation.
    /// This method retrieves the 64-bit little-endian counter stored in the last 8 bytes of the nonce.
    /// </summary>
    /// <param name="nonce">Nonce of any length >= 12</param>
    /// <returns>The current nonce counter value.</returns>
    private static long GetNonce(this byte[] nonce)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(nonce.AsSpan(nonce.Length - sizeof(long)));
    }

    /// <summary>
    /// AES-GCM encryption requires a unique nonce for each encryption operation.
    /// This method increments the 64-bit little-endian counter stored in the last 8 bytes of the nonce.
    /// The encryptor and decryptor advance this counter in lockstep, so both must use this same helper.
    /// </summary>
    /// <param name="nonce">Nonce of any length >= 12</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nonce"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="nonce"/> length is less than 12 bytes.
    /// </exception>
    internal static void IncreaseNonce(this byte[] nonce)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentOutOfRangeException.ThrowIfLessThan(nonce.Length, 12);

        long value = nonce.GetNonce() + 1;
        BinaryPrimitives.WriteInt64LittleEndian(nonce.AsSpan(nonce.Length - sizeof(long)), value);
    }

    /// <summary>
    /// Decrements the 64-bit little-endian counter stored in the last 8 bytes of the nonce.
    /// Used to derive the reserved seal nonce (initial session nonce counter − 1), which keeps the
    /// seal record decryptable independently of how many data frames precede it. It cannot collide
    /// with a data-frame nonce (initial counter + n) unless a session exceeds 2^64 − 1 frames,
    /// the same bound as the documented counter wrap limit.
    /// </summary>
    /// <param name="nonce">Nonce of any length >= 12</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nonce"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="nonce"/> length is less than 12 bytes.
    /// </exception>
    internal static void DecreaseNonce(this byte[] nonce)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentOutOfRangeException.ThrowIfLessThan(nonce.Length, 12);

        long value = nonce.GetNonce() - 1;
        BinaryPrimitives.WriteInt64LittleEndian(nonce.AsSpan(nonce.Length - sizeof(long)), value);
    }

    /// <summary>
    /// Generates a new RSA key pair for encryption and decryption operations.
    /// </summary>
    /// <param name="keySize">The size of the key in bits. Must be at least 2048. Recommended: 2048 (default) or 4096 for enhanced security.</param>
    /// <param name="format">The format in which to export the keys. Default is XML.</param>
    /// <returns>A tuple containing the public and private keys in XML or PEM format. The public key should be distributed to log producers, while the private key must be kept secure.</returns>
    /// <exception cref="NotSupportedException">Thrown when the key format is not supported.</exception>
    /// <exception cref="CryptographicException">Thrown when key generation fails, or when <paramref name="keySize"/> is less than the 2048-bit minimum.</exception>
    /// <remarks>
    /// <para>
    /// <b>Key Size Recommendations:</b>
    /// - 2048-bit: Standard security, faster encryption/decryption, smaller encrypted headers (default)
    /// - 4096-bit: Enhanced security, slower operations, larger encrypted headers (recommended for highly sensitive data)
    /// </para>
    /// <para>
    /// <b>Key Storage:</b> Store private keys securely using Azure Key Vault, AWS Secrets Manager, or encrypted configuration.
    /// Never commit private keys to source control.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Generate 2048-bit key pair (default)
    /// var (publicKey, privateKey) = CryptographicUtils.GenerateRsaKeyPair();
    ///
    /// // Generate 4096-bit key pair for enhanced security
    /// var (publicKey4k, privateKey4k) = CryptographicUtils.GenerateRsaKeyPair(4096);
    ///
    /// // Store keys securely
    /// File.WriteAllText("public_key.xml", publicKey);
    /// // Use secure storage for private key (Azure Key Vault, etc.)
    /// </code>
    /// </example>
    public static (string publicKey, string privateKey) GenerateRsaKeyPair(
        int keySize = 2048,
        KeyFormat format = KeyFormat.Xml
    )
    {
        // Enforce the documented minimum up front. RSA.Create would happily create a weak
        // (e.g. 1024-bit) key otherwise, contradicting the contract and MinimumRsaKeySize.
        if (keySize < EncryptionConstants.MinimumRsaKeySize)
        {
            throw new CryptographicException(
                $"RSA key size must be at least {EncryptionConstants.MinimumRsaKeySize} bits, but was {keySize}."
            );
        }

        using RSA rsa = RSA.Create(keySize);

        (string publicKey, string privateKey) = format switch
        {
            KeyFormat.Xml => (
                rsa.ToXmlString(includePrivateParameters: false),
                rsa.ToXmlString(includePrivateParameters: true)
            ),
            KeyFormat.Pem => (rsa.ExportRSAPublicKeyPem(), rsa.ExportRSAPrivateKeyPem()),
            _ => throw new NotSupportedException($"Unsupported key format: {format}"),
        };

        return (publicKey, privateKey);
    }
}
