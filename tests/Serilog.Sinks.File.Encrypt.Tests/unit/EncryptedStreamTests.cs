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
        encStream.CanRead.ShouldBeFalse();
        encStream.CanSeek.ShouldBeFalse();
        encStream.CanWrite.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => encStream.Length.ShouldBe(0));
        Should.Throw<NotSupportedException>(() => encStream.Position = 0);
        Should.Throw<NotSupportedException>(() => encStream.Read(new byte[1], 0, 1));
        Should.Throw<NotSupportedException>(() => encStream.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => encStream.SetLength(100));
    }

    [Fact]
    public void WriteAndFlush_Moves_Position()
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
        encStream.Position.ShouldBeGreaterThan(5);
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
        encStream.Position.ShouldBeGreaterThan(3);
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
        exception.ShouldBeNull();
    }

    [Fact]
    public void WritingZeroBytes_DoesNot_WriteData()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);

        long staringPosition = encStream.Position;
        // Act
        encStream.Write([], 0, 0);
        encStream.Flush();

        // Assert
        encStream.Position.ShouldBeEquivalentTo(staringPosition);
    }

    [Fact]
    public void EscapeMarkersInHeader_EscapesMarkersCorrectly()
    {
        // Arrange
        byte[] header = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
        byte[] expectedEscaped = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01, 0x00];
        byte[] finalBuffer = new byte[expectedEscaped.Length];

        // Act
        int bytesWritten = EncryptedStream.EscapeMarkersInHeader(header.AsSpan(), finalBuffer);

        // Assert
        bytesWritten.ShouldBeEquivalentTo(expectedEscaped.Length);
        finalBuffer.ShouldBeEquivalentTo(expectedEscaped);
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
        actual.ToArray().ShouldBeEquivalentTo(expectedEscaped);
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
        actual.ToArray().ShouldBeEquivalentTo(input);
    }

    [Fact]
    public void Ctor_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EncryptedStream(null!, rsa));
    }

    [Fact]
    public void Ctor_NullRsa_ThrowsArgumentNullException()
    {
        // Arrange
        using MemoryStream fs = new();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EncryptedStream(fs, null!));
    }

    [Fact]
    public void Ctor_KeyTooShort_ThrowsArgumentException()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair(512); // Too short for OAEP
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new EncryptedStream(fs, rsa));
    }
}
