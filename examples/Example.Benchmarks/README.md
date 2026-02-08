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

```
┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Time Overhead < 50%  (Serilog File Sink, 100 entries)     │
│ AES:                                                            │
│ ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  1-8% (unbuffered)     │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -23 to -35% (buff.)   │
│ AES-GCM:                                                        │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  4-5% (unbuffered)     │
│ FASTER ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -13 to -31% (buff.)   │
│ At 10K entries: AES → ~18%, AES-GCM → ~10%                      │
│ STATUS PASS - Well within target                                │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Memory Overhead < 2x  (Serilog File Sink, Medium msgs)    │
│ AES:                                                            │
│ ████████████████████████░░░░░░░░░░░░░░░░  1.83x (buffered)      │
│ ████████████████████████████████████░░░░  2.16x (unbuffered)    │
│ AES-GCM:                                                        │
│ ██████████████████░░░░░░░░░░░░░░░░░░░░░░  1.42x (buffered)      │
│ ████████████████████░░░░░░░░░░░░░░░░░░░░  1.46x (unbuffered)    │
│ STATUS: AES can exceed 2x on smaller msgs                       │
│         AES-GCM PASS - always under 2x                          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Throughput > 10,000 logs/sec  (Small msgs, 100 entries)   │
│ AES:                                                            │
│ █████████████████████████░░░░░░░░░░░░░░░  273,000 (buffered)    │
│ ████████████████░░░░░░░░░░░░░░░░░░░░░░░░  170,000 (unbuffered)  │
│ AES-GCM:                                                        │
│ █████████████████████████░░░░░░░░░░░░░░░  272,000 (buffered)    │
│ █████████████████░░░░░░░░░░░░░░░░░░░░░░░  178,000 (unbuffered)  │
│ STATUS: PASS - Exceeds target by 17-27x                         │
└─────────────────────────────────────────────────────────────────┘
```

### Real-World Scenarios

**Web API Logging (1,000 requests)**
```
                        Baseline        AES                    AES-GCM
Time:                   3.63 ms         4.08 ms  (+13%)        3.91 ms  (+10%)
Memory:                 999 KB          1,889 KB (1.89x)       1,271 KB (1.27x)
Throughput:             ~276K/sec       ~245K/sec              ~256K/sec
Verdict:                ✅ Both excellent for production, no data loss risk
                        AES-GCM: ~half the time overhead, 33% less memory ↗️
```

**Background Worker (10,000 messages)**
```
                        Baseline        AES                    AES-GCM
Time:                   5.74 ms         6.35 ms  (+11%)        6.19 ms  (+6%)
Memory:                 4.34 MB         7.13 MB  (1.64x)       5.19 MB  (1.20x)
Throughput:             ~1.74M/sec      ~1.58M/sec             ~1.62M/sec
Verdict:                ✅ Ideal for batch processing
                        AES-GCM: ~half the time overhead, 27% less memory ↗️
```

**Serilog File Sink - Medium Messages (100 entries)**
```
                                Baseline       AES                    AES-GCM
No Encryption (unbuffered):     593 μs         -                      -
Encrypted (unbuffered):         -              601 μs  (+1%)          647 μs  (+4%)
Encrypted (buffered):           -              412 μs  (-30%)         428 μs  (-31%)

Throughput (unbuffered):        169K/sec       166K/sec               155K/sec
Throughput (buffered):          -              243K/sec               234K/sec
Memory (unbuffered):            75 KB          162 KB  (2.16x)        110 KB  (1.46x)
Memory (buffered):              -              137 KB  (1.83x)        106 KB  (1.42x)
Verdict:                        ✅ Unbuffered is default safe choice
                                AES-GCM: 32% less memory overhead (unbuffered) ↗️
```

> *Note: Baselines differ slightly between runs (AES: 593 μs, AES-GCM: 624 μs); overhead % is vs each suite's own baseline.*

### Key Findings

✅ **Production Ready** - 1-8% overhead (unbuffered, 100 entries)
✅ **High Throughput** - 166K+ logs/sec unbuffered, 234K+ buffered (medium messages)  
✅ **Safe by Default** - Unbuffered mode has no data loss risk (only unflushed data at risk)  
🚀 **Performance Mode Available** - Buffered mode is 13-35% *faster* than no encryption  
⚠️ **Buffering Trade-off** - Better performance but data loss risk on crashes  
✅ **Zero Lock Contentions** - Safe for multithreaded applications when used through Serilog.File.Sink
✅ **Scales Well** - Better efficiency at higher volumes  

### Bottom Line

**AES-GCM encryption implementation is production-ready with unbuffered writes as the safe default.** The refactored AES-GCM implementation provides substantial memory improvements over the original AES implementation.

- **Unbuffered (default, safe):** 1-8% overhead (100 entries), up to ~18% AES / ~10% AES-GCM at 10K entries
- **Buffered (performance):** 13-35% *faster* than buffered with no encryption at low volume
- **Memory:** AES-GCM always under 2x (1.20-1.46x); AES can exceed 2x on smaller messages (2.16x)
- **Key advantage:** AES-GCM uses 27-33% less total allocation than the original AES implementationacross all scenarios

Buffered mode provides exceptional performance but should only be used when you can tolerate data loss on crashes and have proper shutdown handling.

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
