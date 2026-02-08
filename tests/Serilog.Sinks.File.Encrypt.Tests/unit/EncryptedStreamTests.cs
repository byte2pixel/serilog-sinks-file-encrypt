namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptedStreamTests
{
    [Fact]
    public void StreamContract_PropertiesAndUnsupportedMethods_ThrowOrReturnExpected()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);

        // Act & Assert
        Assert.False(encStream.CanRead);
        Assert.False(encStream.CanSeek);
        Assert.True(encStream.CanWrite);

        Assert.Throws<NotSupportedException>(() => encStream.Length.ShouldBe(0));
        Assert.Throws<NotSupportedException>(() => encStream.Position.ShouldBe(0));
        Assert.Throws<NotSupportedException>(() => encStream.Position = 0);
        Assert.Throws<NotSupportedException>(() => encStream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => encStream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => encStream.SetLength(100));
    }

    [Fact]
    public void SingleFlush_DoesNotThrow()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);

        // Act
        encStream.Write("Hello"u8.ToArray(), 0, 5);
        encStream.Flush();

        // Assert
        Assert.True(encStream.CanWrite);
    }

    [Fact]
    public void MultipleFlushes_DoNotThrow()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);

        // Act
        encStream.Write([0x00], 0, 1);
        encStream.Flush();
        encStream.Write([0x01], 0, 1);
        encStream.Flush();
        encStream.Write([0x02], 0, 1);
        encStream.Flush();

        // Assert
        Assert.True(encStream.CanWrite);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimesSafely()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptedStream encStream = new(fs, rsa);

        // Act
        Exception? exception = Record.Exception(() =>
        {
            encStream.Dispose();
            encStream.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void WritingZeroBytes_DoesNotThrowOrCorruptState()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);

        // Act
        encStream.Write([], 0, 0);
        encStream.Flush();

        // Assert
        Assert.True(encStream.CanWrite);
    }

    [Fact]
    public void WriteEscapedSpan_EscapesMarkerCorrectly()
    {
        // Arrange
        byte[] input = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
        byte[] expectedEscaped = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01, 0x00];

        // Act
        Span<byte> actual = new byte[expectedEscaped.Length];
        EncryptedStream.WriteEscapedSpan(input, actual);

        // Assert
        Assert.Equal(expectedEscaped, actual);
    }

    [Theory]
    [InlineData(
        new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
        Label = "Long enough for markers, but no markers"
    )]
    [InlineData(new byte[] { 0x10, 0x20, 0x30, 0x40 }, Label = "Not long enough for markers")]
    public void WriteEscapedSpan_NoMarkers_DoesNotAlterData(byte[] input)
    {
        // Act
        Span<byte> actual = new byte[input.Length];
        EncryptedStream.WriteEscapedSpan(input, actual);

        // Assert
        Assert.Equal(input, actual);
    }
}
