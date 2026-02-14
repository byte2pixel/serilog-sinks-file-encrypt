using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public class MessageEncryptorV1Tests : V1EncryptionTestBase
{
    private static MessageEncryptorV1 GetSut()
    {
        return new MessageEncryptorV1();
    }

    private (byte[] ciphertext, byte[] tag) EncryptToBytes(
        MessageEncryptorV1 encryptor,
        SessionData session
    )
    {
        using var ms = new MemoryStream();
        encryptor.EncryptAndWrite(ms, session.Plaintext, session.AesKey, session.Nonce);
        byte[] encrypted = ms.ToArray();
        int plaintextLength = session.Plaintext.Length;
        return (encrypted[..plaintextLength], encrypted[plaintextLength..]);
    }

    [Fact]
    public void Encrypt_And_Decrypts_Correctly()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData("Hello, World!");

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session);

        // Assert - Verify encrypted data has expected length
        ciphertext.Length.ShouldBe(
            session.Plaintext.Length,
            "Ciphertext should be same length as plaintext for AES-GCM"
        );
        tag.Length.ShouldBe(
            EncryptionConstants.TagLength,
            $"Tag should be {EncryptionConstants.TagLength} bytes for AES-GCM"
        );

        // Decrypt the message using AES-GCM and verify it matches the original plaintext
        byte[] decryptedPlaintext = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decryptedPlaintext.ShouldBe(
            session.Plaintext.ToArray(),
            "Decrypted plaintext should match original plaintext"
        );
    }

    [Fact]
    public void Encrypt_WithEmptyPlaintext_SuccessfullyEncrypts()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData(string.Empty);

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session);

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
        string largePlaintext = new('X', 1024 * 100); // 100 KB
        SessionData session = CreateSessionData(largePlaintext);

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session);

        // Assert
        ciphertext.Length.ShouldBe(session.Plaintext.Length);
        tag.Length.ShouldBe(EncryptionConstants.TagLength);

        // Verify decryption works
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(session.Plaintext.ToArray());
    }

    [Fact]
    public void Encrypt_WithUnicodeContent_PreservesEncoding()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData("Hello 🌍 世界 Κόσμε");

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session);

        // Assert
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(session.Plaintext.ToArray());
        Encoding.UTF8.GetString(decrypted).ShouldBe("Hello 🌍 世界 Κόσμε");
    }

    [Fact]
    public void Encrypt_SameInputWithSameNonce_ProducesSameCiphertext()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData("Test message");

        // Act - Encrypt twice with same parameters
        var (ciphertext1, tag1) = EncryptToBytes(encryptor, session);
        var (ciphertext2, tag2) = EncryptToBytes(encryptor, session);

        // Assert - Should be deterministic with same nonce
        ciphertext1.ShouldBe(ciphertext2);
        tag1.ShouldBe(tag2);
    }

    [Fact]
    public void Encrypt_SameInputWithDifferentNonce_ProducesDifferentCiphertext()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        byte[] plaintext = "Test message"u8.ToArray();
        ReadOnlyMemory<byte> plaintextMemory = new ReadOnlyMemory<byte>(plaintext);
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce1 = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        byte[] nonce2 = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);

        // Act
        using var ms1 = new MemoryStream();
        encryptor.EncryptAndWrite(ms1, plaintextMemory, key, nonce1);
        byte[] encrypted1 = ms1.ToArray();

        using var ms2 = new MemoryStream();
        encryptor.EncryptAndWrite(ms2, plaintextMemory, key, nonce2);
        byte[] encrypted2 = ms2.ToArray();

        // Assert - Different nonce should produce different ciphertext
        encrypted1[..plaintext.Length].ShouldNotBe(encrypted2[..plaintext.Length]);
    }

    [Fact]
    public void Encrypt_TotalLength_EqualsPlaintextPlusTagLength()
    {
        // Arrange
        MessageEncryptorV1 encryptor = GetSut();
        SessionData session = CreateSessionData("Test message for length verification");

        // Act
        int expectedLength = encryptor.GetEncryptedLength(session.Plaintext.Length);

        // Assert
        expectedLength.ShouldBe(
            session.Plaintext.Length + EncryptionConstants.TagLength,
            "TotalLength should equal ciphertext length plus tag length"
        );
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
        SessionData session = CreateSessionData(plaintext);

        // Act
        var (ciphertext, tag) = EncryptToBytes(encryptor, session);

        // Assert
        ciphertext.Length.ShouldBe(session.Plaintext.Length);
        byte[] decrypted = AesGcmDecrypt(ciphertext, session.AesKey, session.Nonce, tag);
        decrypted.ShouldBe(session.Plaintext.ToArray());
    }
}
