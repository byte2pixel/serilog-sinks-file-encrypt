using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Example.Benchmarks.Keys;
using Serilog.Sinks.File.Encrypt;

namespace Example.Benchmarks.Encryption;

[MemoryDiagnoser]
public class EncryptedStreamBenchmarks
{
    private byte[] _buffer = [];
    private readonly string _publicKey = new KeyService().PublicKey;
    private readonly RSA _rsaPublicKey = RSA.Create();

    [Params(512, 1024, 2048)]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int BufferSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rsaPublicKey.FromXmlString(_publicKey);

        // Create realistic log entry data instead of random bytes
        var logEntry = new
        {
            Timestamp = DateTimeOffset.Now,
            Level = "Information",
            Message = "User action completed successfully",
            Properties = new
            {
                UserId = 12345,
                Action = "UpdateProfile",
                Duration = 156,
                IpAddress = "192.168.1.100",
            },
        };

        string json = JsonSerializer.Serialize(logEntry);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        // Repeat to reach desired buffer size
        int repetitions = Math.Max(1, BufferSize / jsonBytes.Length);
        using MemoryStream tempStream = new();
        for (int i = 0; i < repetitions; i++)
        {
            tempStream.Write(jsonBytes, 0, jsonBytes.Length);
            tempStream.WriteByte((byte)'\n');
        }
        _buffer = tempStream.ToArray();
    }

    [Benchmark(Baseline = true)]
    public void PlainMemoryStreamWrite()
    {
        using MemoryStream ms = new();
        ms.Write(_buffer, 0, _buffer.Length);
        ms.Flush();
    }

    [Benchmark]
    public void EncryptedMemoryStreamWrite()
    {
        using MemoryStream ms = new();
        using EncryptedStream es = new(ms, _rsaPublicKey);
        es.Write(_buffer, 0, _buffer.Length);
        es.Flush();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _rsaPublicKey.Dispose();
    }
}
