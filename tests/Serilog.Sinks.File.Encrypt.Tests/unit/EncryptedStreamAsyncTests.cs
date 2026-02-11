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
        const string TestMessage = "Test message with async flush";

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            TestMessage,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(TestMessage);
    }

    [Fact]
    public async Task FlushAsync_WithMultipleMessages_EncryptsAndDecryptsAll()
    {
        // Arrange
        string[] messages = ["First async message", "Second async message", "Third async message"];

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

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
        const string TestMessage = "Testing FlushAsync with 4096-bit RSA key!";

        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        rsa.KeySize.ShouldBe(4096); // Verify key size

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            TestMessage,
            publicKey,
            TestContext.Current.CancellationToken
        );

        string decrypted = await DecryptStreamToStringAsync(
            encryptedStream,
            privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(TestMessage);
    }

    [Fact]
    public async Task FlushAsync_WithLargeData_HandlesCorrectly()
    {
        // Arrange - Create a large message that will span multiple encryption chunks
        string largeMessage = new('X', 10000); // 10KB of X's

        // Act - Use async stream creation which calls FlushAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(largeMessage);
        string decrypted = await DecryptStreamToStringAsync(encryptedStream);

        // Assert
        decrypted.ShouldBe(largeMessage);
    }

    [Fact]
    public async Task FlushAsync_MultipleChunks_EachChunkProcessedCorrectly()
    {
        // Arrange - Multiple small messages, each flushed separately
        string[] messages = ["Chunk 1", "Chunk 2", "Chunk 3", "Chunk 4", "Chunk 5"];

        // Act - Each message gets its own flush in CreateEncryptedStreamAsync
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

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
        const string TestMessage = "Test message";

        // Act - Create stream with a valid token
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            TestMessage,
            cancellationToken: cts.Token
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
        const string TestMessage = "Same message for both methods";

        // Act - Create one with sync Flush
# pragma warning disable S6966 // Suppress "Async method name should end with 'Async'" for test purpose
        MemoryStream syncStream = CreateEncryptedStream(TestMessage, RsaKeyPair.publicKey);
# pragma warning restore S6966
        string syncDecrypted = await DecryptStreamToStringAsync(syncStream);

        // Create one with async FlushAsync
        MemoryStream asyncStream = await CreateEncryptedStreamAsync(TestMessage);
        string asyncDecrypted = await DecryptStreamToStringAsync(asyncStream);

        // Assert - Both should decrypt to the same original message
        asyncDecrypted.ShouldBe(syncDecrypted);
        asyncDecrypted.ShouldBe(TestMessage);
        syncDecrypted.ShouldBe(TestMessage);
    }

    [Fact]
    public async Task WriteAsync_WritesDataAndFlushes()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        await using EncryptedStream encStream = new(fs, rsa);

        byte[] data = "Hello Async"u8.ToArray();

        // Act
        await encStream.WriteAsync(data, TestContext.Current.CancellationToken);
        await encStream.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        encStream.Position.ShouldBeGreaterThan(data.Length);
    }

    [Fact]
    public async Task WriteAsync_ZeroBytes_DoesNot_WriteData()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        await using EncryptedStream encStream = new(fs, rsa);

        // Act
        await encStream.WriteAsync(Array.Empty<byte>(), TestContext.Current.CancellationToken);
        await encStream.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        encStream.Position.ShouldBeEquivalentTo(0L);
    }

    [Fact]
    public async Task WriteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        (string publicKey, _) = EncryptionUtils.GenerateRsaKeyPair();
        using MemoryStream fs = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);
        await using EncryptedStream encStream = new(fs, rsa);

        byte[] data = "Hello Async"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await encStream.WriteAsync(data, cts.Token)
        );
    }
}
