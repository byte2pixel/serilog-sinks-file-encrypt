using System.Buffers.Binary;

namespace Serilog.Sinks.File.Decrypt.Tests;

public sealed class StreamingDecryptionTests : EncryptionTestBase
{
    [Fact]
    public async Task DecryptLogFileAsync_WithSingleMessage_ReturnsCorrectContent()
    {
        // Arrange & Act
        const string TestMessage = "Test log message";
        string result = await EncryptAndDecryptAsync(TestMessage);

        // Assert
        Assert.Equal(TestMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithMultipleMessages_ReturnsAllContent()
    {
        // Arrange
        string[] messages = ["First message", "Second message", "Third message"];

        // Act
        string result = await EncryptAndDecryptAsync(messages);

        // Assert
        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCustomStreamingOptions_RespectsConfiguration()
    {
        // Arrange
        const string TestMessage = "Test message with custom options";
        DecryptionOptions customOptions = new()
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(TestMessage);

        // Act
        string result = await DecryptStreamToStringAsync(encryptedStream, customOptions);

        // Assert
        Assert.Equal(TestMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCorruptedData_ContinuesOnError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = TestUtils.CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream);

        // Assert
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// Special case test where corruption introduces a marker in parts of that data for the 2nd message.
    /// and ensures decryption continues past it.
    /// </summary>
    [Theory]
    [InlineData(332)] // Corrupt the length so it is a marker and part of the data.
    [InlineData(333)] // Corrupt part of the length and data.
    [InlineData(334)] // Corrupt part of the length and data.
    [InlineData(335)] // Corrupt part of the length and data.
    [InlineData(336)] // Corrupt the data
    [InlineData(337)] // Corrupt the data more.
    [InlineData(338)] // Corrupt the data and more.
    [InlineData(339)] // Corrupt the data and more and more.
    public async Task DecryptLogFileAsync_WithCorruptedMessageLength_ContinuesOnError(
        int corruptionOffset
    )
    {
        // Arrange - Do not change message sizes or the marker will not be in the correct offsets for each test.
        string[] messages = ["Good message 1\n", "Good message 2\n"];
        using LocalKeyProvider keyProvider = new("", RsaKeyPair.privateKey);
        DecryptionOptions decryptionOptions = new() { KeyProvider = keyProvider };
        // create a session with 2 messages.
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Append a new session with a message.
        encryptedStream = await CreateAppendedMemoryStream(encryptedStream, "Appended message");

        // Corrupt part of the first session with a marker that corrupts the length of the 2nd message.
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = TestUtils.CorruptDataAddingMarker(
            fileBytes,
            CryptographicUtils.MagicBytes,
            corruptionOffset
        );
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream, decryptionOptions);

        // Assert we still got message 1 and message 2
        result.ShouldBe("Good message 1\nAppended message");
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithSkipErrorMode_SilentlySkipsCorruptedData()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        DecryptionOptions skipErrorOptions = new()
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = TestUtils.CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream, skipErrorOptions);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteToErrorLogMode_WritesErrorsToSeparateLog()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        DecryptionOptions errorLogOptions = new() { KeyProvider = DecryptOptions.KeyProvider };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = TestUtils.CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(
            corruptedStream,
            errorLogOptions,
            logger: Log
        );

        // Assert
        result.ShouldBeEmpty();

        Log.Received(1)
            .Error(
                Arg.Any<CryptographicException>(),
                Arg.Is<string>(x =>
                    x.Contains("Cryptographic error encountered while processing header")
                )
            );
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithThrowExceptionMode_ThrowsOnFirstError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        DecryptionOptions throwExceptionOptions = new()
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = TestUtils.CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(() =>
            DecryptStreamToStringAsync(corruptedStream, throwExceptionOptions)
        );
    }

    [Theory]
    [InlineData(int.MaxValue)] // Huge positive length - previously OutOfMemoryException / large allocation
    [InlineData(-100)] // Negative length (not the magic marker) - previously ArgumentOutOfRangeException
    [InlineData(0)] // Zero length
    [InlineData(EncryptionConstants.TagLength - 1)] // Too short to hold the authentication tag
    public async Task DecryptLogFileAsync_WithCorruptMessageLengthPrefix_SkipMode_DoesNotCrash(
        int corruptLength
    )
    {
        // Arrange - a single-message session so the corrupt prefix is the only frame
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync("Only message");
        byte[] fileBytes = encryptedStream.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(
            fileBytes.AsSpan(GetFirstMessageLengthOffset(), sizeof(int)),
            corruptLength
        );
        MemoryStream corruptedStream = CreateMemoryStream(fileBytes);

        // Act - default Skip mode should resync past the corrupt frame instead of crashing
        string result = await DecryptStreamToStringAsync(corruptedStream);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCorruptMessageLengthPrefix_ThrowMode_ThrowsInvalidData()
    {
        // Arrange
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync("Only message");
        byte[] fileBytes = encryptedStream.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(
            fileBytes.AsSpan(GetFirstMessageLengthOffset(), sizeof(int)),
            int.MaxValue
        );
        MemoryStream corruptedStream = CreateMemoryStream(fileBytes);
        DecryptionOptions throwOptions = new()
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        // Act & Assert - a bounded, typed failure instead of an unhandled crash
        await Should.ThrowAsync<InvalidDataException>(() =>
            DecryptStreamToStringAsync(corruptedStream, throwOptions)
        );
    }

    /// <summary>
    /// Offset of the first message's 4-byte length prefix: magic bytes + version byte + keyId + RSA header.
    /// </summary>
    private int GetFirstMessageLengthOffset()
    {
        using RSA rsa = RSA.Create();
        rsa.FromString(RsaKeyPair.publicKey);
        return CryptographicUtils.MagicBytes.Length
            + 1
            + HeaderMetadata.KeyIdLength
            + rsa.KeySize / 8;
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithNoErrors_SuccessfullyDecryptsAll()
    {
        // Arrange
        string[] messages = ["Message 1", "Message 2", "Message 3"];
        DecryptionOptions throwExceptionOptions = new()
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Act - should succeed without throwing
        string result = await DecryptStreamToStringAsync(encryptedStream, throwExceptionOptions);

        // Assert
        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }
}
