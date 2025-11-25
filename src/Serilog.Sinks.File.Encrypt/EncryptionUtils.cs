using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Utilities for generating RSA key pairs and decrypting encrypted log files.
/// </summary>
public static class EncryptionUtils
{
    /// <summary>
    /// Generates a new RSA key pair.
    /// </summary>
    /// <param name="keySize">The size of the key in bits (default 2048)</param>
    /// <returns>A tuple containing the public and private keys in XML format</returns>
    public static (string publicKey, string privateKey) GenerateRsaKeyPair(int keySize = 2048)
    {
        using RSA rsa = RSA.Create(keySize);
        string publicKey = rsa.ToXmlString(includePrivateParameters: false);
        string privateKey = rsa.ToXmlString(includePrivateParameters: true);
        return (publicKey, privateKey);
    }

    /// <summary>
    /// Decrypts an encrypted log file asynchronously and writes the decrypted content to an output stream
    /// </summary>
    /// <param name="inputStream">Stream containing the encrypted log data</param>
    /// <param name="outputStream">Stream where the decrypted content will be written</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption</param>
    /// <param name="options">Streaming options for the decryption process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task DecryptLogFileAsync(
        Stream inputStream,
        Stream outputStream,
        string rsaPrivateKey,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= StreamingOptions.Default;

        using StreamingEncryptedFileReader reader = new StreamingEncryptedFileReader(
            inputStream,
            rsaPrivateKey,
            options
        );
        await reader.DecryptToStreamAsync(outputStream, cancellationToken);
    }

    /// <summary>
    /// Decrypts an encrypted log file asynchronously and writes the decrypted content to an output file
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted file</param>
    /// <param name="outputFilePath">Path where the decrypted content will be written</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption</param>
    /// <param name="options">Streaming options for the decryption process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task DecryptLogFileToFileAsync(
        string encryptedFilePath,
        string outputFilePath,
        string rsaPrivateKey,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        using FileStream inputStream = System.IO.File.OpenRead(encryptedFilePath);
        using FileStream outputStream = System.IO.File.Create(outputFilePath);

        await DecryptLogFileAsync(
            inputStream,
            outputStream,
            rsaPrivateKey,
            options,
            cancellationToken
        );
    }
}
