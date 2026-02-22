namespace Serilog.Sinks.File.Encrypt.Tests;

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
    /// Decrypts RSA-encrypted data using the private key.
    /// </summary>
    protected byte[] RsaDecrypt(ReadOnlySpan<byte> encryptedData)
    {
        return PrivateRsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
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
