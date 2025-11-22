using BenchmarkDotNet.Attributes;
using Serilog.Sinks.File.Encrypt;
using System.Security.Cryptography;
using Example.Benchmarks.Keys;

namespace Example.Benchmarks.Encryption;

public class EncryptedStreamBenchmarks
{
    private byte[] _buffer = [];
    private readonly string _publicKey = new KeyService().PublicKey;
    private readonly RSA _rsaPublicKey = RSA.Create();
    [Params(512, 1024, 2048)]
    public int BufferSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Adjust these paths as needed for your environment
        _rsaPublicKey.FromXmlString(_publicKey);
        _buffer = new byte[BufferSize];
        new Random(42).NextBytes(_buffer);
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

