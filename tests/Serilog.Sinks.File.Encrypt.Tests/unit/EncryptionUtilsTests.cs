using System.Diagnostics.CodeAnalysis;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptionUtilsTests : EncryptionTestBase
{
    [Fact]
    public void GenerateRsaKeyPair_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

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
    public async Task DecryptLogFileAsync_ReturnsDecryptedContent()
    {
        // Arrange
        const string originalText = "Hello, simple encrypted log!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(
            originalText,
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(originalText);
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsMultipleDecryptedEntries()
    {
        // Arrange
        const string logMessage1 = "Simple log file test!";
        const string logMessage2 = "Second simple log entry!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(
            [logMessage1, logMessage2],
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(logMessage1);
        decrypted.ShouldContain(logMessage2);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);

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
    public async Task EncryptedStream_With4096BitKey_EncryptsAndDecryptsSuccessfully()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);
        const string originalText = "Testing 4096-bit RSA key with encrypted stream!";

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        rsa.KeySize.ShouldBe(4096); // Verify key size

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            originalText,
            publicKey,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(originalText);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_HandlesMultipleChunks()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);
        const string logMessage1 = "First log entry with 4096-bit key";
        const string logMessage2 = "Second log entry with 4096-bit key";
        const string logMessage3 = "Third log entry with 4096-bit key";

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            [logMessage1, logMessage2, logMessage3],
            publicKey,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldContain(logMessage1);
        decrypted.ShouldContain(logMessage2);
        decrypted.ShouldContain(logMessage3);
    }
}
