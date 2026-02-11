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
│ AES:                                                            │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1-8% (unbuffered)     │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -23 to -35% (buff.)   │
│ AES-GCM:                                                        │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  0-8% (unbuffered)     │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -25 to -45% (buff.)   │
│ At 10K entries: AES → ~18%, AES-GCM → ~7-16%                    │
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
│ AES:                                                            │
│ ████████████████████████░░░░░░░░░░░░░░░░  2.15x (unbuffered)    │ 
│ ███████████████████████░░░░░░░░░░░░░░░░░░  2.02x (buffered)     │
│ AES-GCM:                                                        │
│ ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.06x (unbuffered)    │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1.02x (buffered)      │
│ STATUS: AES can exceed 2x on smaller msgs                       │
│         AES-GCM PASS ~5% more memory well under 2x              │
└─────────────────────────────────────────────────────────────────┘
```
#### Throughput Benchmark Summary

The fuller the bar the closer to matching baseline.
(higher is better)
```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Throughput > 1,500 logs/sec  (Serilog Sink, Small, 100)   │
│ Full logging pipeline: Serilog formatting + file I/O + encrypt  │
| Baseline (no encryption): ~1,760 logs/sec                       |
│ AES:                                                            │
│ ███████████████████████████████████████░  1,696 (unbuffered)    │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  2,733 (buffered)      │
│ AES-GCM:                                                        │
│ ████████████████████████████████████░░░░  1,534 (unbuffered)    │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  2,700 (buffered)      │
│ STATUS: PASS - Exceeds 1,500/sec target by 1.1-1.9x             │
│ (Raw EncryptedStream: ~70K writes/sec - see stream bench)       │
└─────────────────────────────────────────────────────────────────┘
```

### Real-World Scenarios

**Serilog File Sink - Medium Messages (1000 entries)**
```
                                              AES-GCM
                   Baseline       Unbuffered        Buffered
No Encryption:     3,000 μs       3,345 μs (+12%)   1,122 μs (-63%)
Memory:            652 KB         696 KB   (+7%)    672 KB (+3%)
Throughput:        1,760/sec      1,534/sec (-13%)  2,700/sec (+53%)
Verdict:           ✅ Unbuffered is default safe choice
Summary:           AES-GCM: +12% time overhead, +7% more memory, -13% throughput (unbuffered)
                   Buffered mode: 63% faster, 3% more memory, 53% higher throughput
```

**Background Worker (10,000 messages)**
```
buffered: true
                        Baseline        AES-GCM
Time:                   5.70 ms         6.30 ms  (+10%)
Memory:                 4.34 MB         4.38 MB  (+1%)
Throughput:             ?/sec           ?/sec (-9.5%)
Verdict:                ✅ Fine for batch processing and logging to file
    AES-GCM: ~10% time overhead, < 1% more memory, ~10% less throughput
```

**Web API Logging To File.Sink (1,000 requests)**
```
buffered: true
                        Baseline       AES-GCM
Time:                   1.32 ms        1.50 ms  (+13%)
Memory:                 1002 KB        1,020 KB (+2%)
Throughput:             758 r/s        667 r/s (-12%)    
Verdict:                ❌️ Writing to a file is not recommended in a Web API
    AES-GCM: +14% time overhead, +2% more memory, ~12% less throughput️
