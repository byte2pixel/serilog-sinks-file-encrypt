using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

/// <summary>
/// Base class for V1 encryption tests that provides common RSA key setup and helper methods.
/// </summary>
public abstract class V1EncryptionTestBase : IDisposable
{
    protected readonly RSA PublicKey = RSA.Create();
    protected readonly RSA PrivateKey = RSA.Create();
    protected readonly string KeyId;
    private readonly (string publicKey, string privateKey) _keyPair;

    protected V1EncryptionTestBase()
    {
        _keyPair = EncryptionUtils.GenerateRsaKeyPair(format: KeyFormat.Xml);
        PublicKey.FromString(_keyPair.publicKey);
        PrivateKey.FromString(_keyPair.privateKey);
        KeyId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Creates encryption options with the default public key and key ID.
    /// </summary>
    protected EncryptionOptions CreateDefaultOptions()
    {
        return new EncryptionOptions(PublicKey, KeyId);
    }

    /// <summary>
    /// Creates encryption options with a custom key ID.
    /// </summary>
    protected EncryptionOptions CreateOptionsWithKeyId(string? keyId)
    {
        return new EncryptionOptions(PublicKey, keyId);
    }

    /// <summary>
    /// Creates encryption options with a custom RSA key and key ID.
    /// </summary>
    protected EncryptionOptions CreateOptionsWithCustomKey(RSA publicKey, string? keyId)
    {
        return new EncryptionOptions(publicKey, keyId);
    }

    /// <summary>
    /// Creates a new session data with random AES key and nonce.
    /// </summary>
    protected SessionData CreateSessionData(string plaintext = "Test")
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength),
            Nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength),
            Plaintext = new ReadOnlyMemory<byte>(plaintextBytes),
        };
    }

    /// <summary>
    /// Decrypts RSA-encrypted data using the private key.
    /// </summary>
    protected byte[] RsaDecrypt(byte[] encryptedData)
    {
        return PrivateKey.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Encrypts data using the public key.
    /// </summary>
    protected byte[] RsaEncrypt(byte[] data)
    {
        return PublicKey.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Decrypts AES-GCM encrypted data.
    /// </summary>
    protected byte[] AesGcmDecrypt(byte[] ciphertext, byte[] key, byte[] nonce, byte[]? tag = null)
    {
        using var aesGcm = new AesGcm(key, EncryptionConstants.TagLength);

        // If tag is provided separately, combine it with ciphertext
        if (tag != null)
        {
            var plaintext = new byte[ciphertext.Length];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }

        // Otherwise, assume tag is appended to ciphertext
        var ciphertextLength = ciphertext.Length - EncryptionConstants.TagLength;
        var tagFromData = ciphertext.AsSpan(ciphertextLength, EncryptionConstants.TagLength);
        var ciphertextOnly = ciphertext.AsSpan(0, ciphertextLength);
        var decrypted = new byte[ciphertextLength];

        aesGcm.Decrypt(nonce, ciphertextOnly, tagFromData, decrypted);
        return decrypted;
    }

    /// <summary>
    /// Encrypts data using AES-GCM and returns ciphertext with tag appended.
    /// </summary>
    protected byte[] AesGcmEncrypt(byte[] plaintext, byte[] key, byte[] nonce)
    {
        using var aesGcm = new AesGcm(key, EncryptionConstants.TagLength);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[EncryptionConstants.TagLength];

        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Return ciphertext with tag appended
        var result = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);

        return result;
    }

    /// <summary>
    /// Validates that two byte arrays are equal.
    /// </summary>
    protected static void AssertBytesEqual(byte[] expected, byte[] actual, string? message = null)
    {
        actual.ShouldBe(expected, message);
    }

    /// <summary>
    /// Validates that a timestamp matches (comparing as Unix milliseconds to avoid precision issues).
    /// </summary>
    protected static void AssertTimestampEqual(DateTimeOffset expected, DateTimeOffset actual)
    {
        actual.ToUnixTimeMilliseconds().ShouldBe(expected.ToUnixTimeMilliseconds());
    }

    public void Dispose()
    {
        PublicKey?.Dispose();
        PrivateKey?.Dispose();
        GC.SuppressFinalize(this);
    }
}
