using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class HeaderEncoderV1Tests
{
    private readonly RSA _publicKey = RSA.Create();
    private readonly string _keyId;
    private readonly (string publicKey, string privateKey) _kp = EncryptionUtils.GenerateRsaKeyPair(
        format: KeyFormat.Xml
    );

    public HeaderEncoderV1Tests()
    {
        _publicKey.FromString(_kp.publicKey);
        _keyId = Guid.NewGuid().ToString();
    }

    private HeaderEncoderV1 GetEncoder(EncryptionOptions? options = null)
    {
        var defaultOptions = new EncryptionOptions(_publicKey, _keyId, 1);
        return new HeaderEncoderV1(options ?? defaultOptions);
    }

    [Fact]
    public void EncodeHeader_ReturnsNonEmptyByteArray()
    {
        // Arrange
        HeaderEncoderV1 encoder = GetEncoder();
        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Nonce = RandomNumberGenerator.GetBytes(12),
            Plaintext = "Hello, World!"u8.ToArray(),
        };

        // Act
        byte[] header = encoder.EncodeHeader(session);

        // Assert
        header.ShouldNotBeNull();
        header.ShouldNotBeEmpty();

        RSA privateKey = RSA.Create();
        privateKey.FromString(_kp.privateKey);
        byte[] decryptedPayload = privateKey.Decrypt(header, RSAEncryptionPadding.OaepSHA256);
        decryptedPayload.Length.ShouldBeGreaterThan(0);

        Span<byte> keyIdLengthSpan = decryptedPayload.AsSpan(0, 1);
        Span<byte> keyIdSpan = decryptedPayload.AsSpan(1, keyIdLengthSpan[0]);
        string decodedKeyId = Encoding.UTF8.GetString(keyIdSpan);
        decodedKeyId.ShouldBe(_keyId);

        int offset = 1 + keyIdLengthSpan[0];
        Span<byte> aesKeyLengthSpan = decryptedPayload.AsSpan(offset, 1);
        Span<byte> aesKeySpan = decryptedPayload.AsSpan(offset + 1, aesKeyLengthSpan[0]);
        aesKeySpan.ToArray().ShouldBe(session.AesKey);

        offset += 1 + aesKeyLengthSpan[0];
        Span<byte> nonceLengthSpan = decryptedPayload.AsSpan(offset, 1);
        Span<byte> nonceSpan = decryptedPayload.AsSpan(offset + 1, nonceLengthSpan[0]);
        nonceSpan.ToArray().ShouldBe(session.Nonce);

        offset += 1 + nonceLengthSpan[0];
        Span<byte> timestampSpan = decryptedPayload.AsSpan(offset, 8);
        long timestamp = BitConverter.ToInt64(timestampSpan);
        timestamp.ShouldBe(session.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds());
    }
}
