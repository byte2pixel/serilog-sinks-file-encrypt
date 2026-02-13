namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class RsaEncryptionHelperTests
{
    [Theory]
    [InlineData(2048, 190)]
    [InlineData(4096, 446)]
    public void GetMaxPlaintextSize_WithOaepSHA256_ReturnsCorrectSize(
        int keySize,
        int expectedMaxSize
    )
    {
        // Act
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            keySize,
            RSAEncryptionPadding.OaepSHA256
        );

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Theory]
    [InlineData(2048, 214)]
    [InlineData(4096, 470)]
    public void GetMaxPlaintextSize_WithOaepSHA1_ReturnsCorrectSize(
        int keySize,
        int expectedMaxSize
    )
    {
        // Act
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            keySize,
            RSAEncryptionPadding.OaepSHA1
        );

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Theory]
    [InlineData(2048, 158)]
    [InlineData(4096, 414)]
    public void GetMaxPlaintextSize_WithOaepSHA384_ReturnsCorrectSize(
        int keySize,
        int expectedMaxSize
    )
    {
        // Act
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            keySize,
            RSAEncryptionPadding.OaepSHA384
        );

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Theory]
    [InlineData(2048, 126)]
    [InlineData(4096, 382)]
    public void GetMaxPlaintextSize_WithOaepSHA512_ReturnsCorrectSize(
        int keySize,
        int expectedMaxSize
    )
    {
        // Act
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            keySize,
            RSAEncryptionPadding.OaepSHA512
        );

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Theory]
    [InlineData(2048, 245)]
    [InlineData(4096, 501)]
    public void GetMaxPlaintextSize_WithPkcs1_ReturnsCorrectSize(int keySize, int expectedMaxSize)
    {
        // Act
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(keySize, RSAEncryptionPadding.Pkcs1);

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Fact]
    public void ValidatePayloadSize_WithValidSize_DoesNotThrow()
    {
        // Arrange
        const int KeySize = 2048;
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            KeySize,
            RSAEncryptionPadding.OaepSHA256
        );

        // Act & Assert
        Should.NotThrow(() =>
            RsaEncryptionHelper.ValidatePayloadSize(
                maxSize,
                KeySize,
                RSAEncryptionPadding.OaepSHA256
            )
        );
    }

    [Fact]
    public void ValidatePayloadSize_WithOversizedPayload_ThrowsArgumentException()
    {
        // Arrange
        const int KeySize = 2048;
        int maxSize = RsaEncryptionHelper.GetMaxPlaintextSize(
            KeySize,
            RSAEncryptionPadding.OaepSHA256
        );
        int oversizedPayload = maxSize + 1;

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            RsaEncryptionHelper.ValidatePayloadSize(
                oversizedPayload,
                KeySize,
                RSAEncryptionPadding.OaepSHA256
            )
        );

        ex.Message.ShouldContain($"Payload size ({oversizedPayload} bytes)");
        ex.Message.ShouldContain($"maximum size ({maxSize} bytes)");
        ex.Message.ShouldContain("RSA-2048");
    }
}
