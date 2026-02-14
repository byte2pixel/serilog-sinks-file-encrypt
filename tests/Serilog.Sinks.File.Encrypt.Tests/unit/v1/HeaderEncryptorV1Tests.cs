using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class HeaderEncryptorV1Tests : V1EncryptionTestBase
{
    private HeaderEncryptorV1 GetSut(EncryptionOptions? options = null)
    {
        return new HeaderEncryptorV1(options ?? CreateDefaultOptions());
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        HeaderEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData("Hello, World!");

        // Act
        byte[] header = encryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // Assert - Verify encrypted header has correct size
        header.ShouldNotBeNull();
        header.Length.ShouldBe(PublicKey.KeySize / 8, "Encrypted header should match RSA key size");

        // Decrypt and parse the header
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);

        // Verify all fields match
        decryptedHeader.KeyId.ShouldBe(KeyId);
        decryptedHeader.AesKey.ShouldBe(session.AesKey);
        decryptedHeader.Nonce.ShouldBe(session.Nonce);
        AssertTimestampEqual(session.Timestamp, decryptedHeader.Timestamp);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Constructor_WithValidKeyId_DoesNotThrow(int keySize)
    {
        // Arrange
        using var rsa = RSA.Create(keySize);
        var metadata = HeaderMetadata.CreateV1();
        int maxKeyIdSize = metadata.GetMaxVariableFieldSize(keySize);
        string validKeyId = new('A', maxKeyIdSize);
        var options = new EncryptionOptions(rsa, validKeyId);

        // Act & Assert
        Should.NotThrow(() => new HeaderEncryptorV1(options));
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Constructor_WithOversizedKeyId_ThrowsArgumentException(int keySize)
    {
        // Arrange
        using var rsa = RSA.Create(keySize);
        var metadata = HeaderMetadata.CreateV1();
        int maxKeyIdSize = metadata.GetMaxVariableFieldSize(keySize);
        string oversizedKeyId = new('A', maxKeyIdSize + 1);
        var options = new EncryptionOptions(rsa, oversizedKeyId);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new HeaderEncryptorV1(options));
    }

    [Fact]
    public void Constructor_WithNullKeyId_DoesNotThrow()
    {
        // Arrange
        EncryptionOptions options = CreateOptionsWithKeyId(null);

        // Act & Assert
        Should.NotThrow(() => new HeaderEncryptorV1(options));
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Encrypt_WithMaximumKeyIdSize_SuccessfullyEncrypts(int keySize)
    {
        // Arrange
        using var rsa = RSA.Create(keySize);
        var metadata = HeaderMetadata.CreateV1();
        int maxKeyIdSize = metadata.GetMaxVariableFieldSize(keySize);
        string maxKeyId = new('B', maxKeyIdSize);
        EncryptionOptions options = CreateOptionsWithCustomKey(rsa, maxKeyId);
        var encryptor = new HeaderEncryptorV1(options);
        SessionData session = CreateSessionData();

        // Act & Assert - Should not throw
        byte[] header = Should.NotThrow(() =>
            encryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp)
        );
        header.ShouldNotBeNull();
        header.ShouldNotBeEmpty();
    }

    [Fact]
    public void Encrypt_WithEmptyKeyId_SuccessfullyEncrypts()
    {
        // Arrange
        EncryptionOptions options = CreateOptionsWithKeyId(string.Empty);
        var encryptor = new HeaderEncryptorV1(options);
        SessionData session = CreateSessionData();

        // Act
        byte[] header = encryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // Assert
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);
        decryptedHeader.KeyId.ShouldBe(string.Empty);
        decryptedHeader.AesKey.ShouldBe(session.AesKey);
        decryptedHeader.Nonce.ShouldBe(session.Nonce);
    }

    [Fact]
    public void Encrypt_WithUnicodeKeyId_PreservesEncoding()
    {
        // Arrange
        const string UnicodeKeyId = "key-🔐-测试-αβγ";
        EncryptionOptions options = CreateOptionsWithKeyId(UnicodeKeyId);
        var encryptor = new HeaderEncryptorV1(options);
        SessionData session = CreateSessionData();

        // Act
        byte[] header = encryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // Assert
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);
        decryptedHeader.KeyId.ShouldBe(UnicodeKeyId);
    }

    [Fact]
    public void Encrypt_PreservesTimestampAccurately()
    {
        // Arrange
        HeaderEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();

        // Act
        byte[] header = encryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // Assert
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);

        // Verify it matches the session timestamp (compare as Unix milliseconds to avoid precision issues)
        AssertTimestampEqual(session.Timestamp, decryptedHeader.Timestamp);
    }

    /// <summary>
    /// Helper method to decrypt and parse the V1 header format.
    /// Format: KeyIdLen(1) + KeyId(var) + AESLen(1) + AESKey(32) + NonceLen(1) + Nonce(12) + Timestamp(8)
    /// </summary>
    private DecryptedHeaderV1 DecryptAndParseHeader(byte[] encryptedHeader)
    {
        byte[] payload = RsaDecrypt(encryptedHeader);

        payload.Length.ShouldBeGreaterThan(0);

        int offset = 0;

        // Parse KeyId
        byte keyIdLength = payload[offset++];
        string keyId = Encoding.UTF8.GetString(payload.AsSpan(offset, keyIdLength));
        offset += keyIdLength;

        // Parse AES Key
        byte aesKeyLength = payload[offset++];
        byte[] aesKey = payload.AsSpan(offset, aesKeyLength).ToArray();
        offset += aesKeyLength;

        // Parse Nonce
        byte nonceLength = payload[offset++];
        byte[] nonce = payload.AsSpan(offset, nonceLength).ToArray();
        offset += nonceLength;

        // Parse Timestamp
        long timestamp = BitConverter.ToInt64(payload.AsSpan(offset, 8));
        var timestampOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        return new DecryptedHeaderV1(keyId, aesKey, nonce, timestampOffset);
    }

    private record DecryptedHeaderV1(
        string KeyId,
        byte[] AesKey,
        byte[] Nonce,
        DateTimeOffset Timestamp
    );
}
