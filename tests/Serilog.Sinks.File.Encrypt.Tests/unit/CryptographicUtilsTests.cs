using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class CryptographicUtilsTests : EncryptionTestBase
{
    [Fact]
    public void InitializeRsa_FromXml_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );

        // Assert
        Should.NotThrow(() =>
        {
            using var privateKeyRsa = RSA.Create();
            privateKeyRsa.FromString(privateKey);
        });

        Should.NotThrow(() =>
        {
            using var publicKeyRsa = RSA.Create();
            publicKeyRsa.FromString(publicKey);
        });
    }

    [Fact]
    public void InitializeRsa_FromPem_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Pem
        );

        Should.NotThrow(() =>
        {
            // Assert
            using var privateKeyRsa = RSA.Create();
            privateKeyRsa.FromString(privateKey);
        });

        Should.NotThrow(() =>
        {
            using var publicKeyRsa = RSA.Create();
            publicKeyRsa.FromString(publicKey);
        });
    }

    [Fact]
    public void InitializeRsa_FromUnknown_ThrowsCryptographicException()
    {
        // Arrange, Act
        const string UnknownFormat = "UNKOWN_FORMAT";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(UnknownFormat);
        });
    }

    [Fact]
    public void InitializeRsa_FromInvalidXml_ThrowsFormatException()
    {
        // Arrange, Act
        const string InvalidXml =
            "<RSAKeyValue><Modulus>...</Modulus><Exponent>...</Exponent></RSAKeyValue>";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(InvalidXml);
        });
    }

    [Fact]
    public void InitializeRsa_FromInvalidPem_ThrowsArgumentException()
    {
        // Arrange, Act
        const string InvalidPem =
            "-----BEGIN RSA PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7\n-----END RSA PUBLIC KEY-----";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(InvalidPem);
        });
    }

    [Fact]
    public void GenerateRsaKeyPair_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using var publicKeyRsa = RSA.Create();
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
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Pem
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.ImportFromPem(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096,
            format: KeyFormat.Xml
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using var publicKeyRsa = RSA.Create();
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
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096,
            format: KeyFormat.Pem
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using var publicKeyRsa = RSA.Create();
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
    public void GenerateRsaKeyPair_WithInvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        Should
            .Throw<NotSupportedException>(() =>
                CryptographicUtils.GenerateRsaKeyPair(format: (KeyFormat)999)
            )
            .Message.ShouldContain("Unsupported key format: 999");
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsDecryptedContent()
    {
        // Arrange
        const string OriginalText = "Hello, simple encrypted log!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(OriginalText);

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
        string decrypted = await EncryptAndDecryptAsync([LogMessage1, LogMessage2]);

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_EncryptsAndDecryptsSuccessfully()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair(keySize: 4096);
        const string OriginalText = "Testing 4096-bit RSA key with encrypted stream!";

        using var rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(OriginalText);

        // Assert
        decrypted.ShouldBe(OriginalText);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_HandlesMultipleChunks()
    {
        // Arrange
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096
        );
        using var rsa = RSA.Create();
        rsa.FromString(publicKey);
        EncryptionOptions options = new(rsa, "4096");
        var map = new Dictionary<string, string> { { options.KeyId, privateKey } };
        DecryptionOptions decryptionOptions = new() { DecryptionKeys = map };
        const string LogMessage1 = "First log entry with 4096-bit key";
        const string LogMessage2 = "Second log entry with 4096-bit key";
        const string LogMessage3 = "Third log entry with 4096-bit key";

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            [LogMessage1, LogMessage2, LogMessage3],
            options,
            decryptionOptions
        );

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
        decrypted.ShouldContain(LogMessage3);
    }
}
