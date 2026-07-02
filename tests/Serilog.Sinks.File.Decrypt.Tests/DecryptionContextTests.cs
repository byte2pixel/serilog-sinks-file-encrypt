namespace Serilog.Sinks.File.Decrypt.Tests;

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

    [Fact]
    public void Clear_ZeroesSessionKeyAndNonce()
    {
        // Arrange
        byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        byte[] sessionKey = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        var context = new DecryptionContext(nonce, sessionKey);

        // Act
        context.Clear();

        // Assert - key material is wiped in place
        context.SessionKey.ShouldAllBe(b => b == 0);
        context.Nonce.ShouldAllBe(b => b == 0);
    }

    [Fact]
    public void Clear_OnEmptyContext_DoesNotThrow()
    {
        // Arrange
        DecryptionContext context = DecryptionContext.Empty;

        // Act & Assert
        Should.NotThrow(context.Clear);
    }
}
