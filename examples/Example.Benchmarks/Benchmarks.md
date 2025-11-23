# Benchmark History

This document tracks the history of benchmark results for various projects. Each entry includes the date, project name, and a summary of the benchmark results.

## Benchmark Entries

### Baseline Benchmarks

Prior to any optimizations these are the baseline benchmarks.

| Method                          | BufferSize |         Mean |        Error |       StdDev |       Median |
|---------------------------------|------------|-------------:|-------------:|-------------:|-------------:|
| MemoryStreamWrite               | 512        |     24.71 ns |     0.505 ns |     0.771 ns |     24.58 ns |                                                                                                                                                                     
| EncryptedMemoryStreamWrite      | 512        | 53,464.19 ns |   336.083 ns |   262.391 ns | 53,427.85 ns |
| EncryptedChunkMemoryStreamWrite | 512        | 28,626.39 ns |   569.622 ns | 1,201.526 ns | 28,582.66 ns |
| MemoryStreamWrite               | 1024       |     40.98 ns |     0.983 ns |     2.806 ns |     40.92 ns |
| EncryptedMemoryStreamWrite      | 1024       | 57,128.72 ns | 1,039.018 ns |   971.898 ns | 57,185.56 ns |
| EncryptedChunkMemoryStreamWrite | 1024       | 29,854.05 ns |   569.826 ns |   533.016 ns | 29,812.38 ns |
| MemoryStreamWrite               | 2048       |     74.99 ns |     2.839 ns |     8.281 ns |     71.77 ns |
| EncryptedMemoryStreamWrite      | 2048       | 55,996.66 ns |   957.661 ns | 1,573.464 ns | 55,223.31 ns |
| EncryptedChunkMemoryStreamWrite | 2048       | 28,936.44 ns |   213.266 ns |   199.489 ns | 28,917.93 ns |

Removing MemoryStreamWrite.

| Method                          | BufferSize |     Mean |    Error |   StdDev |
|---------------------------------|------------|---------:|---------:|---------:|
| EncryptedMemoryStreamWrite      | 512        | 53.50 us | 0.294 us | 0.261 us |                                                                                                                                                                                                
| EncryptedChunkMemoryStreamWrite | 512        | 28.40 us | 0.567 us | 0.832 us |
| EncryptedMemoryStreamWrite      | 1024       | 58.34 us | 1.110 us | 1.279 us |
| EncryptedChunkMemoryStreamWrite | 1024       | 30.08 us | 0.598 us | 0.798 us |
| EncryptedMemoryStreamWrite      | 2048       | 58.61 us | 1.142 us | 1.012 us |
| EncryptedChunkMemoryStreamWrite | 2048       | 30.95 us | 0.604 us | 0.742 us |

| Method                          | BufferSize |     Mean |    Error |   StdDev |
|---------------------------------|------------|---------:|---------:|---------:|
| EncryptedMemoryStreamWrite      | 512        | 53.61 us | 0.538 us | 0.503 us |                                                                                                                                                                                                
| EncryptedChunkMemoryStreamWrite | 512        | 27.23 us | 0.164 us | 0.145 us |
| EncryptedMemoryStreamWrite      | 1024       | 54.00 us | 0.357 us | 0.298 us |
| EncryptedChunkMemoryStreamWrite | 1024       | 27.67 us | 0.365 us | 0.341 us |
| EncryptedMemoryStreamWrite      | 2048       | 54.93 us | 0.396 us | 0.331 us |
| EncryptedChunkMemoryStreamWrite | 2048       | 28.77 us | 0.245 us | 0.229 us |

### Optimization 1: Improved Encryption Transform For Non-Chunked Streams

| Method                          | BufferSize |     Mean |    Error |   StdDev |
|---------------------------------|------------|---------:|---------:|---------:|
| EncryptedMemoryStreamWrite      | 512        | 27.13 us | 0.249 us | 0.233 us |                                                                                                                                                                                                
| EncryptedChunkMemoryStreamWrite | 512        | 27.34 us | 0.217 us | 0.203 us |
| EncryptedMemoryStreamWrite      | 1024       | 27.70 us | 0.349 us | 0.310 us |
| EncryptedChunkMemoryStreamWrite | 1024       | 27.83 us | 0.285 us | 0.238 us |
| EncryptedMemoryStreamWrite      | 2048       | 30.60 us | 0.593 us | 0.870 us |
| EncryptedChunkMemoryStreamWrite | 2048       | 30.71 us | 0.400 us | 0.374 us |

| Method                          | BufferSize |     Mean |    Error |   StdDev |
|---------------------------------|------------|---------:|---------:|---------:|
| EncryptedMemoryStreamWrite      | 512        | 27.03 us | 0.203 us | 0.180 us |                                                                                                                                                                                                
| EncryptedChunkMemoryStreamWrite | 512        | 27.31 us | 0.194 us | 0.162 us |
| EncryptedMemoryStreamWrite      | 1024       | 27.52 us | 0.341 us | 0.319 us |
| EncryptedChunkMemoryStreamWrite | 1024       | 27.81 us | 0.518 us | 0.433 us |
| EncryptedMemoryStreamWrite      | 2048       | 28.81 us | 0.567 us | 0.607 us |
| EncryptedChunkMemoryStreamWrite | 2048       | 29.19 us | 0.355 us | 0.332 us |

### No Optimizations but slight refactor in marker logic
Chunk logic removed to focus on non-chunked performance.

| Method                     | BufferSize |     Mean |    Error |   StdDev |
|----------------------------|------------|---------:|---------:|---------:|
| EncryptedMemoryStreamWrite | 512        | 26.53 us | 0.116 us | 0.108 us |                                                                                                                                                                                                     
| EncryptedMemoryStreamWrite | 1024       | 26.92 us | 0.128 us | 0.113 us |
| EncryptedMemoryStreamWrite | 2048       | 28.03 us | 0.141 us | 0.125 us |