```

### Key Findings

✅ **Production Ready** - For applications needing encrypted log files.
✅ **Acceptable Throughput** - Only a ~ -7% loss in log/sec with encryption.
✅ **Safe by Default** - Unbuffered mode has no data loss risk (only unflushed data at risk)  
🚀 **Performance Mode Available** - Buffered mode is 50% *faster* than no encryption unbuffered
⚠️ **Buffering Trade-off** - Better performance but data loss risk on crashes  
✅ **Zero Lock Contentions** - Safe for multithreaded applications through Serilog.File.Sink
✅ **Scales Well** - Better efficiency at higher volumes

---

## Benchmark Details

### 1. Encrypted Stream Benchmarks

Tests the raw performance of the `EncryptedStream` class:

- **Baseline:** Plain `MemoryStream` write operations
- **Test:** `EncryptedStream` with RSA+AES-GCM encryption
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
- Parameters: 100 and 1,000 request simulations
- Multithreaded diagnostics enabled

### 4. Background Worker Simulation

Simulates high-volume background processing:

- Job start/complete, progress updates, occasional warnings
- Parameters: 5,000 and 10,000 message simulations
- Tests with buffered writes (common for batch processing)

---

## Recommended Configuration

### ⚠️ Important: Buffering & Data Loss Risk

When using `buffered: true` with encryption, **data written since the last flush may be lost** if your application crashes or terminates unexpectedly. The risk window depends on your `flushToDiskInterval` setting (default is determined by the runtime/OS). This is because:

1. Buffered writes hold data in memory between flush intervals
2. Encryption requires finalizing blocks to write valid encrypted data
3. Sudden termination prevents proper block finalization of unflushed data

**Risk Window:**
- `flushToDiskInterval: TimeSpan.FromSeconds(1)` → At most 1 second of logs at risk
- `flushToDiskInterval: TimeSpan.FromMilliseconds(500)` → At most 500ms of logs at risk
- Default (no explicit interval) → Runtime/OS decides, typically several seconds

**Mitigation strategies:**

```csharp
// 1. Configure flush interval to balance performance vs data loss window
.WriteTo.File(
    buffered: true,
    flushToDiskInterval: TimeSpan.FromMilliseconds(500),  // Max 500ms of logs at risk
    // ...
)

// 2. Explicitly flush on critical operations
Log.Information("Critical operation completed");
Log.CloseAndFlush();  // Ensure data is written before exit

// 3. Use unbuffered for ultra-critical logs (no data loss window)
.WriteTo.File(
    buffered: false,  // Immediate writes (default Serilog behavior)
    // ...
)
```

**Choosing your flush interval:**
- High-volume, low-criticality: `TimeSpan.FromSeconds(5)` - Better performance
- Balanced approach: `TimeSpan.FromSeconds(1)` - Good compromise
- Critical data: `TimeSpan.FromMilliseconds(500)` - Minimal risk window
- Ultra-critical: `buffered: false` - Zero risk, accepts performance cost

**Recommendation:** Use unbuffered writes for most scenarios.
For best performance in production, use buffered writes:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        buffered: true,              // Critical for performance!
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        hooks: new EncryptHooks(publicKey))
    .CreateLogger();
```

**Result:** 1-18% overhead depending on volume, 1.2-2.2x memory, 155K+ logs/sec ✅

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

### Production Deployment

✅ **DO Use Encryption When:**
- Logging sensitive data (PII, credentials, tokens)
- Compliance requires encryption at rest
- High-volume logging (overhead is minimal even unbuffered)

✅ **Use Buffered Mode When:**
- Performance is critical (high-volume background workers)
- Application has reliable shutdown handling (`Log.CloseAndFlush()`)
- You can tolerate potential loss of recent logs

⚠️ **Consider Alternatives When:**
- Every microsecond counts (real-time trading systems)
- Memory is severely constrained (embedded systems)
- Log files already encrypted by infrastructure

⚠️ **Important - Default Configuration:**
- **Start with unbuffered** (default) for data safety
- **Opt-in to buffered** only when performance is critical AND risk is acceptable
- Buffered + Encrypted = Risk of data loss on crashes/exceptions
- Encryption needs to finalize blocks before data is valid
- **Always call `Log.CloseAndFlush()` on application shutdown**

---

## Results & Data

- **[Benchmarks.md](./Benchmarks.md)** - Complete historical benchmark data with detailed tables
- **BenchmarkDotNet.Artifacts/** - Latest benchmark runs (HTML, CSV, Markdown formats)
