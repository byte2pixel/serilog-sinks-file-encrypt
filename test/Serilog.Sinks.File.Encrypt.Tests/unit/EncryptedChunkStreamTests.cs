namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptedChunkStreamTests
{
    [Fact]
    public void StreamContract_PropertiesAndUnsupportedMethods_ThrowOrReturnExpected()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedChunkStream encStream = new(fs, rsa);
        Assert.False(encStream.CanRead);
        Assert.False(encStream.CanSeek);
        Assert.True(encStream.CanWrite);
        Assert.Throws<NotSupportedException>(() => { long _ = encStream.Length; });
        Assert.Throws<NotSupportedException>(() => { long _ = encStream.Position; });
        Assert.Throws<NotSupportedException>(() => encStream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => encStream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => encStream.SetLength(100));
    }

    [Fact]
    public void MultipleFlushes_DoNotThrow()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        using EncryptedChunkStream encStream = new(fs, rsa);
        encStream.Flush();
        encStream.Flush();
        encStream.Flush();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimesSafely()
    {
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        EncryptedChunkStream encStream = new(fs, rsa);
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
        using EncryptedChunkStream encStream = new(fs, rsa);
        encStream.Write([], 0, 0);
        encStream.Flush();
    }
}