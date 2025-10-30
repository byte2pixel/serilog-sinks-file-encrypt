using System.Security.Cryptography;
using Serilog.Core;

namespace Serilog.Sinks.File.Encrypt.Test;

public sealed class FileSinkTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _logFilePath;
    private readonly (string publicKey, string privateKey) _rsaKeyPair;
    private bool _disposed;

    public FileSinkTests()
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
    public void CanGenerateRsaKeyPair()
    {
        // Act
        (string publicKey, string privateKey) keyPair = EncryptionUtils.GenerateRsaKeyPair();

        // Assert
        Assert.NotNull(keyPair.publicKey);
        Assert.NotNull(keyPair.privateKey);
        Assert.NotEqual(keyPair.publicKey, keyPair.privateKey);

        // Verify keys are valid by loading them into RSA objects
        using RSA publicRsa = RSA.Create();
        using RSA privateRsa = RSA.Create();

        publicRsa.FromXmlString(keyPair.publicKey);
        privateRsa.FromXmlString(keyPair.privateKey);
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
        Assert.True(System.IO.File.Exists(_logFilePath), "Log file should exist");

        // The file should contain encrypted content (not plaintext)
        string fileContent = System.IO.File.ReadAllText(_logFilePath);
        Assert.DoesNotContain(logMessage, fileContent);
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
        Assert.Contains(logMessage, decryptedContent);
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
        Assert.Contains(logMessage, decryptedContent);
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
        Assert.Contains("[Error decrypting keys:", result);
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
            Assert.Contains($"Log message {i}", decryptedContent);
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
        Assert.Contains(firstMessage, decryptedContent);
        Assert.Contains(secondMessage, decryptedContent);
    }
}
