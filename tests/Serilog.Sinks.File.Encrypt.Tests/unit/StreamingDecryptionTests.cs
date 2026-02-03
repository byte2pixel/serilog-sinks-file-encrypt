using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public sealed class StreamingDecryptionTests : EncryptionTestBase
{
    [Fact]
    public async Task DecryptLogFileAsync_WithSingleMessage_ReturnsCorrectContent()
    {
        // Arrange & Act
        const string TestMessage = "Test log message";
        string result = await EncryptAndDecryptAsync(
            TestMessage,
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TestMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithMultipleMessages_ReturnsAllContent()
    {
        // Arrange
        string[] messages = ["First message", "Second message", "Third message"];

        // Act
        string result = await EncryptAndDecryptAsync(
            messages,
            RsaKeyPair.publicKey,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCustomStreamingOptions_RespectsConfiguration()
    {
        // Arrange
        const string TestMessage = "Test message with custom options";
        StreamingOptions customOptions = new()
        {
            BufferSize = 8 * 1024, // 8KB
            QueueDepth = 5,
            ContinueOnError = false,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            TestMessage,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Act
        string result = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            customOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TestMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCorruptedData_ContinuesOnError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        // Corrupt the second message part
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(
            corruptedStream,
            RsaKeyPair.privateKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldBe("Good message 1");
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithSkipErrorMode_SilentlySkipsCorruptedData()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        StreamingOptions skipErrorOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        // Corrupt the second message part
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(
            corruptedStream,
            RsaKeyPair.privateKey,
            skipErrorOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldBe("Good message 1");
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteInlineErrorMode_WritesErrorInline()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        StreamingOptions writeInlineOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteInline,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(
            corruptedStream,
            RsaKeyPair.privateKey,
            writeInlineOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldContain("[DECRYPTION ERROR");
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteToErrorLogMode_WritesErrorsToSeparateLog()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string errorLogPath = Path.GetTempFileName();
        StreamingOptions errorLogOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
            ErrorLogPath = errorLogPath,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act
        string result = await DecryptStreamToStringAsync(
            corruptedStream,
            RsaKeyPair.privateKey,
            errorLogOptions,
            TestContext.Current.CancellationToken
        );

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
        StreamingOptions throwExceptionOptions = new()
        {
            ContinueOnError = false,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Corrupt part of the stream
        byte[] fileBytes = encryptedStream.ToArray();
        byte[] corrupted = CorruptData(fileBytes, fileBytes.Length / 2);
        MemoryStream corruptedStream = CreateMemoryStream(corrupted);

        // Act & Assert
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await DecryptStreamToStringAsync(
                corruptedStream,
                RsaKeyPair.privateKey,
                throwExceptionOptions,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithNoErrors_SuccessfullyDecryptsAll()
    {
        // Arrange
        string[] messages = ["Message 1", "Message 2", "Message 3"];
        StreamingOptions throwExceptionOptions = new()
        {
            ContinueOnError = false,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        MemoryStream encryptedStream = await CreateEncryptedStreamAsync(
            messages,
            RsaKeyPair.publicKey,
            TestContext.Current.CancellationToken
        );

        // Act - should succeed without throwing
        string result = await DecryptStreamToStringAsync(
            encryptedStream,
            RsaKeyPair.privateKey,
            throwExceptionOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }
}
