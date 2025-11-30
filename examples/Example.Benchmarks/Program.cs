using BenchmarkDotNet.Running;
using Example.Benchmarks.Encryption;
using Example.Benchmarks.Scenarios;
using Example.Benchmarks.Serilog;

Console.WriteLine("Serilog.Sinks.File.Encrypt Benchmarks");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("Select benchmark to run:");
Console.WriteLine("1. Encrypted Stream (Low-level)");
Console.WriteLine("2. Serilog File Sink Comparison");
Console.WriteLine("3. Web API Request Simulation");
Console.WriteLine("4. Background Worker Simulation");
Console.WriteLine("5. Run All Benchmarks");
Console.WriteLine("0. Exit");
Console.WriteLine();
Console.Write("Enter selection: ");

string? input = Console.ReadLine();

switch (input)
{
    case "1":
        BenchmarkRunner.Run<EncryptedStreamBenchmarks>();
        break;
    case "2":
        BenchmarkRunner.Run<SerilogFileSinkBenchmarks>();
        break;
    case "3":
        BenchmarkRunner.Run<WebApiRequestBenchmarks>();
        break;
    case "4":
        BenchmarkRunner.Run<BackgroundWorkerBenchmarks>();
        break;
    case "5":
        BenchmarkRunner.Run<EncryptedStreamBenchmarks>();
        BenchmarkRunner.Run<SerilogFileSinkBenchmarks>();
        BenchmarkRunner.Run<WebApiRequestBenchmarks>();
        BenchmarkRunner.Run<BackgroundWorkerBenchmarks>();
        break;
    case "0":
        Console.WriteLine("Exiting...");
        break;
    default:
        Console.WriteLine("Invalid selection. Exiting...");
        break;
}
