using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class HeaderEncryptorV1Tests : V1EncryptionTestBase
{
    private readonly EncryptionOptions _options;

    public HeaderEncryptorV1Tests()
    {
        _options = CreateDefaultOptions();
    }

    private HeaderEncryptorV1 GetSut(EncryptionOptions? options = null)
    {
        return new HeaderEncryptorV1(options ?? _options);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        HeaderEncryptorV1 encryptor = GetSut();
        (byte[] aesKey, byte[] nonce) = CreateSessionData();

        // Act
        ReadOnlySpan<byte> header = encryptor.Encrypt(aesKey, nonce);

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
