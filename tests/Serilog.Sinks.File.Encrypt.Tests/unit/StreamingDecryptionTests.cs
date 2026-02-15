using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

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
            DecryptionKeys = _rsaKeyMap,
            BufferSize = 8 * 1024, // 8KB
            QueueDepth = 5,
            ContinueOnError = false,
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
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
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
    [InlineData(299)] // Corrupt the length so it is a marker, but the data is still intact
    [InlineData(300)] // Corrupt part of the length and data.
    [InlineData(301)] // Corrupt part of the length and data.
    [InlineData(302)] // Corrupt part of the length and data.
    [InlineData(303)] // Corrupt the data
    [InlineData(305)] // Corrupt the data more.
    [InlineData(310)] // Corrupt the data and more.
    [InlineData(320)] // Corrupt the data and more and more.
    public async Task DecryptLogFileAsync_WithCorruptedMessageLength_ContinuesOnError(
        int corruptionOffset
    )
    {
        // Arrange - Do not change message sizes or the marker will not be in the correct offsets for each test.
        string[] messages = ["Good message 1\n", "Good message 2\n"];

        // create a session with 2 messages.
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Append a new session with a message.
        encryptedStream = await CreateAppendedMemoryStream(
            encryptedStream,
            "Appended message",
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the first session with a marker that corrupts the length of the 2nd message.
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptDataAddingMarker(
            fileBytes,
            EncryptionConstants.Marker,
            corruptionOffset
        );
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream);

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
            DecryptionKeys = _rsaKeyMap,
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream, skipErrorOptions);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteInlineErrorMode_WritesErrorInline()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        DecryptionOptions writeInlineOptions = new()
        {
            DecryptionKeys = _rsaKeyMap,
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteInline,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream, writeInlineOptions);

        // Assert
        result.ShouldContain("[DECRYPTION ERROR");
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteToErrorLogMode_WritesErrorsToSeparateLog()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string errorLogPath = Path.GetTempFileName();
        DecryptionOptions errorLogOptions = new()
        {
            DecryptionKeys = _rsaKeyMap,
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
            ErrorLogPath = errorLogPath,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(corruptedStream, errorLogOptions);

        // Assert
        result.ShouldBeEmpty();

        // Error log file should exist (in real file system for this test)
        string fileContents = await System.IO.File.ReadAllTextAsync(
            errorLogPath,
            TestContext.Current.CancellationToken
        );
        fileContents.ShouldContain("DECRYPTION ERROR");
        try
        {
            System.IO.File.Delete(errorLogPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithThrowExceptionMode_ThrowsOnFirstError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        DecryptionOptions throwExceptionOptions = new()
        {
            DecryptionKeys = _rsaKeyMap,
            ContinueOnError = false,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(messages);

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await DecryptStreamToStringAsync(corruptedStream, throwExceptionOptions)
        );
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithNoErrors_SuccessfullyDecryptsAll()
    {
        // Arrange
        string[] messages = ["Message 1", "Message 2", "Message 3"];
        DecryptionOptions throwExceptionOptions = new()
        {
            DecryptionKeys = _rsaKeyMap,
            ContinueOnError = false,
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
