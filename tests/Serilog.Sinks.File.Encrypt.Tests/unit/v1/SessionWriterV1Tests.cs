using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class SessionWriterV1Tests : V1EncryptionTestBase
{
    [Fact]
    public void SessionWriterV1_Writes_Header_And_Messages_Correctly()
    {
        // Arrange
        SessionData sessionData = CreateSessionData();
        ReadOnlySpan<byte> buffer = "Test log message"u8;

        // Use test doubles that track calls
        var headerEncryptor = new TestHeaderEncryptor();
        var messageEncryptor = new TestMessageEncryptor();
        var frameWriter = new TestFrameWriter();

        var writer = new SessionWriterV1(headerEncryptor, messageEncryptor, "", frameWriter);

        // Act
        using var ms = new MemoryStream();
        writer.WriteSession(ms, sessionData, buffer);

        // Assert - Verify header encryptor was called correctly
        headerEncryptor.WasCalled.ShouldBeTrue();
        headerEncryptor.CapturedAesKey.ShouldBe(sessionData.AesKey);
        headerEncryptor.CapturedNonce.ShouldBe(sessionData.Nonce);
        headerEncryptor.CapturedTimestamp.ShouldBe(sessionData.Timestamp);

        // Assert - Verify message encryptor was called correctly
        messageEncryptor.WasCalled.ShouldBeTrue();
        messageEncryptor.CapturedPlaintext.ShouldBe(buffer.ToArray());
        messageEncryptor.CapturedKey.ShouldBe(sessionData.AesKey);
        messageEncryptor.CapturedNonce.ShouldBe(sessionData.Nonce);

        // Assert - Verify frame writer was called correctly
        frameWriter.WriteHeaderCallCount.ShouldBe(1);
        frameWriter.CapturedVersion.ShouldBe((byte)1);
        frameWriter.CapturedHeader.ShouldBe(headerEncryptor.ReturnedHeader);

        int expectedSessionLength =
            headerEncryptor.ReturnedHeader.Length
            + messageEncryptor.GetEncryptedLength(buffer.Length);
        frameWriter.CapturedSessionLength.ShouldBe(expectedSessionLength);
    }

    // Test doubles that can capture span parameters
    private class TestHeaderEncryptor : IHeaderEncryptor
    {
        public bool WasCalled { get; private set; }
        public byte[]? CapturedAesKey { get; private set; }
        public byte[]? CapturedNonce { get; private set; }
        public DateTimeOffset CapturedTimestamp { get; private set; }
        public byte[] ReturnedHeader { get; } = [1, 2, 3, 4];

        public byte[] Encrypt(
            ReadOnlySpan<byte> aesKey,
            ReadOnlySpan<byte> nonce,
            DateTimeOffset timestamp
        )
        {
            WasCalled = true;
            CapturedAesKey = aesKey.ToArray();
            CapturedNonce = nonce.ToArray();
            CapturedTimestamp = timestamp;
            return ReturnedHeader;
        }
    }

    private class TestMessageEncryptor : IMessageEncryptor
    {
        public bool WasCalled { get; private set; }
        public byte[]? CapturedPlaintext { get; private set; }
        public byte[]? CapturedKey { get; private set; }
        public byte[]? CapturedNonce { get; private set; }
        private readonly byte[] _testCiphertext = [5, 6, 7, 8];
        private readonly byte[] _testTag = [9, 10, 11, 12];

        public int GetEncryptedLength(int plaintextLength)
        {
            return plaintextLength + EncryptionConstants.TagLength;
        }

        public void EncryptAndWrite(Stream output, SessionData session, ReadOnlySpan<byte> buffer)
        {
            WasCalled = true;
            CapturedPlaintext = buffer.ToArray();
            CapturedKey = session.AesKey.ToArray();
            CapturedNonce = session.Nonce.ToArray();

            // Write test data to stream
            output.Write(_testCiphertext);
            output.Write(_testTag);
        }
    }

    private class TestFrameWriter : IFrameWriter
    {
        public int WriteHeaderCallCount { get; private set; }
        public byte CapturedVersion { get; private set; }
        public byte[]? CapturedHeader { get; private set; }
        public int CapturedSessionLength { get; private set; }

        public void WriteHeader(
            Stream output,
            byte version,
            ReadOnlyMemory<byte> keyId,
            byte[] header,
            int sessionLength
        )
        {
            WriteHeaderCallCount++;
            CapturedVersion = version;
            CapturedHeader = header;
            CapturedSessionLength = sessionLength;
        }

        // public void WriteMessage(Stream output, EncryptedMessage encryptedMessage)
        // {
        //     // Not used in new implementation
        // }
    }
}
