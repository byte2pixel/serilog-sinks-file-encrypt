using System.Security.Cryptography;
using System.Text;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Encrypts log files using RSA for key management and AES for content encryption.
/// The public key is used for encrypting the AES key and IV, which are then stored in the log file header.
/// Only someone with the private RSA key can decrypt the logs.
/// </summary>
public class EncryptHooks : FileLifecycleHooks
{
    private readonly RSA _rsaPublicKey;
    private const int AesKeySize = 256;
    private const int AesBlockSize = 128;

    /// <summary>
    /// Creates a new instance of AsymmetricEncryptHooks using an RSA public key in XML format.
    /// </summary>
    /// <param name="rsaPublicKeyXml">The RSA public key in XML format</param>
    public EncryptHooks(string rsaPublicKeyXml)
    {
        _rsaPublicKey = RSA.Create();
        _rsaPublicKey.FromXmlString(rsaPublicKeyXml);
    }

    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        return CreateEncryptedStream(underlyingStream);
    }

    public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding)
    {
        return CreateEncryptedStream(underlyingStream);
    }

    private CryptoStream CreateEncryptedStream(Stream underlyingStream)
    {
        // Generate random AES key and IV for this log file
        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.BlockSize = AesBlockSize;
        aes.GenerateKey();
        aes.GenerateIV();

        byte[] encryptedKey = _rsaPublicKey.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedIv = _rsaPublicKey.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256);

        byte[] keyLengthBytes = BitConverter.GetBytes(encryptedKey.Length);
        byte[] ivLengthBytes = BitConverter.GetBytes(encryptedIv.Length);

        underlyingStream.Write(keyLengthBytes, 0, keyLengthBytes.Length);
        underlyingStream.Write(ivLengthBytes, 0, ivLengthBytes.Length);
        underlyingStream.Write(encryptedKey, 0, encryptedKey.Length);
        underlyingStream.Write(encryptedIv, 0, encryptedIv.Length);

        // Create encryption stream for the log content
        return new CryptoStream(underlyingStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
    }
}
