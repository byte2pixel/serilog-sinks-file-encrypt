using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Tests.Mocks;

namespace Serilog.Sinks.File.Encrypt.Tests;

public class SessionWriterTests
{
    private readonly MockHeaderWriter _headerWriter = new();
    private readonly IFrameWriter _frameWriter = new FrameWriter();
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;

    public SessionWriterTests()
    {
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
    }

    [Fact]
    public void GivenKeyIdTooLarge_WhenWriteHeader_ThenThrowsArgumentException()
    {
        // Arrange
        string keyId = new('A', HeaderMetadata.KeyIdLength + 1); // 33 bytes when UTF-8 encoded

        SessionWriter writer = new(_headerWriter, keyId, _frameWriter);
        using MemoryStream output = new();

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                byte[] headerHash = new byte[EncryptionConstants.HeaderHashLength];
                writer.WriteHeader(output, _aesKey, _nonce, headerHash);
            })
            .Message.ShouldContain(
                "KeyId is too long for the header format. Maximum length is 32 bytes, but was 33 bytes."
            );
    }

    [Fact]
    public void GivenValidKeyId_WhenWriteHeader_ThenEncryptsAndWritesHeader()
    {
        // Arrange
        const string KeyId = "TestKeyId";
        byte[] expectedKeyId = new byte[HeaderMetadata.KeyIdLength];
        Encoding.UTF8.GetBytes(KeyId).CopyTo(expectedKeyId, 0);

        byte[] expectedHeader = [.. _aesKey, .. _nonce];

        SessionWriter writer = new(_headerWriter, KeyId, _frameWriter);
        using MemoryStream output = new();
        byte[] headerHash = new byte[EncryptionConstants.HeaderHashLength];

        // Act
        writer.WriteHeader(output, _aesKey, _nonce, headerHash);

        // Assert
        _headerWriter.ExpectedHeader.ShouldBe(expectedHeader);
        byte[] outputBytes = output.ToArray();
        int offset = 0;
        outputBytes
            .Take(CryptographicUtils.MagicBytes.Length)
            .ShouldBe(CryptographicUtils.MagicBytes);
        offset += CryptographicUtils.MagicBytes.Length;
        outputBytes[offset].ShouldBe(EncryptionConstants.CurrentFormatVersion); // Version byte
        offset += 1;
        outputBytes.Skip(offset).Take(HeaderMetadata.KeyIdLength).ShouldBe(expectedKeyId);
        offset += HeaderMetadata.KeyIdLength;
        outputBytes.Skip(offset).Take(HeaderMetadata.AesKeyLength).ShouldBe(_aesKey);
        offset += HeaderMetadata.AesKeyLength;
        outputBytes.Skip(offset).Take(HeaderMetadata.NonceLength).ShouldBe(_nonce);
    }

    [Theory]
    [InlineData(EncryptionConstants.HeaderHashLength - 1)]
    [InlineData(EncryptionConstants.HeaderHashLength + 1)]
    [InlineData(0)]
    public void GivenWrongSizeHeaderHash_WhenWriteHeader_ThenThrowsArgumentException(int hashSize)
    {
        // Arrange
        SessionWriter writer = new(_headerWriter, "TestKeyId", _frameWriter);
        using MemoryStream output = new();

        // Act & Assert
        Should
            .Throw<ArgumentException>(() =>
            {
                byte[] headerHash = new byte[hashSize];
                writer.WriteHeader(output, _aesKey, _nonce, headerHash);
            })
            .Message.ShouldContain(
                $"headerHash must be exactly {EncryptionConstants.HeaderHashLength} bytes"
            );
    }

    [Fact]
    public void GivenValidKeyId_WhenWriteHeader_ThenHeaderHashIsSha256OfWrittenBytes()
    {
        // Arrange
        SessionWriter writer = new(_headerWriter, "TestKeyId", _frameWriter);
        using MemoryStream output = new();
        byte[] headerHash = new byte[EncryptionConstants.HeaderHashLength];

        // Act
        writer.WriteHeader(output, _aesKey, _nonce, headerHash);

        // Assert: the hash must cover the exact bytes that landed in the stream.
        headerHash.ShouldBe(SHA256.HashData(output.ToArray()));
    }
}
