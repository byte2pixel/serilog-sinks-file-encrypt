namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptHooksTests
{
    [Fact]
    public void GivenCorrectOptions_WhenCreatingEncryptHooks_ThenNoExceptionIsThrown()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair();

        // Act & Assert
        Should.NotThrow(() => new EncryptHooks(publicKey));
    }

    [Fact]
    public void GivenInvalidPublicKey_WhenCreatingEncryptHooks_ThenArgumentExceptionIsThrown()
    {
        // Arrange
        const string InvalidPublicKey = "This is not a valid RSA public key";
        // Act & Assert
        Should.Throw<CryptographicException>(() => new EncryptHooks(InvalidPublicKey));
    }

    [Fact]
    public void GivenKeyToSmall_WhenCreatingEncryptHooks_ThenCryptographicExceptionIsThrown()
    {
        // Arrange
        using var rsa = RSA.Create(1024);
        string publicKey = rsa.ToXmlString(false);
        // Act & Assert
        Should.Throw<CryptographicException>(() => new EncryptHooks(publicKey));
    }

    [Fact]
    public void OnFileOpened_ReturnsEncryptedStream()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair();
        EncryptHooks hooks = new(publicKey);
        using MemoryStream memoryStream = new();
        Encoding encoding = Encoding.UTF8;

        // Act
        using Stream resultStream = hooks.OnFileOpened("test.log", memoryStream, encoding);

        // Assert
        Assert.IsType<LogWriter>(resultStream);
    }
}
