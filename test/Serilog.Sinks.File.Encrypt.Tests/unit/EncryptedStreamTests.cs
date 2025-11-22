namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptedStreamTests
{
    [Fact]
    public void StreamContract_PropertiesAndUnsupportedMethods_ThrowOrReturnExpected()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);
        Assert.False(encStream.CanRead);
        Assert.False(encStream.CanSeek);
        Assert.True(encStream.CanWrite);
        Assert.Throws<NotSupportedException>(() =>
        {
            long _ = encStream.Length;
        });
        Assert.Throws<NotSupportedException>(() =>
        {
            long _ = encStream.Position;
        });
        Assert.Throws<NotSupportedException>(() =>
        {
            long _ = encStream.Position = 0;
        });
        Assert.Throws<NotSupportedException>(() => encStream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => encStream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => encStream.SetLength(100));
    }
    
    [Fact]
    public void SingleFlush_DoesNotThrow()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);
        encStream.Write("Hello"u8.ToArray(), 0, 5);
        encStream.Flush();
    }
    
    [Fact]
    public void MultipleFlushes_DoNotThrow()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);
        encStream.Write([0x00], 0, 1);
        encStream.Flush();
        encStream.Write([0x01], 0, 1);
        encStream.Flush();
        encStream.Write([0x02], 0, 1);
        encStream.Flush();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimesSafely()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptedStream encStream = new(fs, rsa);
        encStream.Dispose();
        encStream.Dispose();
    }

    [Fact]
    public void WritingZeroBytes_DoesNotThrowOrCorruptState()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedStream encStream = new(fs, rsa);
        encStream.Write([], 0, 0);
        encStream.Flush();
    }
}