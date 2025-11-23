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
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted file</param>
    /// <param name="rsaPrivateKey">The XML representation of the RSA private key used for decryption</param>
    /// <returns></returns>
    public static string DecryptLogFile(string encryptedFilePath, string rsaPrivateKey)
    {
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(rsaPrivateKey);
        using FileStream fileStream = System.IO.File.OpenRead(encryptedFilePath);
        List<FileMarker> markers = [];
        StringBuilder result = new();

        // Read through the file to find all markers
        while (fileStream.Position < fileStream.Length)
        {
            byte[] buffer = new byte[FileMarker.MarkerLength];
            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
                break; // End of file reached
            if (buffer.SequenceEqual(FileMarker.LogHeadMarker))
            {
                long markerPosition = fileStream.Position - buffer.Length;

                // Validate this is a real header marker by checking the following data
                if (FileMarker.IsValidHeaderMarker(fileStream, markerPosition))
                {
                    markers.Add(
                        new FileMarker { Type = MarkerType.LogHead, Offset = markerPosition }
                    );
                }
                else
                {
                    // False positive - continue scanning
                    fileStream.Position -= buffer.Length - 1;
                }
            }
            else if (buffer.SequenceEqual(FileMarker.LogBodyMarker))
            {
                markers.Add(
                    new FileMarker
                    {
                        Type = MarkerType.LogBody,
                        Offset = fileStream.Position - buffer.Length,
                    }
                );
                fileStream.Position += buffer.Length;
            }
            else
            {
                // no marker so it is the log message, keep looking for more markers
                fileStream.Position -= buffer.Length - 1;
            }
        }

        byte[] key = [];
        byte[] iv = [];

        // Now decrypt each chunk
        for (int i = 0; i < markers.Count; i++)
        {
            try
            {
                if (markers[i].Type == MarkerType.LogHead)
                {
                    // Skip marker
                    fileStream.Position = markers[i].Offset + FileMarker.MarkerLength;

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
                    key = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                    iv = rsa.Decrypt(encryptedIv, RSAEncryptionPadding.OaepSHA256);
                }
                else
                {
                    // Skip marker
                    fileStream.Position = markers[i].Offset + FileMarker.MarkerLength;

                    // Determine length of encrypted data
                    long dataLength =
                        i + 1 < markers.Count
                            ? markers[i + 1].Offset - fileStream.Position
                            : fileStream.Length - fileStream.Position;

                    string decryptedText = DecryptedText(dataLength, fileStream, key, iv);
                    result.Append(decryptedText);
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"[Decryption error at chunk {i}: {ex.Message}]");
            }
        }
        return result.ToString();
    }

    private static string DecryptedText(
        long dataLength,
        FileStream fileStream,
        byte[] key,
        byte[] iv
    )
    {
        // Check if data length exceeds int.MaxValue (required by ReadExactly)
        if (dataLength > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Log message size ({dataLength} bytes) exceeds maximum supported size ({int.MaxValue} bytes). This indicates a corrupted file or invalid marker positions."
            );
        }

        // Sanity check for unexpectedly large single log messages
        const long maxLogMessageSize = 10_000_000; // 10 MB should be more than enough for any single log message
        if (dataLength > maxLogMessageSize)
        {
            throw new InvalidOperationException(
                $"Log message size ({dataLength} bytes) is unexpectedly large (>{maxLogMessageSize} bytes). This may indicate file corruption."
            );
        }

        int dataSize = (int)dataLength;
        byte[] encryptedData = new byte[dataSize];
        fileStream.ReadExactly(encryptedData, 0, dataSize);

        // Decrypt the data
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using MemoryStream memoryStream = new();
        using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Write);
        cryptoStream.Write(encryptedData, 0, encryptedData.Length);
        cryptoStream.FlushFinalBlock();
        return Encoding.UTF8.GetString(memoryStream.ToArray());
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
