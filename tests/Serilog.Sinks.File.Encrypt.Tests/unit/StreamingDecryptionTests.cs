using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public sealed class StreamingDecryptionTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly (string publicKey, string privateKey) _rsaKeyPair;
    private bool _disposed;

    public StreamingDecryptionTests()
    {
        _testDirectory = Path.Join(
            Path.GetTempPath(),
            "StreamingEncryptTests",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_testDirectory);
        _rsaKeyPair = EncryptionUtils.GenerateRsaKeyPair();
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithSingleMessage_ReturnsCorrectContent()
    {
        // Arrange
        const string testMessage = "Test log message";
        string encryptedFile = Path.Join(_testDirectory, "encrypted.log");

        // Create encrypted file
        WriteEncryptedLogMessage(encryptedFile, testMessage, _rsaKeyPair.publicKey);

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(testMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithMultipleMessages_ReturnsAllContent()
    {
        // Arrange
        string[] messages = ["First message", "Second message", "Third message"];
        string encryptedFile = Path.Join(_testDirectory, "encrypted.log");

        // Create encrypted file with multiple messages
        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCustomStreamingOptions_RespectsConfiguration()
    {
        // Arrange
        const string testMessage = "Test message with custom options";
        string encryptedFile = Path.Join(_testDirectory, "encrypted.log");
        StreamingOptions customOptions = new()
        {
            BufferSize = 8 * 1024, // 8KB
            QueueDepth = 5,
            ContinueOnError = false,
        };

        WriteEncryptedLogMessage(encryptedFile, testMessage, _rsaKeyPair.publicKey);

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            customOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(testMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileToFileAsync_WorksWithFilePaths()
    {
        // Arrange
        const string testMessage = "Test message for utils";
        string encryptedFile = Path.Join(_testDirectory, "encrypted.log");
        string outputFile = Path.Join(_testDirectory, "decrypted.log");

        WriteEncryptedLogMessage(encryptedFile, testMessage, _rsaKeyPair.publicKey);

        // Act
        await EncryptionUtils.DecryptLogFileToFileAsync(
            encryptedFile,
            outputFile,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        string result = await System.IO.File.ReadAllTextAsync(
            outputFile,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(testMessage, result);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithCorruptedData_ContinuesOnError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string encryptedFile = Path.Join(_testDirectory, "corrupted.log");

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Corrupt part of the file
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(
            encryptedFile,
            TestContext.Current.CancellationToken
        );
        fileBytes[fileBytes.Length / 2] ^= 0xFF; // Flip some bits
        await System.IO.File.WriteAllBytesAsync(
            encryptedFile,
            fileBytes,
            TestContext.Current.CancellationToken
        );

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert - should contain error markers and any recoverable content
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithSkipErrorMode_SilentlySkipsCorruptedData()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string encryptedFile = Path.Join(_testDirectory, "corrupted_skip.log");
        StreamingOptions skipErrorOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Corrupt part of the file
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(
            encryptedFile,
            TestContext.Current.CancellationToken
        );
        fileBytes[fileBytes.Length / 2] ^= 0xFF; // Flip some bits
        await System.IO.File.WriteAllBytesAsync(
            encryptedFile,
            fileBytes,
            TestContext.Current.CancellationToken
        );

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            skipErrorOptions,
            TestContext.Current.CancellationToken
        );

        // Assert - should NOT contain error markers, only successfully decrypted content
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("[Decryption error", result);
        Assert.DoesNotContain("error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteToErrorLogMode_WritesErrorsToSeparateFile()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string encryptedFile = Path.Join(_testDirectory, "corrupted_errorlog.log");
        string errorLogFile = Path.Join(_testDirectory, "decryption_errors.log");
        StreamingOptions errorLogOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
            ErrorLogPath = errorLogFile,
        };

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Corrupt part of the file
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(
            encryptedFile,
            TestContext.Current.CancellationToken
        );
        fileBytes[fileBytes.Length / 2] ^= 0xFF; // Flip some bits
        await System.IO.File.WriteAllBytesAsync(
            encryptedFile,
            fileBytes,
            TestContext.Current.CancellationToken
        );

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            errorLogOptions,
            TestContext.Current.CancellationToken
        );

        // Assert - decrypted output should NOT contain error markers
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("[Decryption error", result);

        // Assert - error log file should exist and contain error information
        Assert.True(System.IO.File.Exists(errorLogFile), "Error log file should be created");
        string errorLogContent = await System.IO.File.ReadAllTextAsync(
            errorLogFile,
            TestContext.Current.CancellationToken
        );
        Assert.Contains("Decryption error", errorLogContent);
        Assert.Contains("position", errorLogContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithWriteToErrorLogMode_GeneratesDefaultErrorLogPath()
    {
        // Arrange
        string[] messages = ["Good message"];
        string encryptedFile = Path.Join(_testDirectory, "test.log");
        string errorLogPath = Path.Join(_testDirectory, "generated_errors.log");
        StreamingOptions errorLogOptions = new()
        {
            ContinueOnError = true,
            ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
            ErrorLogPath = errorLogPath, // Use test directory so Dispose handles cleanup
        };

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Corrupt the file
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(
            encryptedFile,
            TestContext.Current.CancellationToken
        );
        fileBytes[fileBytes.Length / 2] ^= 0xFF;
        await System.IO.File.WriteAllBytesAsync(
            encryptedFile,
            fileBytes,
            TestContext.Current.CancellationToken
        );

        // Act
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            errorLogOptions,
            TestContext.Current.CancellationToken
        );

        // Assert - decrypted output should NOT contain error markers
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("[Decryption error", result);

        // Assert - error log file should be created
        Assert.True(System.IO.File.Exists(errorLogPath), "Error log file should be created");
        string errorLogContent = await System.IO.File.ReadAllTextAsync(
            errorLogPath,
            TestContext.Current.CancellationToken
        );
        Assert.Contains("Decryption error", errorLogContent);
        Assert.Contains("position", errorLogContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithThrowExceptionMode_ThrowsOnFirstError()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string encryptedFile = Path.Join(_testDirectory, "corrupted_throw.log");
        StreamingOptions throwExceptionOptions = new()
        {
            ContinueOnError = false,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Corrupt part of the file
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(
            encryptedFile,
            TestContext.Current.CancellationToken
        );
        fileBytes[fileBytes.Length / 2] ^= 0xFF; // Flip some bits
        await System.IO.File.WriteAllBytesAsync(
            encryptedFile,
            fileBytes,
            TestContext.Current.CancellationToken
        );

        // Act & Assert
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        CryptographicException exception = await Assert.ThrowsAsync<CryptographicException>(
            async () =>
                await EncryptionUtils.DecryptLogFileAsync(
                    inputStream,
                    outputStream,
                    _rsaKeyPair.privateKey,
                    throwExceptionOptions,
                    TestContext.Current.CancellationToken
                )
        );

        Assert.Contains("Decryption failed", exception.Message);
    }

    [Fact]
    public async Task DecryptLogFileAsync_WithThrowExceptionModeAndNoErrors_SucceedsNormally()
    {
        // Arrange
        string[] messages = ["Good message 1", "Good message 2"];
        string encryptedFile = Path.Join(_testDirectory, "valid_throw.log");
        StreamingOptions throwExceptionOptions = new()
        {
            ContinueOnError = false,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        WriteEncryptedLogMessages(encryptedFile, messages, _rsaKeyPair.publicKey);

        // Act - should succeed without throwing
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFile);
        using MemoryStream outputStream = new();

        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            throwExceptionOptions,
            TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader streamReader = new(outputStream);
        string result = await streamReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        string expected = string.Join("", messages);
        Assert.Equal(expected, result);
    }

    private static void WriteEncryptedLogMessage(string filePath, string message, string publicKey)
    {
        WriteEncryptedLogMessages(filePath, [message], publicKey);
    }

    private static void WriteEncryptedLogMessages(
        string filePath,
        string[] messages,
        string publicKey
    )
    {
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(publicKey);

        using FileStream fileStream = System.IO.File.Create(filePath);
        using EncryptedStream encryptedStream = new(fileStream, rsa);

        foreach (byte[] message in messages.Select(m => Encoding.UTF8.GetBytes(m)))
        {
            encryptedStream.Write(message, 0, message.Length);
            encryptedStream.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch (IOException)
        {
            // Directory may be locked by another process - acceptable in test cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // May not have permissions - acceptable in test cleanup
        }

        _disposed = true;
    }
}
