using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

/// <summary>
/// Base class for V1 encryption tests that provides common RSA key setup and helper methods.
/// </summary>
public abstract class V1EncryptionTestBase : IDisposable
{
    protected readonly RSA PublicRsa = RSA.Create();
    protected readonly RSA PrivateRsa = RSA.Create();
    protected readonly string KeyId;
    private bool _disposed;

    protected V1EncryptionTestBase()
    {
        (string publicKey, string privateKey) keyPair = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );
        PublicRsa.FromString(keyPair.publicKey);
        PrivateRsa.FromString(keyPair.privateKey);
        KeyId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Creates encryption options with the default public key and key ID.
    /// </summary>
    protected EncryptionOptions CreateDefaultOptions()
    {
        return new EncryptionOptions(PublicRsa, KeyId);
    }

    /// <summary>
    /// Creates a new session data with random AES key and nonce.
    /// </summary>
    protected static (byte[] aesKey, byte[] nonce) CreateSessionData()
    {
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        return (key, nonce);
    }

    /// <summary>
    /// Decrypts RSA-encrypted data using the private key.
    /// </summary>
    protected byte[] RsaDecrypt(ReadOnlySpan<byte> encryptedData)
    {
        return PrivateRsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Decrypts AES-GCM encrypted data.
    /// </summary>
    protected static byte[] AesGcmDecrypt(
        byte[] ciphertext,
        byte[] key,
        byte[] nonce,
        byte[]? tag = null
    )
    {
        using var aesGcm = new AesGcm(key, EncryptionConstants.TagLength);

        // If tag is provided separately, combine it with ciphertext
        if (tag != null)
        {
            byte[] plaintext = new byte[ciphertext.Length];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }

        // Otherwise, assume tag is appended to ciphertext
        int ciphertextLength = ciphertext.Length - EncryptionConstants.TagLength;
        Span<byte> tagFromData = ciphertext.AsSpan(ciphertextLength, EncryptionConstants.TagLength);
        Span<byte> ciphertextOnly = ciphertext.AsSpan(0, ciphertextLength);
        byte[] decrypted = new byte[ciphertextLength];

        aesGcm.Decrypt(nonce, ciphertextOnly, tagFromData, decrypted);
        return decrypted;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Cleanup
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            PublicRsa.Dispose();
            PrivateRsa.Dispose();
        }
        _disposed = true;
    }
}
