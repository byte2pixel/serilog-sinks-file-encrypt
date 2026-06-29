namespace Serilog.Sinks.File.Decrypt.Tests;

/// <summary>
/// Tests for <see cref="LogReader.DecryptToStreamAsync"/> covering error paths and result variants
/// that are not exercised by the integration tests.
/// </summary>
public sealed class LogReaderDecryptToStreamTests : EncryptionTestBase
{
    #region ThrowException mode — no valid sessions

    [Fact]
    public async Task DecryptToStreamAsync_ThrowException_EmptyStream_ThrowsNoValidSessions()
    {
        // Arrange
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };
        using var reader = new LogReader(input, options);

        // Act & Assert
        var ex = await Should.ThrowAsync<CryptographicException>(() =>
            reader.DecryptToStreamAsync(output, TestContext.Current.CancellationToken)
        );
        ex.Message.ShouldContain("No valid sessions found in the file.");
    }

    #endregion

    #region ThrowException mode — session present but zero messages

    [Fact]
    public async Task DecryptToStreamAsync_ThrowException_SessionWithNoMessages_ThrowsNoMessagesDecrypted()
    {
        // Arrange — build a valid session header, then discard the message bytes
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync("placeholder");
        byte[] allBytes = encryptedStream.ToArray();

        // Session header layout: magic(N) + version(1) + keyId(32) + rsaPayload(keyBits/8)
        int keyBits = await DecryptOptions.KeyProvider.GetKeySizeAsync(
            "",
            TestContext.Current.CancellationToken
        );
        int sessionHeaderSize =
            CryptographicUtils.MagicBytes.Length + 1 + HeaderMetadata.KeyIdLength + (keyBits / 8);

        MemoryStream headerOnly = CreateMemoryStream(allBytes[..sessionHeaderSize]);
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };
        using var reader = new LogReader(headerOnly, options);

        // Act & Assert
        var ex = await Should.ThrowAsync<CryptographicException>(() =>
            reader.DecryptToStreamAsync(output, TestContext.Current.CancellationToken)
        );
        ex.Message.ShouldContain("No messages were decrypted from the input stream.");
    }

    #endregion

    #region ThrowException mode — successful run returns DecryptionResult

    [Fact]
    public async Task DecryptToStreamAsync_ThrowException_ValidStream_ReturnsDecryptionResult()
    {
        // Arrange
        const string TestMessage = "hello world";
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(TestMessage);
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };
        using var reader = new LogReader(encryptedStream, options);

        // Act
        DecryptionResult result = await reader.DecryptToStreamAsync(
            output,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.DecryptedSessions.ShouldBe(1);
        result.DecryptedMessages.ShouldBe(1);
        result.FailedHeaders.ShouldBe(0);
        result.FailedMessages.ShouldBe(0);
        result.ResyncAttempts.ShouldBe(0);

        output.Position = 0;
        using var sr = new System.IO.StreamReader(output, leaveOpen: true);
        (await sr.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe(TestMessage);
    }

    #endregion

    #region Skip mode — corrupted message increments failure counters

    [Fact]
    public async Task DecryptToStreamAsync_Skip_CorruptedMessage_ReturnsResultWithFailureCounts()
    {
        // Arrange — build a valid stream then corrupt the message ciphertext
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync("some log entry");
        byte[] bytes = encryptedStream.ToArray();

        int keyBits = await DecryptOptions.KeyProvider.GetKeySizeAsync(
            "",
            TestContext.Current.CancellationToken
        );
        int sessionHeaderSize =
            CryptographicUtils.MagicBytes.Length + 1 + HeaderMetadata.KeyIdLength + (keyBits / 8);

        // Corrupt a byte inside the message ciphertext (after header + 4-byte length prefix)
        byte[] corrupted = TestUtils.CorruptData(bytes, sessionHeaderSize + 4 + 5);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };
        using var reader = new LogReader(corruptedStream, options);

        // Act
        DecryptionResult result = await reader.DecryptToStreamAsync(
            output,
            TestContext.Current.CancellationToken
        );

        // Assert — error recovery must have fired
        result.FailedMessages.ShouldBeGreaterThan(0);
        result.ResyncAttempts.ShouldBeGreaterThan(0);
    }

    #endregion
}
