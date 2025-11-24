using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Handles the reading and parsing of encrypted log files
/// </summary>
internal sealed class EncryptedFileReader : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly RSA _rsa;
    private readonly StringBuilder _result;
    private DecryptionContext _context;
    private bool _disposed;

    public EncryptedFileReader(string filePath, string rsaPrivateKey)
    {
        _fileStream = System.IO.File.OpenRead(filePath);
        _rsa = RSA.Create();
        _rsa.FromXmlString(rsaPrivateKey);
        _result = new StringBuilder();
        _context = DecryptionContext.Empty;
    }

    /// <summary>
    /// Reads and decrypts the entire file
    /// </summary>
    public string ReadAll()
    {
        while (!IsEndOfFile())
        {
            try
            {
                ProcessNextSection();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        return _result.ToString();
    }

    /// <summary>
    /// Processes the next section in the file (header or body)
    /// </summary>
    private void ProcessNextSection()
    {
        byte[]? markerBuffer = ReadMarkerBuffer();
        if (markerBuffer == null)
            return;

        if (IsHeaderMarker(markerBuffer))
        {
            ProcessHeaderSection(markerBuffer);
        }
        else if (IsBodyMarker(markerBuffer))
        {
            ProcessBodySection();
        }
        else
        {
            SkipUnknownData(markerBuffer);
        }
    }

    /// <summary>
    /// Processes a header section to extract encryption keys
    /// </summary>
    private void ProcessHeaderSection(byte[] markerBuffer)
    {
        long markerPosition = _fileStream.Position - markerBuffer.Length;

        if (!FileMarker.IsValidHeader(_fileStream, markerPosition))
        {
            SkipUnknownData(markerBuffer);
            return;
        }

        HeaderSection header = ReadHeaderSection(markerPosition);
        _context = DecryptKeys(header);
    }

    /// <summary>
    /// Processes a body section to decrypt and append message content
    /// </summary>
    private void ProcessBodySection()
    {
        if (!_context.HasKeys)
            return;

        MessageSection body = ReadMessageSection();
        string decryptedText = DecryptMessageContent(body.MessageLength);
        _result.Append(decryptedText);
    }

    /// <summary>
    /// Reads header section data from the file stream
    /// </summary>
    private HeaderSection ReadHeaderSection(long markerPosition)
    {
        _fileStream.Position = markerPosition + FileMarker.MarkerLength;

        // Read key and IV lengths
        byte[] keyLengthBytes = new byte[4];
        byte[] ivLengthBytes = new byte[4];
        _fileStream.ReadExactly(keyLengthBytes, 0, 4);
        _fileStream.ReadExactly(ivLengthBytes, 0, 4);

        int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

        // Read encrypted key and IV
        byte[] encryptedKey = new byte[keyLength];
        byte[] encryptedIv = new byte[ivLength];
        _fileStream.ReadExactly(encryptedKey, 0, keyLength);
        _fileStream.ReadExactly(encryptedIv, 0, ivLength);

        return new HeaderSection(encryptedKey, encryptedIv);
    }

    /// <summary>
    /// Reads body section metadata from the file stream
    /// </summary>
    private MessageSection ReadMessageSection()
    {
        byte[] lengthBytes = new byte[4];
        _fileStream.ReadExactly(lengthBytes, 0, 4);
        int messageLength = BitConverter.ToInt32(lengthBytes, 0);
        return new MessageSection(messageLength);
    }

    /// <summary>
    /// Decrypts the AES keys from the header section
    /// </summary>
    private DecryptionContext DecryptKeys(HeaderSection header)
    {
        byte[] key = _rsa.Decrypt(header.EncryptedKey, RSAEncryptionPadding.OaepSHA256);
        byte[] iv = _rsa.Decrypt(header.EncryptedIv, RSAEncryptionPadding.OaepSHA256);
        return new DecryptionContext(key, iv);
    }

    /// <summary>
    /// Decrypts message content from the file stream
    /// </summary>
    private string DecryptMessageContent(int dataLength)
    {
        ValidateMessageSize(dataLength);

        byte[] encryptedData = new byte[dataLength];
        _fileStream.ReadExactly(encryptedData, 0, dataLength);

        return DecryptData(encryptedData, _context.Key, _context.Iv);
    }

    /// <summary>
    /// Validates that the message size is reasonable
    /// </summary>
    private static void ValidateMessageSize(int dataLength)
    {
        const int maxLogMessageSize = 10_000_000; // 10 MB should be more than enough for any single log message
        if (dataLength > maxLogMessageSize)
        {
            throw new InvalidOperationException(
                $"Log message size ({dataLength} bytes) is unexpectedly large (>{maxLogMessageSize} bytes). This may indicate file corruption."
            );
        }
    }

    /// <summary>
    /// Decrypts data using AES with the provided key and IV
    /// </summary>
    private static string DecryptData(byte[] encryptedData, byte[] key, byte[] iv)
    {
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

    // Helper methods
    private byte[]? ReadMarkerBuffer()
    {
        byte[] markerBuffer = new byte[FileMarker.MarkerLength];
        int bytesRead = _fileStream.Read(markerBuffer, 0, markerBuffer.Length);
        return bytesRead == markerBuffer.Length ? markerBuffer : null;
    }

    private bool IsEndOfFile() => _fileStream.Position >= _fileStream.Length;

    private static bool IsHeaderMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(FileMarker.Header);

    private static bool IsBodyMarker(byte[] markerBuffer) =>
        markerBuffer.SequenceEqual(FileMarker.Message);

    private void SkipUnknownData(byte[] markerBuffer) =>
        _fileStream.Position -= markerBuffer.Length - 1;

    private void HandleError(Exception ex)
    {
        _result.AppendLine($"[Decryption error at position {_fileStream.Position}: {ex.Message}]");
        TryRecover();
    }

    private void TryRecover()
    {
        if (_fileStream.Position < _fileStream.Length - 1)
            _fileStream.Position++;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _fileStream.Dispose();
            _rsa.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
    }
}
