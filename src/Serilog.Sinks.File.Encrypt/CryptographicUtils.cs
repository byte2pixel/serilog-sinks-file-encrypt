using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Utilities for generating RSA key pairs and decrypting encrypted log files.
/// </summary>
/// <remarks>
/// This class provides static methods for key management and log file decryption.
/// All methods are thread-safe and can be called concurrently.
/// </remarks>
/// <example>
/// <code>
/// // Generate a key pair
/// var (publicKey, privateKey) = CryptographicUtils.GenerateRsaKeyPair(4096);
///
/// // Decrypt a log file
/// using var inputStream = File.OpenRead("encrypted.log");
/// using var outputStream = File.Create("decrypted.log");
///
/// await CryptographicUtils.DecryptLogFileAsync(
///     inputStream,
///     outputStream,
///     privateKey,
///     cancellationToken: cancellationToken
/// );
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
    /// This method retrieves the current nonce value stored in the last 8 bytes of the data array.
    /// </summary>
    /// <param name="nonce">Nonce of any length >= 12</param>
    /// <returns>The current nonce counter value.</returns>
    private static long GetNonce(this byte[] nonce)
    {
        return BitConverter.ToInt64(nonce, nonce.Length - sizeof(long));
    }

    /// <summary>
    /// AES-GCM encryption requires a unique nonce for each encryption operation.
    /// This method increments the nonce value stored in the last 12 bytes of the data array.
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

        long value = nonce.GetNonce() + (1 % long.MaxValue);
        byte[] nonceBytes = BitConverter.GetBytes(value);
        nonceBytes.CopyTo(nonce, nonce.Length - sizeof(long));
    }

    /// <summary>
    /// Generates a new RSA key pair for encryption and decryption operations.
    /// </summary>
    /// <param name="keySize">The size of the key in bits. Must be at least 2048. Recommended: 2048 (default) or 4096 for enhanced security.</param>
    /// <param name="format">The format in which to export the keys. Default is XML.</param>
    /// <returns>A tuple containing the public and private keys in XML or PEM format. The public key should be distributed to log producers, while the private key must be kept secure.</returns>
    /// <exception cref="NotSupportedException">Thrown when the key format is not supported.</exception>
    /// <exception cref="CryptographicException">Thrown when key generation fails or the key size is invalid.</exception>
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

    /// <summary>
    /// Decrypts an encrypted log file from an input stream and writes the decrypted content to an output stream.
    /// </summary>
    /// <param name="inputStream">The input stream to decrypt.</param>
    /// <param name="outputStream">The output stream for plaintext.</param>
    /// <param name="options">Decryption options.</param>
    /// <param name="logger">Optional audit logger for decryption errors. If provided, decryption errors will be logged with details about the error and the position in the stream where it occurred.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>
    /// A <see cref="DecryptionResult"/> containing counts of decrypted sessions, messages, failures, and resync attempts.
    /// </returns>
    public static async Task<DecryptionResult> DecryptLogFileAsync(
        Stream inputStream,
        Stream outputStream,
        DecryptionOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        using LogReader reader = new(inputStream, options, logger);
        return await reader.DecryptToStreamAsync(outputStream, cancellationToken);
    }

    /// <summary>
    /// Decrypts an encrypted log file and writes the decrypted content to a new file.
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted log file. File must exist and be readable.</param>
    /// <param name="outputFilePath">Path where the decrypted content will be written. Will be created or overwritten.</param>
    /// <param name="options">Streaming options for the decryption process.</param>
    /// <param name="logger">Optional audit logger for decryption errors. If provided, decryption errors will be logged with details about the error and the position in the file where it occurred.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>A task representing the asynchronous decryption operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encryptedFilePath"/> or <paramref name="outputFilePath"/> cannot be opened or created.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any string parameter is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="encryptedFilePath"/> does not exist.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails due to wrong key, corrupted data, or invalid format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the input file does not contain valid encryption markers.</exception>
    /// <exception cref="IOException">Thrown when file access fails due to permissions or locking issues.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This is a convenience overload that automatically manages file streams. For more control over stream handling,
    /// use the <see cref="DecryptLogFileAsync(Stream, Stream, DecryptionOptions, ILogger?, CancellationToken)"/> overload.
    /// </remarks>
    public static async Task<DecryptionResult> DecryptLogFileAsync(
        string encryptedFilePath,
        string outputFilePath,
        DecryptionOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFilePath);
        await using FileStream outputStream = System.IO.File.Create(outputFilePath);

        return await DecryptLogFileAsync(
            inputStream,
            outputStream,
            options,
            logger,
            cancellationToken
        );
    }
}
