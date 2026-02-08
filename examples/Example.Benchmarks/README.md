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
│ GOAL: Time Overhead < 50%                                       │
│ AES (Original):                                                 │
│ ████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  17% (unbuffered)      │
│ ██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   7% (buffered)        │
│ AES-GCM (Refactored):                                           │
│ █░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   1% (unbuffered)      │
│ NEGATIVE ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  -37% (buffered)       │
│ STATUS: ✅ PASS ✅ Dramatically exceeds target                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Memory Overhead < 2x                                      │
│ AES (Original):                                                 │
│ ████████████████████████████░░░░░░░░░░░  2.36x (buffered)       │
│ ██████████████████████████████████░░░░░  3.82x (unbuffered)     │
│ AES-GCM (Refactored):                                           │
│ █████████████████░░░░░░░░░░░░░░░░░░░░░░  1.81x (buffered)       │
│ ███████████████████░░░░░░░░░░░░░░░░░░░░  1.95x (unbuffered)     │
│ STATUS: ✅ PASS ✅ Excellent improvement                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ GOAL: Throughput > 10,000 logs/sec                              │
│ AES (Original):                                                 │
│ █████████████████████████░░░░░░░░░░░░░░░  273,000 (buffered)    │
│ ████████████████░░░░░░░░░░░░░░░░░░░░░░░░  170,000 (unbuffered)  │
│ AES-GCM (Refactored):                                           │
│ ██████████████████████████░░░░░░░░░░░░░░  282,000 (buffered)    │
│ █████████████████░░░░░░░░░░░░░░░░░░░░░░░  176,000 (unbuffered)  │
│ STATUS: ✅ PASS ✅ Exceeds target by 17-28x                    │
└─────────────────────────────────────────────────────────────────┘
```

### Real-World Scenarios

**Web API Logging (1,000 requests)**
```
                        AES (Original)         AES-GCM (Refactored)
Without Encryption:     3.63 ms                3.59 ms
With Encryption:        4.08 ms  (+13%)        3.87 ms  (+8%)   ← Default (unbuffered)
Throughput:             245,000 req/sec        258,000 req/sec
Memory:                 1.89x overhead         1.27x overhead
Verdict:                ✅ Excellent for production, no data loss risk
                        Refactor: 26% faster, 33% less memory ↗️
```

**Background Worker (10,000 messages)**
```
                        AES (Original)         AES-GCM (Refactored)
Without Encryption:     5.74 ms                5.73 ms
With Encryption:        6.35 ms  (+11%)        6.13 ms  (+7%)   ← Buffered mode
Throughput:             1,575,000 msg/sec      1,631,000 msg/sec
Memory:                 1.64x overhead         1.20x overhead
Verdict:                ✅ Ideal for batch processing (if crash risk acceptable)
                        Refactor: 3% faster, 27% less memory ↗️
```

**Serilog File Sink - Small Messages (100 entries)**
```
                                AES (Original)         AES-GCM (Refactored)
No Encryption (unbuffered):     567 μs                 563 μs
Encrypted (unbuffered):         590 μs  (+4%)          569 μs  (+1%)   ← Default, safe
Encrypted (buffered):           366 μs  (-35%)         355 μs  (-37%)  ← Performance mode

Throughput (unbuffered):        170,000 logs/sec       176,000 logs/sec  ← Default
Throughput (buffered):          273,000 logs/sec       282,000 logs/sec  ← Performance mode
Memory (unbuffered):            3.82x overhead         1.95x overhead
Memory (buffered):              2.36x overhead         1.81x overhead
Verdict:                        ✅ Unbuffered is default safe choice
                                Refactor: 4% faster, 49% less memory (unbuffered) ↗️
```

### Key Findings

✅ **Production Ready** - 1-8% overhead (unbuffered) with refactored AES-GCM  
✅ **High Throughput** - 176K+ logs/sec unbuffered, 282K+ buffered (small msgs)  
✅ **Safe by Default** - Unbuffered mode has no data loss risk (Only what hasn't been flushed yet) 
🚀 **Performance Mode Available** - Buffered reduces overhead to negative (faster!)  
⚠️ **Buffering Trade-off** - Better performance but data loss risk on crashes  
✅ **Zero Lock Contentions** - Safe for multi-threaded applications  
✅ **Scales Well** - Better efficiency at higher volumes  
🎯 **Refactor Benefits** - AES-GCM /w allocation improvements is more secure and 3-26% faster, using 27-49% less memory

### Bottom Line

**The encryption implementation is production-ready with unbuffered writes as the safe default.** The refactored AES-GCM implementation shows substantial improvements:

- **Unbuffered (default, safe):** 1-8% overhead, 1.27-1.95x memory
- **Buffered (performance):** Up to 37% *faster* than no encryption, 1.20-1.81x memory
- **Real-world impact:** 3-26% faster with 27-49% less memory vs original

Buffered mode provides exceptional performance but should only be used when you can tolerate data loss on crashes and have proper shutdown handling.

---

## Benchmark Details

### 1. Encrypted Stream Benchmarks

Tests the raw performance of the `EncryptedStream` class:

- **Baseline:** Plain `MemoryStream` write operations
- **Test:** `EncryptedStream` with RSA+AES encryption
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
- Multi-threaded diagnostics enabled

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

**Result:** 6-17% overhead, 2x memory, 200K+ logs/sec ✅

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
