using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Example.Benchmarks.Keys;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

namespace Example.Benchmarks.Scenarios;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class BackgroundWorkerBenchmarks
{
    private string _logDirectory = string.Empty;
    private string _publicKey = string.Empty;

    [Params(5000, 10000)]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _publicKey = new KeyService().PublicKey;
        _logDirectory = Path.Combine(
            Path.GetTempPath(),
            "BackgroundWorkerBenchmarks",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_logDirectory);
    }

    [Benchmark(Baseline = true)]
    public void SimulateBackgroundWorkerWithoutEncryption()
    {
        string logPath = Path.Combine(_logDirectory, $"worker-no-encrypt-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(path: logPath, rollingInterval: RollingInterval.Infinite, buffered: true)
            .CreateLogger();

        SimulateBackgroundWorker(logger);
    }

    [Benchmark]
    public void SimulateBackgroundWorkerWithEncryption()
    {
        string logPath = Path.Combine(_logDirectory, $"worker-encrypted-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Infinite,
                buffered: true,
                hooks: new EncryptHooks(_publicKey)
            )
            .CreateLogger();

        SimulateBackgroundWorker(logger);
    }

    private void SimulateBackgroundWorker(Logger logger)
    {
        Random random = new(42);
        string[] jobTypes =
        [
            "DataSync",
            "EmailSend",
            "ReportGeneration",
            "CacheRefresh",
            "DataCleanup",
        ];

        for (int i = 0; i < MessageCount; i++)
        {
            string jobType = jobTypes[random.Next(jobTypes.Length)];
            int jobId = i;

            // Typical background job logging pattern
            logger.Debug("Starting job {JobType} with ID {JobId}", jobType, jobId);

            // Simulate work with progress logging
            if (i % 100 == 0)
            {
                logger.Information(
                    "Job {JobType} progress: {Processed}/{Total} items",
                    jobType,
                    i,
                    MessageCount
                );
            }

            // Occasional warnings
            if (random.Next(100) < 5) // 5% warning rate
            {
                logger.Warning("Job {JobType} ({JobId}) took longer than expected", jobType, jobId);
            }
        }

        logger.Information(
            "Background worker completed processing {MessageCount} jobs",
            MessageCount
        );
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
