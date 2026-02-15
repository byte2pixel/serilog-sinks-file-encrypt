using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class MessageEncryptorV1Tests : V1EncryptionTestBase
{
    private static MessageEncryptorV1 GetSut()
    {
        return new MessageEncryptorV1();
    }

    private (byte[] ciphertext, byte[] tag) EncryptToBytes(
        MessageEncryptorV1 encryptor,
        SessionData session,
        ReadOnlySpan<byte> buffer
    )
    {
        using var ms = new MemoryStream();
        encryptor.EncryptAndWrite(ms, session, buffer);
        byte[] encrypted = ms.ToArray();
        int plaintextLength = buffer.Length;
        return (encrypted[..plaintextLength], encrypted[plaintextLength..]);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();
        ReadOnlySpan<byte> buffer = "Hello, World!"u8;

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session, buffer);

        // Assert - Verify encrypted data has expected length
        ciphertext.Length.ShouldBe(
            buffer.Length,
            "Ciphertext should be same length as plaintext for AES-GCM"
        );
        tag.Length.ShouldBe(
            EncryptionConstants.TagLength,
            $"Tag should be {EncryptionConstants.TagLength} bytes for AES-GCM"
        );

        // Decrypt the message using AES-GCM and verify it matches the original plaintext
        byte[] decryptedPlaintext = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decryptedPlaintext.ShouldBe(
            buffer.ToArray(),
            "Decrypted plaintext should match original plaintext"
        );
    }

    [Fact]
    public void Encrypt_WithEmptyPlaintext_SuccessfullyEncrypts()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session, ""u8);

        // Assert
        ciphertext.Length.ShouldBe(0, "Empty plaintext should produce empty ciphertext");
        tag.Length.ShouldBe(EncryptionConstants.TagLength, "Tag should still be present");

        // Verify decryption works
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBeEmpty();
    }

    [Fact]
    public void Encrypt_WithLargeMessage_SuccessfullyEncrypts()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();
        string largePlaintext = new('X', 1024 * 100); // 100 KB
        ReadOnlySpan<byte> buffer = Encoding.UTF8.GetBytes(largePlaintext).AsSpan();

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session, buffer);

        // Assert
        ciphertext.Length.ShouldBe(buffer.Length);
        tag.Length.ShouldBe(EncryptionConstants.TagLength);

        // Verify decryption works
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(buffer.ToArray());
    }

    [Fact]
    public void Encrypt_WithUnicodeContent_PreservesEncoding()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();
        ReadOnlySpan<byte> buffer = "Hello 🌍 世界 Κόσμε"u8;

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session, buffer);

        // Assert
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(buffer.ToArray());
        Encoding.UTF8.GetString(decrypted).ShouldBe("Hello 🌍 世界 Κόσμε");
    }

    [Fact]
    public void Encrypt_SameInputWithSameNonce_ProducesSameCiphertext()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();
        ReadOnlySpan<byte> buffer = "Test message"u8;

        // Act - Encrypt twice with same parameters
        var (ciphertext1, tag1) = EncryptToBytes(encryptor, session, buffer);
        var (ciphertext2, tag2) = EncryptToBytes(encryptor, session, buffer);

        // Assert - Should be deterministic with same nonce
        ciphertext1.ShouldBe(ciphertext2);
        tag1.ShouldBe(tag2);
    }

    [Fact]
    public void Encrypt_SameInputWithDifferentNonce_ProducesDifferentCiphertext()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        ReadOnlySpan<byte> buffer = "Test message"u8.ToArray();
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce1 = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        byte[] nonce2 = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);

        SessionData session1 = new SessionData
        {
            AesGcm = new AesGcm(key, EncryptionConstants.TagLength),
            AesKey = key,
            Nonce = nonce1,
        };

        SessionData session2 = new SessionData
        {
            AesGcm = new AesGcm(key, EncryptionConstants.TagLength),
            AesKey = key,
            Nonce = nonce2,
        };

        // Act
        using var ms1 = new MemoryStream();
        encryptor.EncryptAndWrite(ms1, session1, buffer);
        byte[] encrypted1 = ms1.ToArray();

        using var ms2 = new MemoryStream();
        encryptor.EncryptAndWrite(ms2, session2, buffer);
        byte[] encrypted2 = ms2.ToArray();

        // Assert - Different nonce should produce different ciphertexts
        encrypted1[..buffer.Length].ShouldNotBe(encrypted2[..buffer.Length]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Short message")]
    [InlineData(
        "This is a longer message that spans multiple words and tests various content lengths"
    )]
    public void Encrypt_VariousMessageLengths_AllSucceed(string plaintext)
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData();
        ReadOnlySpan<byte> buffer = Encoding.UTF8.GetBytes(plaintext).AsSpan();

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session, buffer);

        // Assert
        ciphertext.Length.ShouldBe(buffer.Length);
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(buffer.ToArray());
    }
}
