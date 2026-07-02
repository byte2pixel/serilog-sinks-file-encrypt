namespace Serilog.Sinks.File.Encrypt.Tests;

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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenEmptyOrWhitespacePublicKey_WhenCreatingEncryptHooks_ThenArgumentExceptionIsThrown(
        string publicKey
    )
    {
        // Act & Assert
        ArgumentException ex = Should.Throw<ArgumentException>(() => new EncryptHooks(publicKey));
        ex.GetType().ShouldBe(typeof(ArgumentException));
    }

    [Fact]
    public void GivenNullPublicKey_WhenCreatingEncryptHooks_ThenArgumentNullExceptionIsThrown()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EncryptHooks(null!));
    }

    [Fact]
    public void GivenNullKeyId_WhenCreatingEncryptHooks_ThenArgumentNullExceptionIsThrown()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EncryptHooks(publicKey, null!));
    }

    [Fact]
    public void GivenKeyIdLongerThan32Bytes_WhenCreatingEncryptHooks_ThenArgumentExceptionIsThrown()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair();
        string tooLongKeyId = new('a', 33);

        // Act & Assert - fails at construction, not on the first log write
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            new EncryptHooks(publicKey, tooLongKeyId)
        );
        ex.ParamName.ShouldBe("keyId");
    }

    [Fact]
    public void GivenKeyIdExactly32Bytes_WhenCreatingEncryptHooks_ThenNoExceptionIsThrown()
    {
        // Arrange
        (string publicKey, string _) = CryptographicUtils.GenerateRsaKeyPair();
        string maxKeyId = new('a', 32);

        // Act & Assert
        Should.NotThrow(() => new EncryptHooks(publicKey, maxKeyId));
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
