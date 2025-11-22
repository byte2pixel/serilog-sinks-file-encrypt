using System.Security.Cryptography;
using Example.Benchmarks.Keys;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

namespace Example.Benchmarks.Encryption;

public class EncryptedLogger
{
    private readonly Logger _logger;
    private readonly RSA _rsa = RSA.Create();

    public EncryptedLogger()
    {
        KeyService keyService = new();
        string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logDirectory, "log.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31, // Keep logs for 31 days
                hooks: new DeviceEncryptHooks(keyService.PublicKey) // Use public key for encryption
            )
            .CreateLogger();
    }
    
    public void LogManyMessages(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _logger.Information("Log message number {MessageNumber} at {Timestamp}", i + 1, DateTimeOffset.Now);
        }
    }
}