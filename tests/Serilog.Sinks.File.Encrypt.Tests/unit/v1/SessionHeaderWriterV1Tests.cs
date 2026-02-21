using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class SessionHeaderWriterV1Tests
{
    private readonly MockHeaderEncryptorV1 _headerEncryptor = new();
    private readonly IFrameWriter _frameWriter = new FrameWriter();
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;

    public SessionHeaderWriterV1Tests()
    {
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
    }

    [Fact]
    public void GivenKeyIdTooLarge_WhenWriteHeader_ThenThrowsArgumentException()
    {
        // Arrange
        string keyId = new('A', HeaderMetadataV1.KeyIdLength + 1); // 33 bytes when UTF-8 encoded

        SessionHeaderWriterV1 headerWriter = new(_headerEncryptor, keyId, _frameWriter);
        using MemoryStream output = new();

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
                headerWriter.WriteHeader(output, _aesKey, _nonce)
            )
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

        SessionHeaderWriterV1 headerWriter = new(_headerEncryptor, KeyId, _frameWriter);
        using MemoryStream output = new();

        // Act
        headerWriter.WriteHeader(output, _aesKey, _nonce);

        // Assert
        _headerEncryptor.ExpectedHeader.ShouldBe(expectedHeader);
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
