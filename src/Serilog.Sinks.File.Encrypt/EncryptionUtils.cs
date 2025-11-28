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
/// var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair(4096);
///
/// // Decrypt a log file
/// using var inputStream = File.OpenRead("encrypted.log");
/// using var outputStream = File.Create("decrypted.log");
///
/// await EncryptionUtils.DecryptLogFileAsync(
///     inputStream,
///     outputStream,
///     privateKey,
///     cancellationToken: cancellationToken
/// );
/// </code>
/// </example>
public static class EncryptionUtils
{
    /// <summary>
    /// Generates a new RSA key pair for encryption and decryption operations.
    /// </summary>
    /// <param name="keySize">The size of the key in bits. Must be at least 2048. Recommended: 2048 (default) or 4096 for enhanced security.</param>
    /// <returns>A tuple containing the public and private keys in XML format. The public key should be distributed to log producers, while the private key must be kept secure.</returns>
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
    /// var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair();
    ///
    /// // Generate 4096-bit key pair for enhanced security
    /// var (publicKey4k, privateKey4k) = EncryptionUtils.GenerateRsaKeyPair(4096);
    ///
    /// // Store keys securely
    /// File.WriteAllText("public_key.xml", publicKey);
    /// // Use secure storage for private key (Azure Key Vault, etc.)
    /// </code>
    /// </example>
    public static (string publicKey, string privateKey) GenerateRsaKeyPair(int keySize = 2048)
    {
        using RSA rsa = RSA.Create(keySize);
        string publicKey = rsa.ToXmlString(includePrivateParameters: false);
        string privateKey = rsa.ToXmlString(includePrivateParameters: true);
        return (publicKey, privateKey);
    }

    /// <summary>
    /// Decrypts an encrypted log file asynchronously using streaming for efficient memory usage.
    /// </summary>
    /// <param name="inputStream">Stream containing the encrypted log data. Must be readable and seekable.</param>
    /// <param name="outputStream">Stream where the decrypted content will be written. Must be writable.</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption. Must match the public key used for encryption.</param>
    /// <param name="options">Streaming options for the decryption process. If null, uses <see cref="StreamingOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>A task representing the asynchronous decryption operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputStream"/>, <paramref name="outputStream"/>, or <paramref name="rsaPrivateKey"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails due to wrong key, corrupted data, or invalid format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the input stream does not contain valid encryption markers.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Memory Usage:</b> Uses a producer-consumer pattern with bounded channels to limit memory usage.
    /// Memory consumption is controlled by <see cref="StreamingOptions.BufferSize"/> and <see cref="StreamingOptions.QueueDepth"/>.
    /// Typical memory usage: BufferSize × QueueDepth (e.g., 16KB × 10 = 160KB).
    /// </para>
    /// <para>
    /// <b>Error Handling:</b> Behavior depends on <see cref="StreamingOptions.ErrorHandlingMode"/>:
    /// - <see cref="ErrorHandlingMode.Skip"/>: Silently skips corrupted sections (safest for structured logs)
    /// - <see cref="ErrorHandlingMode.WriteInline"/>: Writes error messages to output stream
    /// - <see cref="ErrorHandlingMode.WriteToErrorLog"/>: Logs errors to separate file
    /// - <see cref="ErrorHandlingMode.ThrowException"/>: Throws exception on first error
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic decryption with default options
    /// using var inputStream = File.OpenRead("encrypted.log");
    /// using var outputStream = File.Create("decrypted.log");
    ///
    /// await EncryptionUtils.DecryptLogFileAsync(
    ///     inputStream,
    ///     outputStream,
    ///     privateKeyXml,
    ///     cancellationToken: cts.Token
    /// );
    ///
    /// // Decryption with custom error handling
    /// var options = new StreamingOptions
    /// {
    ///     ContinueOnError = true,
    ///     ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
    ///     ErrorLogPath = "decryption_errors.log",
    ///     BufferSize = 32 * 1024, // 32KB chunks
    ///     QueueDepth = 20 // Allow more buffering
    /// };
    ///
    /// await EncryptionUtils.DecryptLogFileAsync(
    ///     inputStream,
    ///     outputStream,
    ///     privateKeyXml,
    ///     options,
    ///     cts.Token
    /// );
    /// </code>
    /// </example>
    public static async Task DecryptLogFileAsync(
        Stream inputStream,
        Stream outputStream,
        string rsaPrivateKey,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= StreamingOptions.Default;

        await using StreamingEncryptedFileReader reader = new(inputStream, rsaPrivateKey, options);
        await reader.DecryptToStreamAsync(outputStream, cancellationToken);
    }

    /// <summary>
    /// Decrypts an encrypted log file and writes the decrypted content to a new file.
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted log file. File must exist and be readable.</param>
    /// <param name="outputFilePath">Path where the decrypted content will be written. Will be created or overwritten.</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption. Must match the public key used for encryption.</param>
    /// <param name="options">Streaming options for the decryption process. If null, uses <see cref="StreamingOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>A task representing the asynchronous decryption operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any string parameter is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="encryptedFilePath"/> does not exist.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails due to wrong key, corrupted data, or invalid format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the input file does not contain valid encryption markers.</exception>
    /// <exception cref="IOException">Thrown when file access fails due to permissions or locking issues.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This is a convenience overload that automatically manages file streams. For more control over stream handling,
    /// use the <see cref="DecryptLogFileAsync(Stream, Stream, string, StreamingOptions?, CancellationToken)"/> overload.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Decrypt a file with default options
    /// await EncryptionUtils.DecryptLogFileAsync(
    ///     "logs/encrypted.log",
    ///     "logs/decrypted.log",
    ///     privateKeyXml
    /// );
    ///
    /// // Decrypt with error logging
    /// var options = new StreamingOptions
    /// {
    ///     ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
    ///     ErrorLogPath = "logs/errors.log"
    /// };
    ///
    /// await EncryptionUtils.DecryptLogFileAsync(
    ///     "logs/encrypted.log",
    ///     "logs/decrypted.log",
    ///     privateKeyXml,
    ///     options,
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task DecryptLogFileAsync(
        string encryptedFilePath,
        string outputFilePath,
        string rsaPrivateKey,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFilePath);
        await using FileStream outputStream = System.IO.File.Create(outputFilePath);

        await DecryptLogFileAsync(
            inputStream,
            outputStream,
            rsaPrivateKey,
            options,
            cancellationToken
        );
    }
}
