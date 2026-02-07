using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptionUtilsTests : EncryptionTestBase
{
    [Fact]
    public void GenerateRsaKeyPair_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(format: KeyFormat.Xml);

        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.FromXmlString(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_WithPemFormat_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);

        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.ImportFromPem(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsDecryptedContent()
    {
        // Arrange
        const string OriginalText = "Hello, simple encrypted log!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(
            OriginalText,
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(OriginalText);
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsMultipleDecryptedEntries()
    {
        // Arrange
        const string LogMessage1 = "Simple log file test!";
        const string LogMessage2 = "Second simple log entry!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(
            [LogMessage1, LogMessage2],
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096, format: KeyFormat.Xml);

        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.FromXmlString(publicKey);

        // Assert - Verify key size is 4096 bits
        privateKeyRsa.KeySize.ShouldBe(4096);
        publicKeyRsa.KeySize.ShouldBe(4096);

        // Verify keys can encrypt and decrypt
        byte[] data = "Test data for 4096-bit key"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_WithPemFormat_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096, format: KeyFormat.Pem);

        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.ImportFromPem(publicKey);

        // Assert - Verify key size is 4096 bits
        privateKeyRsa.KeySize.ShouldBe(4096);
        publicKeyRsa.KeySize.ShouldBe(4096);

        // Verify keys can encrypt and decrypt
        byte[] data = "Test data for 4096-bit key"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        decryptedData.ShouldBe(data);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_EncryptsAndDecryptsSuccessfully()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);
        const string OriginalText = "Testing 4096-bit RSA key with encrypted stream!";

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        rsa.KeySize.ShouldBe(4096); // Verify key size

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            OriginalText,
            publicKey,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(OriginalText);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_HandlesMultipleChunks()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);
        const string LogMessage1 = "First log entry with 4096-bit key";
        const string LogMessage2 = "Second log entry with 4096-bit key";
        const string LogMessage3 = "Third log entry with 4096-bit key";

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            [LogMessage1, LogMessage2, LogMessage3],
            publicKey,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
        decrypted.ShouldContain(LogMessage3);
    }
}
