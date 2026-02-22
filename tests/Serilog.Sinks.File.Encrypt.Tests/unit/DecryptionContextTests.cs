namespace Serilog.Sinks.File.Encrypt.Tests;

public class DecryptionContextTests
{
    [Fact]
    public void DecryptionContext_Should_Have_Properties_Set()
    {
        // Arrange
        byte[] nonce = "testkey"u8.ToArray();
        byte[] sessionKey = "sessionkey"u8.ToArray();

        // Act
        var context = new DecryptionContext(nonce, sessionKey);

        // Assert
        context.HasKeys.ShouldBeTrue();
        context.Nonce.ShouldBe(nonce);
        context.SessionKey.ShouldBe(sessionKey);
    }

    [Fact]
    public void DecryptionContext_Empty_HasKeys_False()
    {
        // Act
        DecryptionContext context = DecryptionContext.Empty;

        // Assert
        context.HasKeys.ShouldBeFalse();
        context.Nonce.ShouldBe([]);
        context.SessionKey.ShouldBe([]);
    }
}
