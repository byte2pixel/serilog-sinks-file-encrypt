using System.Security.Cryptography;
using Serilog.Core;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.File.Encrypt.Test;

public class EncryptionConfigurationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly (string publicKey, string privateKey) _rsaKeyPair;

    public EncryptionConfigurationTests()
    {
        // Setup test environment
        _testDirectory = Path.Combine(Path.GetTempPath(), "SerilogEncryptTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _rsaKeyPair = EncryptionUtils.GenerateRsaKeyPair();
    }

    public void Dispose()
    {
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
        finally
        {
            GC.SuppressFinalize(this);
        }
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
        Assert.Contains(testValue, decryptedContent);
        Assert.Contains("Property", decryptedContent); // Looking for property name
        Assert.Contains("Message with", decryptedContent);
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
        Assert.NotEmpty(logFiles);

        // Verify it's encrypted
        string decryptedContent = EncryptionUtils.DecryptLogFile(logFiles[0], _rsaKeyPair.privateKey);
        Assert.Contains(logMessage, decryptedContent);
    }

    [Fact]
    public void CanEncryptFilesWithDifferentPublicKeys()
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

        Assert.Contains("Message for log 1", content1);
        Assert.Contains("Message for log 2", content2);

        // Verify cross-decryption fails
        string result1 = EncryptionUtils.DecryptLogFile(logFile1, secondKeyPair.privateKey);
        string result2 = EncryptionUtils.DecryptLogFile(logFile2, _rsaKeyPair.privateKey);
        Assert.Contains("[Error decrypting keys:", result1);
        Assert.Contains("[Error decrypting keys:", result2);
    }

    [Fact]
    public void EncryptionWorksWithDifferentLogEventLevels()
    {
        // Arrange
        string logFilePath = Path.Combine(_testDirectory, "levels.log");

        // Act - Configure logger with minimum level Warning
        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.File(
                path: logFilePath,
                hooks: new DeviceEncryptHooks(_rsaKeyPair.publicKey))
            .CreateLogger();

        // Log different levels
        logger.Verbose("Verbose message");
        logger.Debug("Debug message");
        logger.Information("Info message");
        logger.Warning("Warning message");
        logger.Error("Error message");
        logger.Fatal("Fatal message");

        logger.Dispose();

        // Assert
        string decryptedContent = EncryptionUtils.DecryptLogFile(logFilePath, _rsaKeyPair.privateKey);

        // Only Warning and above should be in the log
        Assert.DoesNotContain("Verbose message", decryptedContent);
        Assert.DoesNotContain("Debug message", decryptedContent);
        Assert.DoesNotContain("Info message", decryptedContent);
        Assert.Contains("Warning message", decryptedContent);
        Assert.Contains("Error message", decryptedContent);
        Assert.Contains("Fatal message", decryptedContent);
    }
}
