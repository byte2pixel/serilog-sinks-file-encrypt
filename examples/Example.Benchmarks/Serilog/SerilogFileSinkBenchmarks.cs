using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Example.Benchmarks.Keys;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

namespace Example.Benchmarks.Serilog;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SerilogFileSinkBenchmarks
{
    private string _logDirectory = string.Empty;
    private string _publicKey = string.Empty;

    [Params(100, 1000, 10000)]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int LogEntryCount { get; set; }

    [Params("Small", "Medium", "Large")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public string MessageSize { get; set; } = "Small";

    [GlobalSetup]
    public void Setup()
    {
        _publicKey = new KeyService().PublicKey;
        _logDirectory = Path.Join(
            Path.GetTempPath(),
            "SerilogBenchmarks",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_logDirectory);
    }

    [Benchmark(Baseline = true)]
    public void LogWithoutEncryption()
    {
        string logPath = Path.Join(_logDirectory, $"no-encrypt-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .WriteTo.File(path: logPath, rollingInterval: RollingInterval.Infinite, buffered: false)
            .CreateLogger();

        WriteLogEntries(logger);
    }

    [Benchmark]
    public void LogWithEncryption()
    {
        string logPath = Path.Join(_logDirectory, $"encrypted-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Infinite,
                buffered: false,
                hooks: new EncryptHooks(_publicKey)
            )
            .CreateLogger();

        WriteLogEntries(logger);
    }

    [Benchmark]
    public void LogWithEncryptionBuffered()
    {
        string logPath = Path.Join(_logDirectory, $"encrypted-buffered-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Infinite,
                buffered: true,
                hooks: new EncryptHooks(_publicKey)
            )
            .CreateLogger();

        WriteLogEntries(logger);
    }

    private void WriteLogEntries(Logger logger)
    {
        string message = MessageSize switch
        {
            "Small" => "User action completed",
            "Medium" =>
                "Processing request for user {UserId} from {IpAddress} with parameters {Params}",
            "Large" =>
                "Complex operation started with configuration: {Config}, user details: {User}, system state: {State}, metadata: {Metadata}",
            _ => "Default message",
        };

        for (int i = 0; i < LogEntryCount; i++)
        {
            switch (MessageSize)
            {
                case "Small":
                    logger.Information(message);
                    break;
                case "Medium":
                    logger.Information(
                        message,
                        i,
                        "192.168.1.100",
                        new { Action = "Update", ResourceId = i }
                    );
                    break;
                case "Large":
                    logger.Information(
                        message,
                        new
                        {
                            Setting1 = "Value1",
                            Setting2 = "Value2",
                            Setting3 = "Value3",
                        },
                        new
                        {
                            UserId = i,
                            UserName = $"User{i}",
                            Email = $"user{i}@example.com",
                        },
                        new
                        {
                            CpuUsage = 45.2,
                            MemoryUsage = 78.5,
                            DiskUsage = 62.3,
                        },
                        new
                        {
                            Timestamp = DateTimeOffset.Now,
                            CorrelationId = Guid.NewGuid(),
                            TraceId = Guid.NewGuid(),
                        }
                    );
                    break;
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_logDirectory))
            {
                Directory.Delete(_logDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
