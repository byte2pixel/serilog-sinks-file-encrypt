namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptHooksTests
{
    [Fact]
    public void OnFileOpened_ReturnsEncryptedStream()
    {
        // Arrange
        (string publicKey, string _) = EncryptionUtils.GenerateRsaKeyPair();
        EncryptHooks hooks = new(publicKey);
        using MemoryStream memoryStream = new();
        Encoding encoding = Encoding.UTF8;

        // Act
        using Stream resultStream = hooks.OnFileOpened("test.log", memoryStream, encoding);

        // Assert
        Assert.IsType<EncryptedStream>(resultStream);
    }
}
