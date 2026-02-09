using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class CryptographicUtilsTests : EncryptionTestBase
{
    [Fact]
    public void InitializeRsa_FromXml_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(format: KeyFormat.Xml);

        // Assert
        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.FromString(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.FromString(publicKey);
    }

    [Fact]
    public void InitializeRsa_FromPem_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);

        // Assert
        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.FromString(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.FromString(publicKey);
    }

    [Fact]
    public void InitializeRsa_FromUnknown_ThrowsCryptographicException()
    {
        // Arrange, Act
        string unknownKeyFormat = "UNKOWN_FORMAT";

        // Assert
        using RSA rsa = RSA.Create();
        Assert.Throws<CryptographicException>(() => rsa.FromString(unknownKeyFormat));
    }

    [Fact]
    public void InitializeRsa_FromInvalidXml_ThrowsFormatException()
    {
        // Arrange, Act
        string invalidXml = "<RSAKeyValue><Modulus>...</Modulus><Exponent>...</Exponent></RSAKeyValue>";

        // Assert
        using RSA rsa = RSA.Create();
        Assert.Throws<FormatException>(() => rsa.FromString(invalidXml));
    }

    [Fact]
    public void InitializeRsa_FromInvalidPem_ThrowsArgumentException()
    {
        // Arrange, Act
        string invalidPem = "-----BEGIN RSA PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7\n-----END RSA PUBLIC KEY-----";

        // Assert
        using RSA rsa = RSA.Create();
        Assert.Throws<ArgumentException>(() => rsa.FromString(invalidPem));
    }
}
