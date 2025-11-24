using System.Security.Cryptography;

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
    /// This method decrypts the log files in which each log message is encrypted separately.
    /// Each log entry is encrypted with its own AES key and IV, which are then encrypted with RSA.
    /// This allows for secure storage of log files while still enabling decryption of individual log entries.
    /// Format: [HEADER][key_len][iv_len][encrypted_key][encrypted_iv][BODY_MARKER][msg_len][encrypted_message]
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted file</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption</param>
    /// <returns>Decrypted log content</returns>
    public static string DecryptLogFile(string encryptedFilePath, string rsaPrivateKey)
    {
        using EncryptedFileReader reader = new(encryptedFilePath, rsaPrivateKey);
        return reader.ReadAll();
    }

    /// <summary>
    /// Decrypts an encrypted log file and writes the decrypted content to an output file
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted file</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption</param>
    /// <param name="outputFilePath">Path where the decrypted content will be written</param>
    public static void DecryptLogFileToFile(
        string encryptedFilePath,
        string rsaPrivateKey,
        string outputFilePath
    )
    {
        string decryptedContent = DecryptLogFile(encryptedFilePath, rsaPrivateKey);
        System.IO.File.WriteAllText(outputFilePath, decryptedContent);
    }
}
