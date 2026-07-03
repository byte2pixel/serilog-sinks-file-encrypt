using System.Diagnostics.CodeAnalysis;

namespace Serilog.Sinks.File.Encrypt.Tests;

public class LogWriterTests
{
    [Fact]
    public void StreamContract_PropertiesAndUnsupportedMethods_ThrowOrReturnExpected()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));

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
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));

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
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));

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
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));

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
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));

        long staringPosition = logWriter.Position;
        // Act
        logWriter.Write([], 0, 0);
        logWriter.Flush();

        // Assert
        logWriter.Position.ShouldBeEquivalentTo(staringPosition);
    }

    [Fact]
    public void Dispose_WritesSealRecord()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        MemoryStream fs = new();
        long lengthBeforeDispose;

        // Act
        using (LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey)))
        {
            logWriter.Write("Hello"u8);
            logWriter.Flush();
            lengthBeforeDispose = logWriter.Length;
        }

        // Assert: dispose appended exactly one 28-byte seal record (marker + ct + tag)
        byte[] bytes = fs.ToArray();
        int sealLength =
            EncryptionConstants.SealMarkerBytes.Length
            + EncryptionConstants.SealRecordRemainderLength;
        bytes.Length.ShouldBe((int)lengthBeforeDispose + sealLength);
        bytes[^sealLength..^EncryptionConstants.SealRecordRemainderLength]
            .ShouldBe(EncryptionConstants.SealMarkerBytes);
    }

    [Fact]
    public void Dispose_WithoutWrites_LeavesStreamEmpty()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        MemoryStream fs = new();

        // Act: no writes means no session, so dispose must not emit a header or seal
        using (LogWriter _ = new(fs, TestUtils.GetEncryptionOptions(publicKey))) { }

        // Assert
        fs.ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void Dispose_Twice_WritesExactlyOneSeal()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        MemoryStream fs = new();
        LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));
        logWriter.Write("Hello"u8);
        logWriter.Flush();

        // Act
        logWriter.Dispose();
        int lengthAfterFirstDispose = fs.ToArray().Length;
        logWriter.Dispose();

        // Assert
        fs.ToArray().Length.ShouldBe(lengthAfterFirstDispose);
    }

    [Fact]
    public void Dispose_WhenSealWriteThrows_StillDisposesInnerStream()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        ThrowOnWriteStream fs = new();
        LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey));
        logWriter.Write("Hello"u8);
        logWriter.Flush();

        // Act: arm the failure so the seal write in Dispose throws (e.g. disk full)
        fs.ThrowOnWrite = true;
        Should.Throw<IOException>(() => logWriter.Dispose());

        // Assert: the inner stream was still disposed; the file simply ends unsealed
        fs.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void IdenticalPlaintext_ProducesDifferentFrames()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        MemoryStream fs = new();
        long headerAndFirstFrameLength;
        using (LogWriter logWriter = new(fs, TestUtils.GetEncryptionOptions(publicKey)))
        {
            logWriter.Write("repeat"u8);
            logWriter.Flush();
            headerAndFirstFrameLength = logWriter.Length;
            logWriter.Write("repeat"u8);
            logWriter.Flush();

            // Assert: same plaintext, but nonce and AAD frame sequence differ per frame,
            // so the ciphertext+tag bytes must differ.
            byte[] bytes = fs.ToArray();
            int frameLength = (int)(bytes.Length - headerAndFirstFrameLength);
            byte[] firstFrame = bytes[
                ((int)headerAndFirstFrameLength - frameLength)..(int)headerAndFirstFrameLength
            ];
            byte[] secondFrame = bytes[(int)headerAndFirstFrameLength..];
            firstFrame.ShouldNotBe(secondFrame);
        }
    }

    private sealed class ThrowOnWriteStream : MemoryStream
    {
        public bool ThrowOnWrite { get; set; }
        public bool Disposed { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ThrowOnWrite)
            {
                throw new IOException("Simulated write failure");
            }
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (ThrowOnWrite)
            {
                throw new IOException("Simulated write failure");
            }
            base.Write(buffer);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void Ctor_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        (string publicKey, _) = CryptographicUtils.GenerateRsaKeyPair();
        EncryptionOptions options = TestUtils.GetEncryptionOptions(publicKey);
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
