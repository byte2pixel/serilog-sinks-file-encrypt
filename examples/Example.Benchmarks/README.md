# Serilog.Sinks.File.Encrypt Benchmarks

This project contains comprehensive performance benchmarks for the Serilog.Sinks.File.Encrypt library using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Quick Start

### Prerequisites

- .NET 8.0 or higher

### Running Benchmarks

```bash
cd examples/Example.Benchmarks
dotnet run -c Release --framework net8.0
```

Select from the interactive menu which benchmark(s) to run.

Results are saved to `BenchmarkDotNet.Artifacts/results/` with HTML, CSV, and Markdown formats.

---

## Performance Summary 📊

### At a Glance

#### Time Overhead Benchmark Summary
The fuller the bar the more time overhead compared to baseline.
(lower is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Time Overhead < 50%  (Serilog File Sink, 100 entries)     │
| Baseline (no encryption): ~680 μs for 100 entries               │
│ v2.x (AES):                                                     │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1-8% (unbuffered)     │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -23 to -35% (buff.)   │
│ v3.x (AES-GCM):                                                 │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  0-13% (unbuffered)    │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -25 to -45% (buff.)   │
│ At 10K entries: AES → ~18%, AES-GCM → ~9-12%                    │
│ STATUS PASS - Well within target                                │
└─────────────────────────────────────────────────────────────────┘
```

#### Memory Overhead Benchmark Summary

The fuller the bar the more memory used compared to baseline.
(lower is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Memory Overhead < 2x (Serilog File Sink, Medium msgs)     │
│ Baseline (no encryption): ~652.49 KB for 1000 entries           │
│ v2.x (AES):                                                     │
│ ████████████████████████░░░░░░░░░░░░░░░░░ 2.15x (unbuffered)    │ 
│ ███████████████████████░░░░░░░░░░░░░░░░░░ 2.02x (buffered)      │
│ v3.x (AES-GCM):                                                 │
│ ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.07x (unbuffered)    │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.03x (buffered)      │
│ STATUS: AES can exceed 2x on smaller msgs                       │
│         AES-GCM PASS ~5-7% more memory well under 2x            │
└─────────────────────────────────────────────────────────────────┘
```
#### Throughput Benchmark Summary

The fuller the bar the closer to matching baseline.
(higher is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Throughput > 100,000 logs/sec (File Sink, Small, 100)     │
│ Full logging pipeline: Serilog formatting + file I/O + encrypt  │
| Baseline (no encryption): ~174,000 logs/sec                     |
│ v2.x (AES):                                                     │
│ ███████████████████████████████████░░░░░  153,200 (unbuffered)  │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  311,400 (buffered)    │
│ v3.0.0                                                          │
│ ██████████████████████████████████░░░░░░  148,200 (unbuffered)  │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  311,900 (buffered)    │
│ STATUS: PASS - Exceeds 100,000/sec target by ~1.5x              │
└─────────────────────────────────────────────────────────────────┘
```

### Real-World Scenarios

**Serilog File Sink - Medium Messages (1000 entries)**
```
                                              AES-GCM
                   Baseline       Unbuffered        Buffered
No Encryption:     2,990 μs       3,369 μs (+12%)   1,126 μs (-66%)
Memory:            652 KB         696 KB   (+7%)    672 KB (+3%)
Throughput/sec:    334,000        297,000  (+11%)   888,000 (-166%)
Verdict:           ✅ Unbuffered is default safe choice
Summary:           AES-GCM: +12% time overhead, +7% more memory, -11% throughput (unbuffered)
                   Buffered mode: 66% faster, 3% more memory, ~166% higher throughput
```

**Background Worker (10,000 messages)**
```
buffered: true
                        Baseline        AES-GCM
Time:                   5.62 ms         6.11 ms   (+8%)
Memory:                 4.34 MB         4.38 MB   (+1%)
Throughput: (logs/sec)  1,777,400       1,636,661 (-9%)
Verdict:                ✅ Fine for batch processing and logging to file
    AES-GCM: ~8% time overhead, < 1% more memory, ~9% less throughput
    Note: unbuffered ~14% time overhead, ~8% more memory
    393,700 vs. 344,800 logs/sec (unbuffered)
```

**Web API Logging To File.Sink (1,000 requests)**
```
buffered: TRUE
                        Baseline       AES-GCM
Time:                   1.32 ms        1.44 ms  (+9%)
Memory:                 1002 KB        1,020 KB (+2%)
Throughput: (req/sec)   758,725        696,864  (-9%)    
Verdict:                ✅ Acceptable for web API logging to file, especially with buffered writes
                        Note: It is probably bet to log to OTEL not to a file sink.
    AES-GCM: +9% time overhead, +2% more memory, ~9% less throughput️
    Note: unbuffered ~14% time overhead, ~5% more memory
    277,770 vs. 243,900 req/sec (unbuffered)
```

### Key Findings

✅ **Production Ready** - For applications needing encrypted log files.
✅ **Acceptable Throughput** - Only a ~ -5-16% loss in log/sec with encryption.
✅ **Safe by Default** - Unbuffered mode has no data loss risk (only unflushed data at risk)  
🚀 **Performance Mode Available** - Buffered mode is 66% *faster* than no encryption unbuffered
⚠️ **Buffering Trade-off** - Better performance but data loss risk on crashes  
✅ **Zero Lock Contentions** - Safe for multithreaded applications through Serilog.File.Sink

---

## Benchmark Details

### 1. Encrypted Stream Benchmarks

Tests the raw performance of the `EncryptedLogStream` class:

- **Baseline:** Plain `MemoryStream` write operations
- **Test:** `EncryptedLogStream` with RSA+AES-GCM encryption
- **Parameters:** Buffer sizes of 512, 1024, and 2048 bytes
- **Data:** Realistic JSON-formatted log entries

### 2. Serilog File Sink Benchmarks

Tests end-to-end Serilog logging with encryption:

- **Scenarios:** Without encryption, with encryption (unbuffered/buffered)
- **Parameters:**
  - Log entry counts: 100, 1,000, 10,000
  - Message sizes: Small, Medium, Large (with structured properties)
- **Measures:** Complete pipeline including serialization, formatting, and file I/O

### 3. Web API Request Simulation

Simulates realistic web application logging:

- HTTP request/response logging with structured data
- Method, endpoint, status code, duration, user ID, correlation ID
- Parameters: 100 and 1,000 request simulations, Buffered and Unbuffered modes
- Multithreaded diagnostics enabled

### 4. Background Worker Simulation

Simulates high-volume background processing:

- Job start/complete, progress updates, occasional warnings
- Parameters: 5,000 and 10,000 message simulations, Buffered and Unbuffered modes

---

## Best Practices

### Before Running Benchmarks

1. **Close unnecessary applications** - Reduce background noise
2. **Use Release mode** - Always benchmark optimized code
3. **Disable power management** - Prevent CPU throttling
4. **Run multiple times** - Verify consistency of results

### Interpreting Results

- Look for consistent patterns across multiple runs
- Compare against baseline measurements
- Watch for memory allocation increases
- Monitor GC collection frequency

---

## Results & Data

- **[Benchmarks.md](./Benchmarks.md)** - Complete historical benchmark data with detailed tables
- **BenchmarkDotNet.Artifacts/** - Latest benchmark runs (HTML, CSV, Markdown formats)
