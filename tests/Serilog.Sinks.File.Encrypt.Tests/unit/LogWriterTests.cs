using System.Diagnostics.CodeAnalysis;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class LogWriterTests
{
    [Fact]
    public void StreamContract_PropertiesAndUnsupportedMethods_ThrowOrReturnExpected()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptionOptions options = new(rsa);
        using LogWriter logWriter = new(fs, options);

        // Act & Assert
        logWriter.CanRead.ShouldBeFalse();
        logWriter.CanSeek.ShouldBeFalse();
        logWriter.CanWrite.ShouldBeTrue();
        logWriter.Length.ShouldBe(0);

        Should.Throw<NotSupportedException>(() => logWriter.Read(new byte[1], 0, 1));
        Should.Throw<NotSupportedException>(() => logWriter.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => logWriter.SetLength(100));
        Should.Throw<NotSupportedException>(() => logWriter.Position = 0);
    }

    [Fact]
    public void WriteAndFlush_Moves_Position()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptionOptions options = new(rsa);
        using LogWriter logWriter = new(fs, options);

        // Act
        logWriter.Write("Hello"u8.ToArray(), 0, 5);
        logWriter.Flush();

        // Assert
        logWriter.Position.ShouldBeGreaterThan(5);
    }

    [Fact]
    public void MultipleFlushes_DoNotThrow()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptionOptions options = new(rsa);
        using LogWriter logWriter = new(fs, options);

        // Act
        logWriter.Write([0x00], 0, 1);
        logWriter.Flush();
        logWriter.Write([0x01], 0, 1);
        logWriter.Flush();
        logWriter.Write([0x02], 0, 1);
        logWriter.Flush();

        // Assert
        logWriter.Position.ShouldBeGreaterThan(3);
    }

    [Fact]
    [SuppressMessage(
        "ReSharper",
        "DisposeOnUsingVariable",
        Justification = "We want to test that Dispose can be called multiple times without throwing."
    )]
    [SuppressMessage(
        "ReSharper",
        "AccessToDisposedClosure",
        Justification = "We want to test that Dispose can be called multiple times without throwing."
    )]
    public void Dispose_CanBeCalledMultipleTimesSafely()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptionOptions options = new(rsa);
        using LogWriter logWriter = new(fs, options);

        // Act
        Exception? exception = Record.Exception(() =>
        {
            logWriter.Dispose();
            logWriter.Dispose();
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
        EncryptionOptions options = new(rsa);
        using LogWriter logWriter = new(fs, options);

        long staringPosition = logWriter.Position;
        // Act
        logWriter.Write([], 0, 0);
        logWriter.Flush();

        // Assert
        logWriter.Position.ShouldBeEquivalentTo(staringPosition);
    }

    [Fact]
    public void Ctor_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptionOptions options = new(rsa);
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LogWriter(null!, options));
    }

    [Fact]
    public void Ctor_NullRsa_ThrowsArgumentNullException()
    {
        // Arrange
        using MemoryStream fs = new();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LogWriter(fs, null!));
    }
}
