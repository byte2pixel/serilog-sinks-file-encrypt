namespace Serilog.Sinks.File.Encrypt.Tests;

public class HeaderWriterV1Tests : V1EncryptionTestBase
{
    private readonly EncryptionOptions _options;

    public HeaderWriterV1Tests()
    {
        _options = TestUtils.GetEncryptionOptions(PublicRsa);
    }

    private HeaderWriterV1 GetSut(EncryptionOptions? options = null)
    {
        return new HeaderWriterV1(options ?? _options);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        HeaderWriterV1 writer = GetSut();
        (byte[] aesKey, byte[] nonce) = TestUtils.CreateSessionData();

        // Act
        ReadOnlySpan<byte> header = writer.Encrypt(aesKey, nonce);

        // Assert - Verify encrypted header has correct size
        int totalLen = PublicRsa.KeySize / 8;
        header.Length.ShouldBe(totalLen, "Encrypted header should match RSA key size");

        // Decrypt and parse the header
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);

        // Verify all fields match
        decryptedHeader.KeyId.ShouldBe(KeyId);
        decryptedHeader.AesKey.ShouldBe(aesKey);
        decryptedHeader.Nonce.ShouldBe(nonce);
    }

    /// <summary>
    /// Helper method to decrypt and parse the V1 header format.
    /// Format:
    /// RSA-Encrypted: AESKey(32) + Nonce(12)
    /// </summary>
    private DecryptedHeaderV1 DecryptAndParseHeader(ReadOnlySpan<byte> header)
    {
        byte[] decryptedPayload = RsaDecrypt(header);

        decryptedPayload.Length.ShouldBeGreaterThan(0);

        int payloadOffset = 0;

        // Parse AES Key
        byte[] aesKey = decryptedPayload
            .AsSpan(payloadOffset, HeaderMetadataV1.AesKeyLength)
            .ToArray();
        payloadOffset += HeaderMetadataV1.AesKeyLength;

        // Parse Nonce
        byte[] nonce = decryptedPayload
            .AsSpan(payloadOffset, HeaderMetadataV1.NonceLength)
            .ToArray();

        return new DecryptedHeaderV1(KeyId, aesKey, nonce);
    }

    private record DecryptedHeaderV1(string KeyId, byte[] AesKey, byte[] Nonce);
}
