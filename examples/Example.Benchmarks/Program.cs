using BenchmarkDotNet.Running;
using Example.Benchmarks.Encryption;

BenchmarkRunner.Run<EncryptedStreamBenchmarks>();
