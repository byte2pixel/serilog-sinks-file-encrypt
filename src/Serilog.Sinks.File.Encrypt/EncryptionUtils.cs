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
    /// Decrypts and reads the content of an encrypted log file.
    /// </summary>
    /// <param name="encryptedFilePath">The path to the encrypted log file</param>
    /// <param name="rsaPrivateKey">The RSA private key in XML format</param>
    /// <returns>The decrypted content as a string</returns>
    public static string DecryptLogFile(string encryptedFilePath, string rsaPrivateKey)
    {
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(rsaPrivateKey);
        using FileStream fileStream = System.IO.File.OpenRead(encryptedFilePath);

        StringBuilder result = new();
        byte[] chunkMarker = "LOGCHUNK"u8.ToArray();

        try
        {
            while (fileStream.Position < fileStream.Length)
            {
                // Verify chunk marker
                byte[] markerBuffer = new byte[chunkMarker.Length];
                int bytesRead = fileStream.Read(markerBuffer, 0, markerBuffer.Length);
                if (bytesRead != markerBuffer.Length)
                    break; // End of file reached

                if (!markerBuffer.SequenceEqual(chunkMarker))
                {
                    // We didn't find a marker where expected
                    // Move back one byte and try to find the next chunk marker
                    fileStream.Position -= bytesRead - 1;
                    if (!TryFindNextChunk(fileStream, chunkMarker))
                        break; // No more chunks found
                    continue;
                }

                // Read header info
                byte[] keyLengthBytes = new byte[sizeof(int)];
                byte[] ivLengthBytes = new byte[sizeof(int)];

                if (
                    fileStream.Read(keyLengthBytes, 0, keyLengthBytes.Length)
                        != keyLengthBytes.Length
                    || fileStream.Read(ivLengthBytes, 0, ivLengthBytes.Length)
                        != ivLengthBytes.Length
                )
                    break; // End of file reached

                int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
                int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

                // Sanity check - key and IV lengths should be reasonable
                if (keyLength <= 0 || keyLength > 1024 || ivLength <= 0 || ivLength > 1024)
                {
                    // Invalid lengths, try to find next chunk
                    if (!TryFindNextChunk(fileStream, chunkMarker))
                        break;
                    continue;
                }

                // Read encrypted key and IV
                byte[] encryptedKey = new byte[keyLength];
                byte[] encryptedIv = new byte[ivLength];

                if (
                    fileStream.Read(encryptedKey, 0, encryptedKey.Length) != encryptedKey.Length
                    || fileStream.Read(encryptedIv, 0, encryptedIv.Length) != encryptedIv.Length
                )
                    break; // End of file reached

                // Read all data until next chunk marker
                byte[] key;
                byte[] iv;

                try
                {
                    // Decrypt key and IV
                    key = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                    iv = rsa.Decrypt(encryptedIv, RSAEncryptionPadding.OaepSHA256);
                }
                catch (CryptographicException ex)
                {
                    result.AppendLine($"[Error decrypting keys: {ex.Message}]");

                    // Try to find the next chunk
                    if (!TryFindNextChunk(fileStream, chunkMarker))
                        break;
                    continue;
                }

                // Capture all data until the next LOGCHUNK marker or EOF
                MemoryStream encryptedDataMs = new();
                byte[] buffer = new byte[4096];

                // Read until EOF or until we find the next marker
                while (true)
                {
                    // Check if we've hit a marker
                    long currentPosition = fileStream.Position;
                    bytesRead = fileStream.Read(markerBuffer, 0, 1);
                    if (bytesRead == 0)
                        break; // EOF

                    // Check if this could be the start of a marker
                    if (markerBuffer[0] == chunkMarker[0])
                    {
                        // Could be a marker - read the rest to check
                        bytesRead = fileStream.Read(markerBuffer, 1, chunkMarker.Length - 1);
                        if (
                            bytesRead == chunkMarker.Length - 1
                            && markerBuffer.SequenceEqual(chunkMarker)
                        )
                        {
                            // Found a marker - go back to the start of it
                            fileStream.Position = currentPosition;
                            break;
                        }

                        // Not a full marker - rewind and continue
                        fileStream.Position = currentPosition + 1;
                    }

                    // Not the start of a marker - write the byte
                    encryptedDataMs.WriteByte(markerBuffer[0]);

                    if (encryptedDataMs.Length < buffer.Length)
                        continue;

                    // Check if we have a full buffer to process
                    encryptedDataMs.Position = 0;
                    bytesRead = encryptedDataMs.Read(buffer, 0, buffer.Length);
                    encryptedDataMs.SetLength(0);

                    // Check for any marker in the buffer
                    int markerPos = IndexOf(buffer, chunkMarker, bytesRead);
                    if (markerPos >= 0)
                    {
                        // Write data up to the marker
                        encryptedDataMs.Write(buffer, 0, markerPos);

                        // Position file stream at start of marker
                        fileStream.Position = currentPosition - bytesRead + markerPos;
                        break;
                    }

                    // No marker in buffer, write everything
                    encryptedDataMs.Write(buffer, 0, bytesRead);
                }

                // Decrypt the data
                try
                {
                    byte[] encryptedBytes = encryptedDataMs.ToArray();
                    if (encryptedBytes.Length > 0)
                    {
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

                        cryptoStream.Write(encryptedBytes, 0, encryptedBytes.Length);
                        cryptoStream.FlushFinalBlock();

                        string decryptedText = Encoding.UTF8.GetString(memoryStream.ToArray());
                        result.Append(decryptedText);
                    }
                }
                catch (CryptographicException ex)
                {
                    result.AppendLine($"[Error decrypting data: {ex.Message}]");
                }
            }
        }
        catch (Exception ex)
        {
            return $"Decryption error: {ex.Message}";
        }

        return result.ToString();
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int length)
    {
        if (needle.Length == 0 || length < needle.Length)
            return -1;

        for (int i = 0; i <= length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] == needle[j])
                    continue;

                found = false;
                break;
            }
            if (found)
                return i;
        }
        return -1;
    }

    private static bool TryFindNextChunk(Stream stream, byte[] marker)
    {
        // Create a buffer to allow for more efficient reading
        byte[] buffer = new byte[4096];
        int bytesRead;

        // Read the stream in chunks
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Look for marker in the current buffer
            for (int i = 0; i <= bytesRead - marker.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < marker.Length; j++)
                {
                    if (buffer[i + j] == marker[j])
                        continue;

                    found = false;
                    break;
                }

                if (!found)
                    continue;

                // Found the marker - position stream at start of marker
                stream.Position -= (bytesRead - i);
                return true;
            }

            // If no complete marker was found in this buffer, check if the
            // end of buffer might contain the start of a marker
            if (bytesRead >= marker.Length)
            {
                // Position to read the last (marker.Length-1) bytes to check in next iteration
                stream.Position -= (marker.Length - 1);
            }
        }

        return false;
    }

    public static void DecryptLogFileToFile(
        string inputPath,
        string outputPath,
        string rsaPrivateKey
    )
    {
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(rsaPrivateKey);

        using FileStream inputStream = System.IO.File.OpenRead(inputPath);
        using FileStream outputStream = System.IO.File.Create(outputPath);
        using StreamWriter writer = new(outputStream, Encoding.UTF8, leaveOpen: true);

        try
        {
            while (inputStream.Position < inputStream.Length)
            {
                if (!TryReadChunk(inputStream, rsa, out string decryptedText))
                {
                    // If we can't read a chunk, try to find the next valid chunk marker
                    byte[] chunkMarker = "LOGCHUNK"u8.ToArray();
                    if (!TryFindNextChunk(inputStream, chunkMarker))
                        break; // No more valid chunks found
                    continue;
                }

                writer.Write(decryptedText);
                writer.Flush();
            }
        }
        catch (Exception ex)
        {
            writer.WriteLine($"[Decryption error: {ex.Message}]");
        }
    }

    private static bool TryReadChunk(Stream stream, RSA rsaPrivateKey, out string decryptedText)
    {
        decryptedText = string.Empty;

        try
        {
            // Read chunk header
            byte[] marker = new byte[8];
            if (stream.Read(marker, 0, 8) != 8)
                return false;

            if (Encoding.UTF8.GetString(marker) != "LOGCHUNK")
                return false;

            // Read encrypted key/IV lengths and data
            byte[] keyLengthBytes = new byte[4];
            byte[] ivLengthBytes = new byte[4];

            if (stream.Read(keyLengthBytes, 0, 4) != 4 || stream.Read(ivLengthBytes, 0, 4) != 4)
                return false;

            int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
            int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

            // Sanity check - key and IV lengths should be reasonable
            if (keyLength <= 0 || keyLength > 1024 || ivLength <= 0 || ivLength > 1024)
                return false;

            byte[] encryptedKey = new byte[keyLength];
            byte[] encryptedIv = new byte[ivLength];

            if (
                stream.Read(encryptedKey, 0, keyLength) != keyLength
                || stream.Read(encryptedIv, 0, ivLength) != ivLength
            )
                return false;

            // Decrypt AES key/IV
            byte[] key = rsaPrivateKey.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
            byte[] iv = rsaPrivateKey.Decrypt(encryptedIv, RSAEncryptionPadding.OaepSHA256);

            // Read until next chunk marker or end of file using the same logic as DecryptLogFile
            using MemoryStream encryptedDataMs = new();
            byte[] chunkMarker = "LOGCHUNK"u8.ToArray();

            // Read until EOF or until we find the next marker
            ProcessChunk(stream, chunkMarker, encryptedDataMs);

            // Decrypt the data
            byte[] encryptedBytes = encryptedDataMs.ToArray();
            if (encryptedBytes.Length <= 0)
                return true;

            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Padding = PaddingMode.PKCS7;

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            using MemoryStream memoryStream = new();
            using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Write);

            cryptoStream.Write(encryptedBytes, 0, encryptedBytes.Length);
            cryptoStream.FlushFinalBlock();

            decryptedText = Encoding.UTF8.GetString(memoryStream.ToArray());

            return true;
        }
        catch (CryptographicException)
        {
            // Failed to decrypt - return false to stop processing
            return false;
        }
        catch (Exception)
        {
            // Other errors - return false to stop processing
            return false;
        }
    }

    private static void ProcessChunk(Stream stream, byte[] chunkMarker, MemoryStream encryptedDataMs)
    {
        while (true)
        {
            // Check if we've hit a marker
            long currentPosition = stream.Position;
            byte[] markerBuffer = new byte[1];
            int bytesRead = stream.Read(markerBuffer, 0, 1);
            if (bytesRead == 0)
                break; // EOF

            // Check if this could be the start of a marker
            if (markerBuffer[0] == chunkMarker[0])
            {
                // Could be a marker - read the rest to check
                byte[] fullMarkerBuffer = new byte[chunkMarker.Length];
                fullMarkerBuffer[0] = markerBuffer[0];
                bytesRead = stream.Read(fullMarkerBuffer, 1, chunkMarker.Length - 1);
                if (
                    bytesRead == chunkMarker.Length - 1
                    && fullMarkerBuffer.SequenceEqual(chunkMarker)
                )
                {
                    // Found a marker - go back to the start of it
                    stream.Position = currentPosition;
                    break;
                }
                else
                {
                    // Not a full marker - rewind and continue
                    stream.Position = currentPosition + 1;
                    encryptedDataMs.WriteByte(fullMarkerBuffer[0]);
                }
            }
            else
            {
                // Not the start of a marker - write the byte
                encryptedDataMs.WriteByte(markerBuffer[0]);
            }
        }
    }
}
