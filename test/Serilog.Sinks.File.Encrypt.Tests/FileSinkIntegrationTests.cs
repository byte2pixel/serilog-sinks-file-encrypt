namespace Serilog.Sinks.File.Encrypt.Tests;

public sealed class FileSinkIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _logFilePath;
    private readonly (string publicKey, string privateKey) _rsaKeyPair;
    private bool _disposed;

    public FileSinkIntegrationTests()
    {
        // Create a unique test directory for each test
        _testDirectory = Path.Join(
            Path.GetTempPath(),
            "SerilogEncryptTests",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_testDirectory);
        _logFilePath = Path.Join(_testDirectory, "test.log");

        // Generate a key pair for testing
        _rsaKeyPair = EncryptionUtils.GenerateRsaKeyPair();
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources here
            try
            {
                // Clean up test files
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
        }

        // Dispose unmanaged resources here (none in this case)
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    [Fact]
    public void CanWriteEncryptedLogFile()
    {
        // Arrange
        const string logMessage = "This is a test log message";

        // Act - Configure and create a logger that writes encrypted logs
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Write a test message
        logger.Information(logMessage);

        // Dispose to ensure the log is written and file handle is released
        logger.Dispose();

        // Assert
        System.IO.File.Exists(_logFilePath).ShouldBeTrue();

        // The file should contain encrypted content (not plaintext)
        string fileContent = System.IO.File.ReadAllText(_logFilePath);
        fileContent.ShouldNotContain(logMessage);
    }

    [Fact]
    public async Task CanDecryptLogFileWithPrivateKey()
    {
        // Arrange
        const string logMessage = "This is a secret log message";

        // Create a logger that writes encrypted logs
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Write a test message
        logger.Information(logMessage);

        // Dispose to ensure the log is written and file handle is released
        await logger.DisposeAsync();

        logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();
        logger.Information("This is a second log message");
        await logger.DisposeAsync();

        // Act - Decrypt the log file
        await using FileStream inputStream = System.IO.File.OpenRead(_logFilePath);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldContain(logMessage);
    }

    [Fact]
    public async Task CanDecryptLogFileToFile_WithPrivateKey()
    {
        // Arrange
        const string logMessage = "This is a secret log message";
        string decryptedFilePath = Path.Join(_testDirectory, "decrypted.log");
        // Create a logger that writes encrypted logs
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();
        // Write a test message
        logger.Information(logMessage);
        // Dispose to ensure the log is written and file handle is released
        await logger.DisposeAsync();

        logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        logger.Information("This is a second log message");
        await logger.DisposeAsync();
        // Act - Decrypt the log file to a specified file
        await EncryptionUtils.DecryptLogFileToFileAsync(
            _logFilePath,
            decryptedFilePath,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );
        // Assert
        string decryptedContent = await System.IO.File.ReadAllTextAsync(
            decryptedFilePath,
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldContain(logMessage);
    }

    [Fact]
    public async Task DecryptionFailsWithWrongKey()
    {
        // Arrange
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        logger.Information("Secret data");
        await logger.DisposeAsync();

        // Generate a different key pair
        (string publicKey, string privateKey) differentKeyPair =
            EncryptionUtils.GenerateRsaKeyPair();

        // Act & Assert
        await using FileStream inputStream = System.IO.File.OpenRead(_logFilePath);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            differentKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string result = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        result.ShouldNotContain("Secret data");
    }

    [Fact]
    public async Task CanWriteMultipleLogMessages()
    {
        // Arrange
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Act - Write multiple messages
        for (int i = 1; i <= 10; i++)
        {
            logger.Information("Log message {MessageNumber}", i);
        }

        // Dispose to ensure logs are written
        await logger.DisposeAsync();

        // Decrypt and verify
        await using FileStream inputStream = System.IO.File.OpenRead(_logFilePath);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert - Check that all messages are present
        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );

        for (int i = 1; i <= 10; i++)
        {
            decryptedContent.ShouldContain($"Log message {i}");
        }
    }

    [Fact]
    public async Task CanAppendToEncryptedLogFile()
    {
        // Arrange
        const string firstMessage = "First log entry";
        const string secondMessage = "Second log entry";

        // Create logger and write first message
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new EncryptHooks(_rsaKeyPair.publicKey),
                rollingInterval: RollingInterval.Infinite
            )
            .CreateLogger();
        logger.Information(firstMessage);
        await logger.DisposeAsync();
        // Recreate logger to append second message
        logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new EncryptHooks(_rsaKeyPair.publicKey),
                rollingInterval: RollingInterval.Infinite
            )
            .CreateLogger();
        logger.Information(secondMessage);
        await logger.DisposeAsync();
        // Act - Decrypt the log file
        await using FileStream inputStream = System.IO.File.OpenRead(_logFilePath);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldContain(firstMessage);
        decryptedContent.ShouldContain(secondMessage);
    }

    [Fact]
    public async Task EncryptionWorksWithJsonFormatter()
    {
        // Arrange
        string logFilePath = Path.Join(_testDirectory, "json.log");
        const string testValue = "test-value";

        // Act - Configure logger with JSON formatter
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                new JsonFormatter(),
                logFilePath,
                hooks: new EncryptHooks(_rsaKeyPair.publicKey)
            )
            .CreateLogger();

        // Log message with properties
        logger.Information("Message with {Property}", testValue);
        await logger.DisposeAsync();

        // Assert
        await using FileStream inputStream = System.IO.File.OpenRead(logFilePath);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldContain(testValue);
        decryptedContent.ShouldContain("Property");
        decryptedContent.ShouldContain("Message with");
    }

    [Fact]
    public async Task EncryptionWorksWithRollingFiles()
    {
        // Arrange
        string fileNamePattern = Path.Join(_testDirectory, "rolling-{Date}.log");
        const string logMessage = "This is a rolling file test";

        // Act - Configure logger with rolling files
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: fileNamePattern,
                rollingInterval: RollingInterval.Day,
                hooks: new EncryptHooks(_rsaKeyPair.publicKey)
            )
            .CreateLogger();

        logger.Information(logMessage);
        await logger.DisposeAsync();

        // Assert - Find the log file that was created
        string[] logFiles = Directory.GetFiles(_testDirectory);
        logFiles.ShouldNotBeEmpty();

        // Verify it's encrypted
        await using FileStream inputStream = System.IO.File.OpenRead(logFiles[0]);
        using MemoryStream outputStream = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream,
            outputStream,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldContain(logMessage);
    }

    [Fact]
    public async Task CanEncryptFilesWithDifferentPublicKeys_ButNot_CrossDecrypt()
    {
        // Arrange
        string logFile1 = Path.Join(_testDirectory, "log1.log");
        string logFile2 = Path.Join(_testDirectory, "log2.log");

        // Generate a second key pair
        (string publicKey, string privateKey) secondKeyPair = EncryptionUtils.GenerateRsaKeyPair();

        // Act - Create two loggers with different encryption keys
        Logger logger1 = new LoggerConfiguration()
            .WriteTo.File(path: logFile1, hooks: new EncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        Logger logger2 = new LoggerConfiguration()
            .WriteTo.File(path: logFile2, hooks: new EncryptHooks(secondKeyPair.publicKey))
            .CreateLogger();

        // Write to both logs
        logger1.Information("Message for log 1");
        logger2.Information("Message for log 2");

        // Dispose both loggers
        await logger1.DisposeAsync();
        await logger2.DisposeAsync();

        // Assert - Decrypt with corresponding private keys
        await using FileStream inputStream1 = System.IO.File.OpenRead(logFile1);
        using MemoryStream outputStream1 = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream1,
            outputStream1,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        await using FileStream inputStream2 = System.IO.File.OpenRead(logFile2);
        using MemoryStream outputStream2 = new();
        await EncryptionUtils.DecryptLogFileAsync(
            inputStream2,
            outputStream2,
            secondKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        outputStream1.Position = 0;
        using StreamReader reader1 = new(outputStream1);
        string content1 = await reader1.ReadToEndAsync(TestContext.Current.CancellationToken);

        outputStream2.Position = 0;
        using StreamReader reader2 = new(outputStream2);
        string content2 = await reader2.ReadToEndAsync(TestContext.Current.CancellationToken);

        content1.ShouldContain("Message for log 1");
        content2.ShouldContain("Message for log 2");

        // Verify cross-decryption fails
        await using FileStream crossInputStream1 = System.IO.File.OpenRead(logFile1);
        using MemoryStream crossOutputStream1 = new();
        await EncryptionUtils.DecryptLogFileAsync(
            crossInputStream1,
            crossOutputStream1,
            secondKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        await using FileStream crossInputStream2 = System.IO.File.OpenRead(logFile2);
        using MemoryStream crossOutputStream2 = new();
        await EncryptionUtils.DecryptLogFileAsync(
            crossInputStream2,
            crossOutputStream2,
            _rsaKeyPair.privateKey,
            cancellationToken: TestContext.Current.CancellationToken
        );

        crossOutputStream1.Position = 0;
        using StreamReader crossReader1 = new(crossOutputStream1);
        string result1 = await crossReader1.ReadToEndAsync(TestContext.Current.CancellationToken);

        crossOutputStream2.Position = 0;
        using StreamReader crossReader2 = new(crossOutputStream2);
        string result2 = await crossReader2.ReadToEndAsync(TestContext.Current.CancellationToken);

        result1.ShouldContain("[Decryption error at position");
        result2.ShouldContain("[Decryption error at position");
    }
}
