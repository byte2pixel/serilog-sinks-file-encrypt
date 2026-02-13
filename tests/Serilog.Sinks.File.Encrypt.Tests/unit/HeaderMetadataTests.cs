using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class HeaderMetadataTests
{
    [Fact]
    public void CreateV1_ReturnsCorrectVersion()
    {
        // Act
        var metadata = HeaderMetadata.CreateV1();

        // Assert
        metadata.Version.ShouldBe(1);
    }

    [Fact]
    public void CreateV1_ReturnsCorrectFixedOverhead()
    {
        // Act
        var metadata = HeaderMetadata.CreateV1();

        // Assert - V1 format: 1 (KeyId len) + 1 (AES len) + 32 (AES) + 1 (nonce len) + 12 (nonce) + 8 (timestamp)
        metadata.FixedOverheadBytes.ShouldBe(55);
    }

    [Fact]
    public void CreateV1_ReturnsOaepSHA256Padding()
    {
        // Act
        var metadata = HeaderMetadata.CreateV1();

        // Assert
        metadata.Padding.ShouldBe(RSAEncryptionPadding.OaepSHA256);
    }

    [Fact]
    public void CreateV1_ReturnsReservedBuffer()
    {
        // Act
        var metadata = HeaderMetadata.CreateV1();

        // Assert
        metadata.ReservedBufferBytes.ShouldBe(16);
    }

    [Fact]
    public void CreateV1_ReturnsFieldDescription()
    {
        // Act
        var metadata = HeaderMetadata.CreateV1();

        // Assert
        metadata.FieldDescription.ShouldNotBeNullOrEmpty();
        metadata.FieldDescription.ShouldContain("KeyId");
        metadata.FieldDescription.ShouldContain("AES");
        metadata.FieldDescription.ShouldContain("Nonce");
        metadata.FieldDescription.ShouldContain("Timestamp");
    }

    [Theory]
    [InlineData(2048, 119)] // 190 - 55 - 16 = 119 bytes
    [InlineData(4096, 375)] // 446 - 55 - 16 = 375 bytes
    public void GetMaxVariableFieldSize_ReturnsCorrectSize(int keySize, int expectedMaxSize)
    {
        // Arrange
        var metadata = HeaderMetadata.CreateV1();

        // Act
        int maxSize = metadata.GetMaxVariableFieldSize(keySize);

        // Assert
        maxSize.ShouldBe(expectedMaxSize);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void GetMaxVariableFieldSize_MaxSizeIsValid_CanEncryptWithRealRsa(int keySize)
    {
        // Arrange
        using RSA rsa = RSA.Create(keySize);
        var metadata = HeaderMetadata.CreateV1();
        int maxKeyIdSize = metadata.GetMaxVariableFieldSize(keySize);

        // Build a worst-case header payload (max size KeyId + fixed fields)
        byte[] worstCasePayload = BuildWorstCaseV1HeaderPayload(maxKeyIdSize);

        // Act & Assert - Should not throw
        Should.NotThrow(() => rsa.Encrypt(worstCasePayload, metadata.Padding));
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void GetMaxVariableFieldSize_ExceedingMaxSize_ThrowsCryptographicException(int keySize)
    {
        // Arrange
        using RSA rsa = RSA.Create(keySize);
        var metadata = HeaderMetadata.CreateV1();
        int maxKeyIdSize = metadata.GetMaxVariableFieldSize(keySize);

        // Build an oversized payload - add enough bytes to exceed RSA limits
        // Adding just 1 byte may not trigger since we have reserved buffer
        byte[] oversizedPayload = BuildWorstCaseV1HeaderPayload(
            maxKeyIdSize + metadata.ReservedBufferBytes + 1
        );

        // Act & Assert - Should throw CryptographicException
        Should.Throw<CryptographicException>(() => rsa.Encrypt(oversizedPayload, metadata.Padding));
    }

    [Fact]
    public void GetMaxVariableFieldSize_WithSmallKey_ReturnsZeroIfInsufficientSpace()
    {
        // Arrange
        var metadata = new HeaderMetadata
        {
            FixedOverheadBytes = 200,
            ReservedBufferBytes = 50,
            Padding = RSAEncryptionPadding.OaepSHA256,
        };

        // Act - 2048-bit key with OAEP-SHA256 = 190 bytes max, but we need 250
        int maxSize = metadata.GetMaxVariableFieldSize(2048);

        // Assert - Should return 0, not negative
        maxSize.ShouldBe(0);
    }

    /// <summary>
    /// Builds a worst-case V1 header payload for testing RSA encryption limits.
    /// Format: KeyIdLen(1) + KeyId(var) + AESLen(1) + AESKey(32) + NonceLen(1) + Nonce(12) + Timestamp(8)
    /// </summary>
    private static byte[] BuildWorstCaseV1HeaderPayload(int keyIdSize)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // KeyId length + KeyId
        byte[] keyId = new byte[keyIdSize];
        Array.Fill(keyId, (byte)'A');
        bw.Write((byte)keyId.Length);
        bw.Write(keyId);

        // AES key length + AES key (32 bytes)
        byte[] aesKey = RandomNumberGenerator.GetBytes(32);
        bw.Write((byte)aesKey.Length);
        bw.Write(aesKey);

        // Nonce length + Nonce (12 bytes)
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        bw.Write((byte)nonce.Length);
        bw.Write(nonce);

        // Timestamp (8 bytes)
        bw.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        return ms.ToArray();
    }
}
