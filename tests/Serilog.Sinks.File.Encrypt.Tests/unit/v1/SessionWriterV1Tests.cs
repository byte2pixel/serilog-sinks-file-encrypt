using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class SessionWriterV1Tests
{
    private readonly MockHeaderWriterV1 _headerWriter = new();
    private readonly IFrameWriter _frameWriter = new FrameWriter();
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;

    public SessionWriterV1Tests()
    {
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
    }

    [Fact]
    public void GivenKeyIdTooLarge_WhenWriteHeader_ThenThrowsArgumentException()
    {
        // Arrange
        string keyId = new('A', HeaderMetadataV1.KeyIdLength + 1); // 33 bytes when UTF-8 encoded

        SessionWriterV1 writer = new(_headerWriter, keyId, _frameWriter);
        using MemoryStream output = new();

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() => writer.WriteHeader(output, _aesKey, _nonce))
            .Message.ShouldContain(
                "KeyId is too long for the header format. Maximum length is 32 bytes, but was 33 bytes."
            );
    }

    [Fact]
    public void GivenValidKeyId_WhenWriteHeader_ThenEncryptsAndWritesHeader()
    {
        // Arrange
        const string KeyId = "TestKeyId";
        byte[] expectedKeyId = new byte[HeaderMetadataV1.KeyIdLength];
        Encoding.UTF8.GetBytes(KeyId).CopyTo(expectedKeyId, 0);

        byte[] expectedHeader = [.. _aesKey, .. _nonce];

        SessionWriterV1 writer = new(_headerWriter, KeyId, _frameWriter);
        using MemoryStream output = new();

        // Act
        writer.WriteHeader(output, _aesKey, _nonce);

        // Assert
        _headerWriter.ExpectedHeader.ShouldBe(expectedHeader);
        byte[] outputBytes = output.ToArray();
        int offset = 0;
        outputBytes
            .Take(EncryptionConstants.MagicBytes.Length)
            .ShouldBe(EncryptionConstants.MagicBytes);
        offset += EncryptionConstants.MagicBytes.Length;
        outputBytes[offset].ShouldBe((byte)1); // Version byte
        offset += 1;
        outputBytes.Skip(offset).Take(HeaderMetadataV1.KeyIdLength).ShouldBe(expectedKeyId);
        offset += HeaderMetadataV1.KeyIdLength;
        outputBytes.Skip(offset).Take(HeaderMetadataV1.AesKeyLength).ShouldBe(_aesKey);
        offset += HeaderMetadataV1.AesKeyLength;
        outputBytes.Skip(offset).Take(HeaderMetadataV1.NonceLength).ShouldBe(_nonce);
    }
}
