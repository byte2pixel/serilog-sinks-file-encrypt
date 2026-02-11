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
