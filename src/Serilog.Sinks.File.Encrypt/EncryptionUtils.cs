using System.Security.Cryptography;
using System.Text;

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
    /// Each message is prefixed with a marker "LOGCHUNK" followed by the lengths and encrypted AES key and IV.
    /// </summary>
    /// <param name="encryptedFilePath"></param>
    /// <param name="rsaPrivateKey"></param>
    /// <returns></returns>
    public static string DecryptLogFile(string encryptedFilePath, string rsaPrivateKey)
    {
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(rsaPrivateKey);
        using FileStream fileStream = System.IO.File.OpenRead(encryptedFilePath);
        List<int> chunkOffsets = [];
        StringBuilder result = new();
        byte[] chunkMarker = "LOGCHUNK"u8.ToArray();
        // Read through the file to find all chunk offsets
        while (fileStream.Position < fileStream.Length)
        {
            byte[] buffer = new byte[chunkMarker.Length];
            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
                break; // End of file reached
            if (buffer.SequenceEqual(chunkMarker))
            {
                chunkOffsets.Add((int)fileStream.Position - buffer.Length);
            }
        }

        // Now decrypt each chunk
        for (int i = 0; i < chunkOffsets.Count; i++)
        {
            try
            {
                fileStream.Position = chunkOffsets[i];

                // Read chunk header
                byte[] marker = new byte[8];
                fileStream.ReadExactly(marker, 0, 8);

                // Read encrypted key/IV lengths and data
                byte[] keyLengthBytes = new byte[4];
                byte[] ivLengthBytes = new byte[4];
                fileStream.ReadExactly(keyLengthBytes, 0, 4);
                fileStream.ReadExactly(ivLengthBytes, 0, 4);
                int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
                int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);
                byte[] encryptedKey = new byte[keyLength];
                byte[] encryptedIv = new byte[ivLength];
                fileStream.ReadExactly(encryptedKey, 0, keyLength);
                fileStream.ReadExactly(encryptedIv, 0, ivLength);

                // Decrypt AES key/IV
                byte[] key = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                byte[] iv = rsa.Decrypt(encryptedIv, RSAEncryptionPadding.OaepSHA256);

                // Read encrypted data
                int dataLength =
                    (i + 1 < chunkOffsets.Count)
                        ? chunkOffsets[i + 1] - (int)fileStream.Position
                        : (int)(fileStream.Length - fileStream.Position);

                byte[] encryptedData = new byte[dataLength];
                fileStream.ReadExactly(encryptedData, 0, dataLength);

                // Decrypt the data
                using Aes aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7;
                using ICryptoTransform decryptor = aes.CreateDecryptor();
                using MemoryStream memoryStream = new();
                using CryptoStream cryptoStream = new(
                    memoryStream,
                    decryptor,
                    CryptoStreamMode.Write
                );
                cryptoStream.Write(encryptedData, 0, encryptedData.Length);
                cryptoStream.FlushFinalBlock();
                string decryptedText = Encoding.UTF8.GetString(memoryStream.ToArray());
                result.Append(decryptedText);
            }
            catch (Exception ex)
            {
                result.AppendLine($"[Decryption error at chunk {i}: {ex.Message}]");
            }
        }
        return result.ToString();
    }

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
