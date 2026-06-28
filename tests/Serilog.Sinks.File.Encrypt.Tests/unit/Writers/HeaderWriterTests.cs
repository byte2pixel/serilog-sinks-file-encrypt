namespace Serilog.Sinks.File.Encrypt.Tests;

public class HeaderWriterTests : IDisposable
{
    private readonly RSA _publicRsa = RSA.Create();
    private readonly RSA _privateRsa = RSA.Create();
    private readonly EncryptionOptions _options;

    public HeaderWriterTests()
    {
        (string publicKey, string privateKey) keyPair = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );
        _publicRsa.FromString(keyPair.publicKey);
        _privateRsa.FromString(keyPair.privateKey);
        _options = TestUtils.GetEncryptionOptions(_publicRsa);
    }

    public void Dispose()
    {
        _publicRsa.Dispose();
        _privateRsa.Dispose();
    }

    private HeaderWriter GetSut(EncryptionOptions? options = null)
    {
        return new HeaderWriter(options ?? _options);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        HeaderWriter writer = GetSut();
        (byte[] aesKey, byte[] nonce) = TestUtils.CreateSessionData();

        // Act
        ReadOnlySpan<byte> header = writer.Encrypt(aesKey, nonce);

        // Assert - Verify encrypted header has correct size
        int totalLen = _publicRsa.KeySize / 8;
        header.Length.ShouldBe(totalLen, "Encrypted header should match RSA key size");

        // Decrypt and parse the header
        DecryptedHeader decryptedHeader = DecryptAndParseHeader(header);

        // Verify decrypted values match
        decryptedHeader.AesKey.ShouldBe(aesKey);
        decryptedHeader.Nonce.ShouldBe(nonce);
    }

    /// <summary>
    /// Helper method to decrypt and parse the header format.
    /// Format:
    /// RSA-Encrypted: AESKey(32) + Nonce(12)
    /// </summary>
    private byte[] RsaDecrypt(ReadOnlySpan<byte> encryptedData) =>
        _privateRsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);

    private DecryptedHeader DecryptAndParseHeader(ReadOnlySpan<byte> header)
    {
        byte[] decryptedPayload = RsaDecrypt(header);

        decryptedPayload.Length.ShouldBeGreaterThan(0);

        int payloadOffset = 0;

        // Parse AES Key
        byte[] aesKey = decryptedPayload
            .AsSpan(payloadOffset, HeaderMetadata.AesKeyLength)
            .ToArray();
        payloadOffset += HeaderMetadata.AesKeyLength;

        // Parse Nonce
        byte[] nonce = decryptedPayload.AsSpan(payloadOffset, HeaderMetadata.NonceLength).ToArray();

        return new DecryptedHeader(aesKey, nonce);
    }

    private record DecryptedHeader(byte[] AesKey, byte[] Nonce);
}
