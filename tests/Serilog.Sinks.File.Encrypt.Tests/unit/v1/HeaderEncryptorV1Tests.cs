using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class HeaderEncryptorV1Tests
{
    private readonly RSA _publicKey = RSA.Create();
    private readonly string _keyId;
    private readonly (string publicKey, string privateKey) _kp = EncryptionUtils.GenerateRsaKeyPair(
        format: KeyFormat.Xml
    );

    public HeaderEncryptorV1Tests()
    {
        _publicKey.FromString(_kp.publicKey);
        _keyId = Guid.NewGuid().ToString();
    }

    private HeaderEncryptorV1 GetEncoder(EncryptionOptions? options = null)
    {
        var defaultOptions = new EncryptionOptions(_publicKey, _keyId);
        return new HeaderEncryptorV1(options ?? defaultOptions);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        HeaderEncryptorV1 encryptor = GetEncoder();
        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Hello, World!"u8.ToArray(),
        };

        // Act
        byte[] header = encryptor.Encrypt(session);

        // Assert - Verify encrypted header has correct size
        header.ShouldNotBeNull();
        header.Length.ShouldBe(
            _publicKey.KeySize / 8,
            "Encrypted header should match RSA key size"
        );

        // Decrypt and parse the header
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);

        // Verify all fields match
        decryptedHeader.KeyId.ShouldBe(_keyId);
        decryptedHeader.AesKey.ShouldBe(session.AesKey);
        decryptedHeader.Nonce.ShouldBe(session.Nonce);
        decryptedHeader
            .Timestamp.ToUnixTimeMilliseconds()
            .ShouldBe(session.Timestamp.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Helper method to decrypt and parse the V1 header format.
    /// Format: KeyIdLen(1) + KeyId(var) + AESLen(1) + AESKey(32) + NonceLen(1) + Nonce(12) + Timestamp(8)
    /// </summary>
    private DecryptedHeaderV1 DecryptAndParseHeader(byte[] encryptedHeader)
    {
        using var privateKey = RSA.Create();
        privateKey.FromString(_kp.privateKey);
        byte[] payload = privateKey.Decrypt(encryptedHeader, RSAEncryptionPadding.OaepSHA256);

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
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            new HeaderEncryptorV1(options)
        );
        ex.Message.ShouldContain($"KeyId length ({maxKeyIdSize + 1} bytes)");
        ex.Message.ShouldContain($"recommended maximum ({maxKeyIdSize} bytes)");
        ex.Message.ShouldContain($"RSA-{keySize}");
    }

    [Fact]
    public void Constructor_WithNullKeyId_DoesNotThrow()
    {
        // Arrange
        var options = new EncryptionOptions(_publicKey, null);

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
        var options = new EncryptionOptions(rsa, maxKeyId);
        var encryptor = new HeaderEncryptorV1(options);

        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Test"u8.ToArray(),
        };

        // Act & Assert - Should not throw
        byte[] header = Should.NotThrow(() => encryptor.Encrypt(session));
        header.ShouldNotBeNull();
        header.ShouldNotBeEmpty();
    }

    [Fact]
    public void Encrypt_WithEmptyKeyId_SuccessfullyEncrypts()
    {
        // Arrange
        var options = new EncryptionOptions(_publicKey, string.Empty);
        var encryptor = new HeaderEncryptorV1(options);
        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Test"u8.ToArray(),
        };

        // Act
        byte[] header = encryptor.Encrypt(session);

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
        string unicodeKeyId = "key-🔐-测试-αβγ";
        var options = new EncryptionOptions(_publicKey, unicodeKeyId);
        var encryptor = new HeaderEncryptorV1(options);
        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Test"u8.ToArray(),
        };

        // Act
        byte[] header = encryptor.Encrypt(session);

        // Assert
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);
        decryptedHeader.KeyId.ShouldBe(unicodeKeyId);
    }

    [Fact]
    public void Encrypt_PreservesTimestampAccurately()
    {
        // Arrange
        HeaderEncryptorV1 encryptor = GetEncoder();

        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Test"u8.ToArray(),
        };

        // Act
        byte[] header = encryptor.Encrypt(session);

        // Assert
        DecryptedHeaderV1 decryptedHeader = DecryptAndParseHeader(header);

        // Verify it matches the session timestamp (compare as Unix milliseconds to avoid precision issues)
        decryptedHeader
            .Timestamp.ToUnixTimeMilliseconds()
            .ShouldBe(session.Timestamp.ToUnixTimeMilliseconds());
    }
}
