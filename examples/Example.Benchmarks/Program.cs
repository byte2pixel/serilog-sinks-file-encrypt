// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using Example.Benchmarks.Encryption;
using Serilog.Sinks.File.Encrypt;

BenchmarkRunner.Run<EncryptedStreamBenchmarks>();
// EncryptedLogger l = new();
// l.LogManyMessages(10000);


return 0;