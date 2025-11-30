using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Example.Benchmarks.Keys;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

namespace Example.Benchmarks.Scenarios;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class WebApiRequestBenchmarks
{
    private string _logDirectory = string.Empty;
    private string _publicKey = string.Empty;

    [Params(100, 1000)]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int RequestCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _publicKey = new KeyService().PublicKey;
        _logDirectory = Path.Combine(
            Path.GetTempPath(),
            "WebApiBenchmarks",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_logDirectory);
    }

    [Benchmark(Baseline = true)]
    public void SimulateApiRequestsWithoutEncryption()
    {
        string logPath = Path.Combine(_logDirectory, $"api-no-encrypt-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(path: logPath, rollingInterval: RollingInterval.Infinite, buffered: false)
            .CreateLogger();

        SimulateApiRequests(logger);
    }

    [Benchmark]
    public void SimulateApiRequestsWithEncryption()
    {
        string logPath = Path.Combine(_logDirectory, $"api-encrypted-{Guid.NewGuid()}.log");

        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Infinite,
                buffered: false,
                hooks: new EncryptHooks(_publicKey)
            )
            .CreateLogger();

        SimulateApiRequests(logger);
    }

    private void SimulateApiRequests(Logger logger)
    {
        Random random = new(42);
        string[] endpoints = ["/api/users", "/api/products", "/api/orders", "/api/reports"];
        string[] methods = ["GET", "POST", "PUT", "DELETE"];
        int[] statusCodes = [200, 201, 204, 400, 404, 500];

        for (int i = 0; i < RequestCount; i++)
        {
            string endpoint = endpoints[random.Next(endpoints.Length)];
            string method = methods[random.Next(methods.Length)];
            int statusCode = statusCodes[random.Next(statusCodes.Length)];
            int duration = random.Next(10, 500);
            string userId = $"user-{random.Next(1, 1000)}";
            string correlationId = Guid.NewGuid().ToString();

            // Simulate typical web API logging pattern
            logger.Information(
                "HTTP {Method} {Endpoint} responded {StatusCode} in {Duration}ms for user {UserId} (CorrelationId: {CorrelationId})",
                method,
                endpoint,
                statusCode,
                duration,
                userId,
                correlationId
            );

            // Occasionally log errors
            if (statusCode >= 500)
            {
                logger.Error(
                    "Server error processing {Method} {Endpoint}: {ErrorMessage}",
                    method,
                    endpoint,
                    "Internal server error occurred"
                );
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
