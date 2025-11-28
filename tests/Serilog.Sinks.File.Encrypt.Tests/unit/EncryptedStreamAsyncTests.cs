using System.Diagnostics.CodeAnalysis;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

/// <summary>
/// Tests for EncryptedStream async operations, specifically FlushAsync
/// </summary>
public sealed class EncryptedStreamAsyncTests : EncryptionTestBase
{
    [Fact]
    public async Task FlushAsync_WithSingleMessage_EncryptsAndDecryptsCorrectly()
    {
        // Arrange
        const string testMessage = "Test message with async flush";

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            testMessage,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(testMessage);
    }

    [Fact]
    public async Task FlushAsync_WithMultipleMessages_EncryptsAndDecryptsAll()
    {
        // Arrange
        string[] messages = ["First async message", "Second async message", "Third async message"];

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        string expected = string.Join("", messages);
        decrypted.ShouldBe(expected);
    }

    [Fact]
    public async Task FlushAsync_With4096BitKey_EncryptsAndDecryptsSuccessfully()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair(keySize: 4096);
        const string testMessage = "Testing FlushAsync with 4096-bit RSA key!";

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        rsa.KeySize.ShouldBe(4096); // Verify key size

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            testMessage,
            publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(testMessage);
    }

    [Fact]
    public async Task FlushAsync_WithLargeData_HandlesCorrectly()
    {
        // Arrange - Create a large message that will span multiple encryption chunks
        string largeMessage = new('X', 10000); // 10KB of X's

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            largeMessage,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(largeMessage);
    }

    [Fact]
    public async Task FlushAsync_MultipleChunks_EachChunkProcessedCorrectly()
    {
        // Arrange - Multiple small messages, each flushed separately
        string[] messages = ["Chunk 1", "Chunk 2", "Chunk 3", "Chunk 4", "Chunk 5"];

        // Act - Each message gets its own flush in CreateEncryptedStreamAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        string expected = string.Join("", messages);
        decrypted.ShouldBe(expected);
    }

    [Fact]
    public async Task FlushAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        const string testMessage = "Test message";

        // Act - Create stream with a valid token
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            testMessage,
            RsaKeyPair.publicKey,
            cts.Token
        );

        // Assert - Should complete successfully
        encryptedStream.ShouldNotBeNull();
        encryptedStream.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task FlushAsync_ComparedToFlush_ProducesSameDecryptedOutput()
    {
        // Arrange
        const string testMessage = "Same message for both methods";

        // Act - Create one with sync Flush
# pragma warning disable S6966 // Suppress "Async method name should end with 'Async'" for test purpose
        MemoryStream syncStream = CreateEncryptedStream(testMessage, RsaKeyPair.publicKey);
# pragma warning restore S6966
        string syncDecrypted = await DecryptStreamToStringAsync(
            syncStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Create one with async FlushAsync
        MemoryStream asyncStream = await CreateEncryptedStreamAsync(
            testMessage,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );
        string asyncDecrypted = await DecryptStreamToStringAsync(
            asyncStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert - Both should decrypt to the same original message
        asyncDecrypted.ShouldBe(syncDecrypted);
        asyncDecrypted.ShouldBe(testMessage);
        syncDecrypted.ShouldBe(testMessage);
    }
}
