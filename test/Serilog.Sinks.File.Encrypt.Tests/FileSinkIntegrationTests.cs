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
        _testDirectory = Path.Combine(Path.GetTempPath(), "SerilogEncryptTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _logFilePath = Path.Combine(_testDirectory, "test.log");

        // Generate a key pair for testing
        _rsaKeyPair = EncryptionUtils.GenerateRsaKeyPair();
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

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
            catch
            {
                // Ignore cleanup errors
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
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
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
    public void CanDecryptLogFileWithPrivateKey()
    {
        // Arrange
        const string logMessage = "This is a secret log message";

        // Create a logger that writes encrypted logs
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Write a test message
        logger.Information(logMessage);

        // Dispose to ensure the log is written and file handle is released
        logger.Dispose();

        logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();
        logger.Information("This is a second log message");
        logger.Dispose();

        // Act - Decrypt the log file
        string decryptedContent = EncryptionUtils.DecryptLogFile(_logFilePath, _rsaKeyPair.privateKey);

        // Assert
        decryptedContent.ShouldContain(logMessage);
    }

    [Fact]
    public void CanDecryptLogFileToFile_WithPrivateKey()
    {
        // Arrange
        const string logMessage = "This is a secret log message";
        string decryptedFilePath = Path.Combine(_testDirectory, "decrypted.log");
        // Create a logger that writes encrypted logs
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();
        // Write a test message
        logger.Information(logMessage);
        // Dispose to ensure the log is written and file handle is released
        logger.Dispose();

        logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        logger.Information("This is a second log message");
        logger.Dispose();
        // Act - Decrypt the log file to a specified file
        EncryptionUtils.DecryptLogFileToFile(_logFilePath, decryptedFilePath, _rsaKeyPair.privateKey);
        // Assert
        string decryptedContent = System.IO.File.ReadAllText(decryptedFilePath);
        decryptedContent.ShouldContain(logMessage);
    }

    [Fact]
    public void DecryptionFailsWithWrongKey()
    {
        // Arrange
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        logger.Information("Secret data");
        logger.Dispose();

        // Generate a different key pair
        (string publicKey, string privateKey) differentKeyPair = EncryptionUtils.GenerateRsaKeyPair();

        // Act & Assert
        string result = EncryptionUtils.DecryptLogFile(_logFilePath, differentKeyPair.privateKey);
        result.ShouldNotContain("Secret data");
    }

    [Fact]
    public void CanWriteMultipleLogMessages()
    {
        // Arrange
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Act - Write multiple messages
        for (int i = 1; i <= 10; i++)
        {
            logger.Information("Log message {MessageNumber}", i);
        }

        // Dispose to ensure logs are written
        logger.Dispose();

        // Decrypt and verify
        string decryptedContent = EncryptionUtils.DecryptLogFile(_logFilePath, _rsaKeyPair.privateKey);

        // Assert - Check that all messages are present
        for (int i = 1; i <= 10; i++)
        {
            decryptedContent.ShouldContain($"Log message {i}");
        }
    }

    [Fact]
    public void CanAppendToEncryptedLogFile()
    {
        // Arrange
        const string firstMessage = "First log entry";
        const string secondMessage = "Second log entry";

        // Create logger and write first message
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey),
                rollingInterval: RollingInterval.Infinite).CreateLogger();
        logger.Information(firstMessage);
        logger.Dispose();
        // Recreate logger to append second message
        logger = new LoggerConfiguration()
            .WriteTo.File(
                path: _logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey),
                rollingInterval: RollingInterval.Infinite).CreateLogger();
        logger.Information(secondMessage);
        logger.Dispose();
        // Act - Decrypt the log file
        string decryptedContent = EncryptionUtils.DecryptLogFile(_logFilePath, _rsaKeyPair.privateKey);
        // Assert
        decryptedContent.ShouldContain(firstMessage);
        decryptedContent.ShouldContain(secondMessage);
    }
    
    [Fact]
    public void EncryptionWorksWithJsonFormatter()
    {
        // Arrange
        string logFilePath = Path.Combine(_testDirectory, "json.log");
        const string testValue = "test-value";

        // Act - Configure logger with JSON formatter
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                new JsonFormatter(),
                logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Log message with properties
        logger.Information("Message with {Property}", testValue);
        logger.Dispose();

        // Assert
        string decryptedContent = EncryptionUtils.DecryptLogFile(logFilePath, _rsaKeyPair.privateKey);
        decryptedContent.ShouldContain(testValue);
        decryptedContent.ShouldContain("Property");
        decryptedContent.ShouldContain("Message with");
    }
    
    [Fact]
    public void EncryptionWorksWithRollingFiles()
    {
        // Arrange
        string fileNamePattern = Path.Combine(_testDirectory, "rolling-{Date}.log");
        const string logMessage = "This is a rolling file test";

        // Act - Configure logger with rolling files
        Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: fileNamePattern,
                rollingInterval: RollingInterval.Day,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        logger.Information(logMessage);
        logger.Dispose();

        // Assert - Find the log file that was created
        string[] logFiles = Directory.GetFiles(_testDirectory);
        logFiles.ShouldNotBeEmpty();

        // Verify it's encrypted
        string decryptedContent = EncryptionUtils.DecryptLogFile(logFiles[0], _rsaKeyPair.privateKey);
        decryptedContent.ShouldContain(logMessage);
    }
    
    [Fact]
    public void CanEncryptFilesWithDifferentPublicKeys_ButNot_CrossDecrypt()
    {
        // Arrange
        string logFile1 = Path.Combine(_testDirectory, "log1.log");
        string logFile2 = Path.Combine(_testDirectory, "log2.log");

        // Generate a second key pair
        (string publicKey, string privateKey) secondKeyPair = EncryptionUtils.GenerateRsaKeyPair();

        // Act - Create two loggers with different encryption keys
        Logger logger1 = new LoggerConfiguration()
            .WriteTo.File(path: logFile1, hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        Logger logger2 = new LoggerConfiguration()
            .WriteTo.File(path: logFile2, hooks: new DeviceEncryptHooks(secondKeyPair.publicKey))
            .CreateLogger();

        // Write to both logs
        logger1.Information("Message for log 1");
        logger2.Information("Message for log 2");

        // Dispose both loggers
        logger1.Dispose();
        logger2.Dispose();

        // Assert - Decrypt with corresponding private keys
        string content1 = EncryptionUtils.DecryptLogFile(logFile1, _rsaKeyPair.privateKey);
        string content2 = EncryptionUtils.DecryptLogFile(logFile2, secondKeyPair.privateKey);

        content1.ShouldContain("Message for log 1");
        content2.ShouldContain("Message for log 2");

        // Verify cross-decryption fails
        string result1 = EncryptionUtils.DecryptLogFile(logFile1, secondKeyPair.privateKey);
        string result2 = EncryptionUtils.DecryptLogFile(logFile2, _rsaKeyPair.privateKey);
        result1.ShouldContain("[Error decrypting keys:");
        result2.ShouldContain("[Error decrypting keys:");
    }
}
