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

> **Latest run:** 2026-07-03, v6.0.0 (`feature/83-v2-format`), Windows 11, .NET 8.0.28.
> Earlier-era rows were measured on different hardware — compare **percentages/ratios**, not
> absolute values. Raw tables for every era live in [Benchmarks.md](./Benchmarks.md).

#### Time Overhead Benchmark Summary
The fuller the bar the more time overhead compared to baseline.
(lower is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Time Overhead < 50%  (Serilog File Sink, 100 entries)     │
│ v2.x (AES):                                                     │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1-8% (unbuffered)    │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -23 to -35% (buff.)   │
│ v3.x (AES-GCM):                                                 │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  0-13% (unbuffered)   │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -25 to -45% (buff.)   │
│ v6.x (AES-GCM + AAD + end-of-log seal):                         │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  0-13% (unbuffered)   │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -30 to -83% (buff.)   │
│ At 10K entries: AES ~18%, AES-GCM ~9-12%, v6 ~7-13%             │
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
│ ████████████████████████░░░░░░░░░░░░░░░░░  2.15x (unbuffered)   │
│ ███████████████████████░░░░░░░░░░░░░░░░░░  2.02x (buffered)     │
│ v3.x (AES-GCM):                                                 │
│ ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.07x (unbuffered)   │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.03x (buffered)     │
│ v6.x (AES-GCM + AAD + end-of-log seal):                         │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.00x (unbuffered)   │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.01x (buffered)     │
│ STATUS: AES could exceed 2x on smaller msgs                     │
│         AES-GCM PASS; v6 allocations ~= baseline (<=1%)         │
└─────────────────────────────────────────────────────────────────┘
```
#### Throughput Benchmark Summary

The fuller the bar the closer to matching baseline.
(higher is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Throughput > 100,000 logs/sec (File Sink, Small, 100)     │
│ Full logging pipeline: Serilog formatting + file I/O + encrypt  │
│ Earlier hardware - baseline ~174,000 logs/sec:                  │
│   v2.x (AES):     153,200 unbuffered / 311,400 buffered         │
│   v3.0.0:         148,200 unbuffered / 311,900 buffered         │
│ Current hardware - baseline ~135,900 logs/sec:                  │
│ v6.x (AES-GCM + AAD + end-of-log seal):                         │
│ █████████████████████████████████████░░░░  124,700 (unbuffered) │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  240,400 (buffered)    │
│ STATUS: PASS - Exceeds 100,000/sec target                       │
└─────────────────────────────────────────────────────────────────┘
```

### Real-World Scenarios (v6.0.0 run, 2026-07-03)

**Serilog File Sink - Medium Messages (1000 entries)**
```
                                     AES-GCM + AAD + seal (v6)
                   Baseline       Unbuffered         Buffered
Time:              3,560 μs       3,937 μs (+11%)    1,323 μs (-63%)
Memory:            652 KB         653 KB   (+0.1%)   656 KB (+0.6%)
Throughput/sec:    280,900        254,000  (-10%)    756,000 (+169%)
Verdict:           ✅ Unbuffered remains the default safe choice
Summary:           v6: +11% time, ~0% more memory, -10% throughput (unbuffered)
                   Buffered mode: 63% faster than baseline, +169% throughput
```

**Background Worker (10,000 messages)**
```
buffered: true
                        Baseline        v6 (AES-GCM + AAD + seal)
Time:                   6.67 ms         7.12 ms   (+7%)
Memory:                 4.34 MB         4.34 MB   (+0%)
Throughput: (logs/sec)  1,500,400       1,404,700 (-7%)
Verdict:                ✅ Fine for batch processing and logging to file
    v6: ~7% time overhead, ~0% more memory, ~7% less throughput
    Note: unbuffered was a clean +11% this run (329,100 vs 296,800 logs/sec),
    resolving the +16-24% I/O noise seen in the earlier same-day run.
```

**Web API Logging To File.Sink (1,000 requests)**
```
buffered: TRUE
                        Baseline       v6 (AES-GCM + AAD + seal)
Time:                   1.49 ms        1.65 ms  (+11%)
Memory:                 1,002 KB       1,003 KB (+0.1%)
Throughput: (req/sec)   673,300        607,100  (-10%)
Verdict:                ✅ Acceptable for web API logging to file, especially buffered
                        Note: It is probably best to log to OTEL not to a file sink.
    v6: +11% time overhead, +0.1% more memory, ~10% less throughput
    Note: unbuffered +11% time, ~0% more memory; 234,400 vs 211,100 req/sec
```

### Key Findings

✅ **Production Ready** - For applications needing encrypted log files.
✅ **Acceptable Throughput** - Only a ~7-13% loss in logs/sec with unbuffered encryption.
✅ **Integrity Effectively Free** - The v2 format's AAD binding + end-of-log seal (v6.0.0) add no
measurable cost: allocations are now ~1.00x baseline (previously up to 1.2x) and the time-overhead
band is unchanged vs v3.x.
✅ **Safe by Default** - Unbuffered mode has no data loss risk (only unflushed data at risk)  
🚀 **Performance Mode Available** - Buffered mode is up to ~6x *faster* than no-encryption
unbuffered at 10K entries (and every cleanly closed file still gets its seal on dispose)
⚠️ **Buffering Trade-off** - Better performance but data loss risk on crashes  
✅ **Zero Lock Contentions** - Safe for multithreaded applications through `Serilog.File.Sink`

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
