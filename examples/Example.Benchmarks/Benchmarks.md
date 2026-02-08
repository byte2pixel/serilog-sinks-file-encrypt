# Benchmark Results - Raw Data

This document contains the complete historical record of benchmark runs for the Serilog.Sinks.File.Encrypt project.

**For performance analysis and recommendations, see [README.md](./README.md)**

## Important Note on Buffered Results

⚠️ **Data Loss Risk with Buffered Writes:**  
The benchmarks show excellent performance with `buffered: true`, but be aware that buffered writes combined with encryption carry a risk of data loss if the application crashes before flushing. Encryption requires finalizing blocks, so unflushed buffered data cannot be recovered.

**Always use `Log.CloseAndFlush()` in production applications before shutdown.**

---

## Table of Contents

- [Encrypted Stream Benchmarks](#encrypted-stream-benchmarks)
- [Serilog File Sink Benchmarks](#serilog-file-sink-benchmarks)
- [Scenario Benchmarks](#scenario-benchmarks)
  - [Web API Request Simulation](#web-api-request-simulation)
  - [Background Worker Simulation](#background-worker-simulation)

---

## Results

### Encrypted Stream Benchmarks

Run 1:

| Method                     | BufferSize |         Mean |      Error |     StdDev |    Ratio | RatioSD |   Gen0 |   Gen1 | Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-----------:|-----------:|---------:|--------:|-------:|-------:|----------:|------------:|
| PlainMemoryStreamWrite     | 512        |     20.16 ns |   0.271 ns |   0.254 ns |     1.00 |    0.02 | 0.0105 |      - |     528 B |        1.00 |
| EncryptedMemoryStreamWrite | 512        | 26,535.12 ns |  79.309 ns |  66.226 ns | 1,316.39 |   16.29 | 0.0916 |      - |    5376 B |       10.18 |
|                            |            |              |            |            |          |         |        |        |           |             |
| PlainMemoryStreamWrite     | 1024       |     29.58 ns |   0.603 ns |   0.718 ns |     1.00 |    0.03 | 0.0191 |      - |     960 B |        1.00 |
| EncryptedMemoryStreamWrite | 1024       | 27,212.37 ns | 339.388 ns | 317.464 ns |   920.45 |   24.15 | 0.1526 |      - |    9176 B |        9.56 |
|                            |            |              |            |            |          |         |        |        |           |             |
| PlainMemoryStreamWrite     | 2048       |     54.55 ns |   1.118 ns |   1.639 ns |     1.00 |    0.04 | 0.0408 | 0.0001 |    2048 B |        1.00 |
| EncryptedMemoryStreamWrite | 2048       | 27,983.58 ns | 135.664 ns | 120.263 ns |   513.42 |   15.09 | 0.2747 |      - |   13992 B |        6.83 |

Run 2:

| Method                     | BufferSize |         Mean |      Error |     StdDev |    Ratio | RatioSD |   Gen0 |   Gen1 | Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-----------:|-----------:|---------:|--------:|-------:|-------:|----------:|------------:|
| PlainMemoryStreamWrite     | 512        |     23.84 ns |   0.495 ns |   0.530 ns |     1.00 |    0.03 | 0.0105 |      - |     528 B |        1.00 |
| EncryptedMemoryStreamWrite | 512        | 26,581.70 ns |  68.580 ns |  57.267 ns | 1,115.64 |   24.39 | 0.0916 |      - |    5504 B |       10.42 |
|                            |            |              |            |            |          |         |        |        |           |             |
| PlainMemoryStreamWrite     | 1024       |     34.90 ns |   0.713 ns |   0.821 ns |     1.00 |    0.03 | 0.0191 |      - |     960 B |        1.00 |
| EncryptedMemoryStreamWrite | 1024       | 27,227.10 ns | 118.212 ns | 104.792 ns |   780.61 |   18.12 | 0.1831 |      - |    9304 B |        9.69 |
|                            |            |              |            |            |          |         |        |        |           |             |
| PlainMemoryStreamWrite     | 2048       |     62.74 ns |   1.272 ns |   2.358 ns |     1.00 |    0.05 | 0.0408 | 0.0001 |    2048 B |        1.00 |
| EncryptedMemoryStreamWrite | 2048       | 28,190.59 ns |  99.900 ns |  88.559 ns |   449.90 |   16.43 | 0.2747 |      - |   14120 B |        6.89 |

#### Refactored Encrypted Stream Benchmarks

Run 1:

| Method                     | BufferSize |         Mean |        Error |       StdDev |    Ratio |  RatioSD |       Gen0 |       Gen1 |  Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-------------:|-------------:|---------:|---------:|-----------:|-----------:|-----------:|------------:|
| **PlainMemoryStreamWrite** | **512**    | **23.34 ns** | **0.071 ns** | **0.066 ns** | **1.00** | **0.00** | **0.0105** |      **-** |  **528 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 512        | 38,431.95 ns |    75.223 ns |    70.364 ns | 1,646.84 |     5.39 |     0.0610 |          - |     4976 B |        9.42 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **1024**   | **31.94 ns** | **0.119 ns** | **0.111 ns** | **1.00** | **0.00** | **0.0191** |      **-** |  **960 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 1024       | 38,514.00 ns |    43.001 ns |    38.119 ns | 1,205.83 |     4.24 |     0.0610 |          - |     5936 B |        6.18 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **2048**   | **58.41 ns** | **0.262 ns** | **0.245 ns** | **1.00** | **0.01** | **0.0408** | **0.0001** | **2048 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 2048       | 38,921.33 ns |    74.538 ns |    69.723 ns |   666.34 |     2.94 |     0.1221 |          - |     9152 B |        4.47 |

Run 2:

| Method                     | BufferSize |         Mean |        Error |       StdDev |    Ratio |  RatioSD |       Gen0 |       Gen1 |  Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-------------:|-------------:|---------:|---------:|-----------:|-----------:|-----------:|------------:|
| **PlainMemoryStreamWrite** | **512**    | **23.18 ns** | **0.119 ns** | **0.111 ns** | **1.00** | **0.01** | **0.0105** |      **-** |  **528 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 512        | 38,506.05 ns |   102.991 ns |    96.338 ns | 1,661.54 |     8.71 |     0.0610 |          - |     4976 B |        9.42 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **1024**   | **32.30 ns** | **0.180 ns** | **0.168 ns** | **1.00** | **0.01** | **0.0191** |      **-** |  **960 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 1024       | 38,492.57 ns |    93.557 ns |    87.513 ns | 1,191.65 |     6.56 |     0.0610 |          - |     5936 B |        6.18 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **2048**   | **58.22 ns** | **0.299 ns** | **0.280 ns** | **1.00** | **0.01** | **0.0408** | **0.0001** | **2048 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 2048       | 38,726.30 ns |    49.884 ns |    44.221 ns |   665.18 |     3.20 |     0.1831 |          - |     9192 B |        4.49 |

### Serilog File Sink Benchmarks

Run 1:

| Method                    | LogEntryCount | MessageSize |        Mean |     Error |      StdDev |      Median | Ratio | RatioSD | Completed Work Items | Lock Contentions |      Gen0 |      Gen1 |      Gen2 |   Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|------------:|----------:|------------:|------------:|------:|--------:|---------------------:|-----------------:|----------:|----------:|----------:|------------:|------------:|
| LogWithoutEncryption      | 100           | Large       |    887.4 us |   8.24 us |     6.43 us |    887.2 us |  1.00 |    0.01 |                    - |                - |    3.9063 |         - |         - |   235.19 KB |        1.00 |
| LogWithEncryption         | 100           | Large       |    938.1 us |  18.59 us |    37.98 us |    924.2 us |  1.06 |    0.04 |                    - |                - |    7.8125 |         - |         - |   462.16 KB |        1.97 |
| LogWithEncryptionBuffered | 100           | Large       |    634.9 us |  15.52 us |    44.03 us |    615.8 us |  0.72 |    0.05 |                    - |                - |    7.8125 |         - |         - |   420.49 KB |        1.79 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 100           | Medium      |    714.6 us |  12.83 us |    17.98 us |    707.2 us |  1.00 |    0.03 |                    - |                - |         - |         - |         - |    75.13 KB |        1.00 |
| LogWithEncryption         | 100           | Medium      |    745.0 us |  16.90 us |    47.11 us |    738.2 us |  1.04 |    0.07 |                    - |                - |    1.9531 |         - |         - |   162.25 KB |        2.16 |
| LogWithEncryptionBuffered | 100           | Medium      |    525.0 us |  11.15 us |    29.18 us |    522.1 us |  0.74 |    0.04 |                    - |                - |    1.9531 |         - |         - |   137.08 KB |        1.82 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 100           | Small       |    674.3 us |  13.26 us |    20.64 us |    677.4 us |  1.00 |    0.04 |                    - |                - |         - |         - |         - |    28.17 KB |        1.00 |
| LogWithEncryption         | 100           | Small       |    665.1 us |  11.17 us |     9.33 us |    664.9 us |  0.99 |    0.03 |                    - |                - |    1.9531 |         - |         - |   107.49 KB |        3.82 |
| LogWithEncryptionBuffered | 100           | Small       |    466.1 us |  17.86 us |    51.24 us |    447.7 us |  0.69 |    0.08 |                    - |                - |    0.9766 |         - |         - |    66.14 KB |        2.35 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Large       |  5,056.6 us | 133.51 us |   380.92 us |  4,915.2 us |  1.01 |    0.10 |                    - |                - |   31.2500 |         - |         - |  2225.83 KB |        1.00 |
| LogWithEncryption         | 1000          | Large       |  5,729.4 us | 113.12 us |   165.81 us |  5,679.7 us |  1.14 |    0.09 |                    - |                - |   78.1250 |         - |         - |  4372.42 KB |        1.96 |
| LogWithEncryptionBuffered | 1000          | Large       |  3,072.2 us |  61.29 us |   156.00 us |  3,038.0 us |  0.61 |    0.05 |                    - |                - |  398.4375 |  398.4375 |  398.4375 |  3706.82 KB |        1.67 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Medium      |  3,571.9 us |  51.31 us |    40.06 us |  3,568.3 us |  1.00 |    0.02 |                    - |                - |         - |         - |         - |   652.51 KB |        1.00 |
| LogWithEncryption         | 1000          | Medium      |  4,003.8 us |  78.71 us |   117.81 us |  3,975.5 us |  1.12 |    0.03 |                    - |                - |   23.4375 |         - |         - |  1400.58 KB |        2.15 |
| LogWithEncryptionBuffered | 1000          | Medium      |  1,393.1 us |  27.83 us |    21.73 us |  1,387.6 us |  0.39 |    0.01 |                    - |                - |  164.0625 |  164.0625 |  164.0625 |  1320.53 KB |        2.02 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Small       |  3,294.8 us |  65.04 us |   149.44 us |  3,239.3 us |  1.00 |    0.06 |                    - |                - |         - |         - |         - |   168.74 KB |        1.00 |
| LogWithEncryption         | 1000          | Small       |  3,444.5 us |  68.68 us |   110.90 us |  3,392.6 us |  1.05 |    0.06 |                    - |                - |   15.6250 |         - |         - |   838.71 KB |        4.97 |
| LogWithEncryptionBuffered | 1000          | Small       |    774.9 us |  14.97 us |    15.38 us |    772.0 us |  0.24 |    0.01 |                    - |                - |    5.8594 |    1.9531 |         - |   371.58 KB |        2.20 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Large       | 42,536.1 us | 250.25 us |   208.97 us | 42,461.6 us |  1.00 |    0.01 |                    - |                - |  375.0000 |         - |         - | 22265.07 KB |        1.00 |
| LogWithEncryption         | 10000         | Large       | 49,943.2 us | 514.15 us |   429.34 us | 49,869.4 us |  1.17 |    0.01 |                    - |                - |  800.0000 |         - |         - | 43607.71 KB |        1.96 |
| LogWithEncryptionBuffered | 10000         | Large       | 26,868.4 us | 527.66 us | 1,147.08 us | 26,323.2 us |  0.63 |    0.03 |                    - |                - | 1312.5000 | 1000.0000 | 1000.0000 |  43123.3 KB |        1.94 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Medium      | 30,448.2 us | 383.75 us |   320.45 us | 30,350.5 us |  1.00 |    0.01 |                    - |                - |  125.0000 |         - |         - |  6558.79 KB |        1.00 |
| LogWithEncryption         | 10000         | Medium      | 35,132.9 us | 571.50 us |   506.62 us | 34,990.2 us |  1.15 |    0.02 |                    - |                - |  266.6667 |         - |         - | 14057.19 KB |        2.14 |
| LogWithEncryptionBuffered | 10000         | Medium      |  9,269.4 us | 167.43 us |   148.42 us |  9,199.9 us |  0.30 |    0.01 |                    - |                - | 1171.8750 | 1046.8750 | 1046.8750 | 12086.89 KB |        1.84 |
|                           |               |             |             |           |             |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Small       | 25,694.5 us | 134.40 us |   112.23 us | 25,699.6 us |  1.00 |    0.01 |                    - |                - |         - |         - |         - |  1575.05 KB |        1.00 |
| LogWithEncryption         | 10000         | Small       | 30,024.4 us | 302.90 us |   283.34 us | 29,895.5 us |  1.17 |    0.01 |                    - |                - |  156.2500 |         - |         - |  8151.39 KB |        5.18 |
| LogWithEncryptionBuffered | 10000         | Small       |  4,448.4 us |  35.11 us |    29.32 us |  4,442.0 us |  0.17 |    0.00 |                    - |                - |  742.1875 |  742.1875 |  742.1875 |  4227.42 KB |        2.68 |

Run 2:

| Method                    | LogEntryCount | MessageSize |        Mean |     Error |    StdDev |      Median | Ratio | RatioSD | Completed Work Items | Lock Contentions |      Gen0 |      Gen1 |      Gen2 |   Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|------------:|----------:|----------:|------------:|------:|--------:|---------------------:|-----------------:|----------:|----------:|----------:|------------:|------------:|
| LogWithoutEncryption      | 100           | Large       |    902.5 us |  16.65 us |  13.90 us |    896.1 us |  1.00 |    0.02 |                    - |                - |    3.9063 |         - |         - |   235.19 KB |        1.00 |
| LogWithEncryption         | 100           | Large       |    923.2 us |  18.02 us |  18.50 us |    920.9 us |  1.02 |    0.03 |                    - |                - |    7.8125 |         - |         - |   462.16 KB |        1.97 |
| LogWithEncryptionBuffered | 100           | Large       |    628.5 us |  12.59 us |  33.37 us |    618.7 us |  0.70 |    0.04 |                    - |                - |    7.8125 |         - |         - |    420.5 KB |        1.79 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 100           | Medium      |    748.7 us |  13.47 us |  14.98 us |    747.1 us |  1.00 |    0.03 |                    - |                - |         - |         - |         - |    75.13 KB |        1.00 |
| LogWithEncryption         | 100           | Medium      |    715.2 us |   9.69 us |   8.10 us |    712.0 us |  0.96 |    0.02 |                    - |                - |    1.9531 |         - |         - |   162.26 KB |        2.16 |
| LogWithEncryptionBuffered | 100           | Medium      |    517.3 us |  24.02 us |  63.69 us |    495.3 us |  0.69 |    0.09 |                    - |                - |    1.9531 |         - |         - |   137.15 KB |        1.83 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 100           | Small       |    706.5 us |   9.90 us |   8.27 us |    706.7 us |  1.00 |    0.02 |                    - |                - |         - |         - |         - |    28.12 KB |        1.00 |
| LogWithEncryption         | 100           | Small       |    781.0 us |  25.95 us |  73.61 us |    767.8 us |  1.11 |    0.10 |                    - |                - |    1.9531 |         - |         - |   107.48 KB |        3.82 |
| LogWithEncryptionBuffered | 100           | Small       |    555.3 us |  20.24 us |  57.09 us |    540.7 us |  0.79 |    0.08 |                    - |                - |    0.9766 |         - |         - |    66.15 KB |        2.35 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Large       |  5,237.8 us | 113.95 us | 330.60 us |  5,229.8 us |  1.00 |    0.09 |                    - |                - |   31.2500 |         - |         - |  2225.85 KB |        1.00 |
| LogWithEncryption         | 1000          | Large       |  6,110.6 us | 135.84 us | 371.87 us |  5,978.7 us |  1.17 |    0.10 |                    - |                - |   78.1250 |         - |         - |  4372.42 KB |        1.96 |
| LogWithEncryptionBuffered | 1000          | Large       |  3,345.9 us |  72.12 us | 194.98 us |  3,313.2 us |  0.64 |    0.05 |                    - |                - |  398.4375 |  398.4375 |  398.4375 |  3706.82 KB |        1.67 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Medium      |  3,887.0 us |  81.49 us | 229.84 us |  3,812.2 us |  1.00 |    0.08 |                    - |                - |         - |         - |         - |   652.49 KB |        1.00 |
| LogWithEncryption         | 1000          | Medium      |  4,248.6 us |  77.82 us | 183.43 us |  4,206.5 us |  1.10 |    0.08 |                    - |                - |   23.4375 |         - |         - |  1400.58 KB |        2.15 |
| LogWithEncryptionBuffered | 1000          | Medium      |  1,482.9 us |  39.58 us | 105.64 us |  1,462.3 us |  0.38 |    0.03 |                    - |                - |  164.0625 |  164.0625 |  164.0625 |  1320.53 KB |        2.02 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 1000          | Small       |  3,131.1 us |  56.46 us |  52.81 us |  3,115.9 us |  1.00 |    0.02 |                    - |                - |         - |         - |         - |   168.74 KB |        1.00 |
| LogWithEncryption         | 1000          | Small       |  3,453.1 us |  53.71 us |  47.61 us |  3,443.9 us |  1.10 |    0.02 |                    - |                - |   15.6250 |         - |         - |   838.76 KB |        4.97 |
| LogWithEncryptionBuffered | 1000          | Small       |    768.4 us |   6.87 us |   7.64 us |    767.8 us |  0.25 |    0.00 |                    - |                - |    3.9063 |         - |         - |   371.58 KB |        2.20 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Large       | 42,244.9 us | 421.87 us | 394.61 us | 42,231.1 us |  1.00 |    0.01 |                    - |                - |  400.0000 |         - |         - |  22265.1 KB |        1.00 |
| LogWithEncryption         | 10000         | Large       | 50,063.2 us | 445.56 us | 394.98 us | 49,973.8 us |  1.19 |    0.01 |                    - |                - |  800.0000 |         - |         - | 43607.71 KB |        1.96 |
| LogWithEncryptionBuffered | 10000         | Large       | 26,469.6 us | 419.19 us | 392.11 us | 26,381.0 us |  0.63 |    0.01 |                    - |                - | 1312.5000 | 1000.0000 | 1000.0000 | 43123.31 KB |        1.94 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Medium      | 30,301.9 us | 347.69 us | 290.34 us | 30,133.7 us |  1.00 |    0.01 |                    - |                - |  125.0000 |         - |         - |  6558.78 KB |        1.00 |
| LogWithEncryption         | 10000         | Medium      | 34,858.6 us | 153.81 us | 120.09 us | 34,896.0 us |  1.15 |    0.01 |                    - |                - |  266.6667 |         - |         - | 14057.19 KB |        2.14 |
| LogWithEncryptionBuffered | 10000         | Medium      |  9,327.5 us | 177.33 us | 182.10 us |  9,330.9 us |  0.31 |    0.01 |                    - |                - | 1171.8750 | 1046.8750 | 1046.8750 | 12087.18 KB |        1.84 |
|                           |               |             |             |           |           |             |       |         |                      |                  |           |           |           |             |             |
| LogWithoutEncryption      | 10000         | Small       | 26,457.8 us | 511.60 us | 647.01 us | 26,089.8 us |  1.00 |    0.03 |                    - |                - |   31.2500 |         - |         - |  1575.01 KB |        1.00 |
| LogWithEncryption         | 10000         | Small       | 30,260.0 us | 206.22 us | 172.20 us | 30,234.8 us |  1.14 |    0.03 |                    - |                - |  156.2500 |         - |         - |  8151.39 KB |        5.18 |
| LogWithEncryptionBuffered | 10000         | Small       |  4,415.2 us |  66.52 us |  51.93 us |  4,407.0 us |  0.17 |    0.00 |                    - |                - |  742.1875 |  742.1875 |  742.1875 |   4227.5 KB |        2.68 |

#### Refactored Serilog File Sink Benchmarks

Run 1:

| Method                    | LogEntryCount | MessageSize |            Mean |           Error |           StdDev |          Median |    Ratio |   RatioSD |         Gen0 | Completed Work Items | Lock Contentions |       Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|----------------:|----------------:|-----------------:|----------------:|---------:|----------:|-------------:|---------------------:|-----------------:|----------------:|------------:|
| **LogWithoutEncryption**  | **100**       | **Large**   |    **741.7 μs** |     **4.55 μs** |      **3.55 μs** |    **741.9 μs** | **1.00** |  **0.01** |   **3.9063** |                **-** |            **-** |   **235.19 KB** |    **1.00** |
| LogWithEncryption         | 100           | Large       |        778.2 μs |        15.48 μs |         41.58 μs |        757.3 μs |     1.05 |      0.06 |       5.8594 |                    - |                - |       300.14 KB |        1.28 |
| LogWithEncryptionBuffered | 100           | Large       |        642.2 μs |        25.32 μs |         68.89 μs |        616.1 μs |     0.87 |      0.09 |       5.8594 |                    - |                - |       295.87 KB |        1.26 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Medium**  |    **624.3 μs** |    **11.86 μs** |      **9.26 μs** |    **621.3 μs** | **1.00** |  **0.02** |        **-** |                **-** |            **-** |    **75.13 KB** |    **1.00** |
| LogWithEncryption         | 100           | Medium      |        646.7 μs |        12.58 μs |         20.66 μs |        640.7 μs |     1.04 |      0.04 |       1.9531 |                    - |                - |       109.69 KB |        1.46 |
| LogWithEncryptionBuffered | 100           | Medium      |        427.9 μs |        10.79 μs |         30.08 μs |        420.5 μs |     0.69 |      0.05 |       1.9531 |                    - |                - |       106.47 KB |        1.42 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Small**   |  **7,451.6 μs** | **4,009.27 μs** | **11,821.42 μs** |    **575.5 μs** | **9.41** | **18.23** |        **-** |                **-** |            **-** |    **28.17 KB** |    **1.00** |
| LogWithEncryption         | 100           | Small       |        561.1 μs |         7.81 μs |          6.52 μs |        561.0 μs |     0.71 |      0.43 |       0.9766 |                    - |                - |        54.91 KB |        1.95 |
| LogWithEncryptionBuffered | 100           | Small       |        367.1 μs |         6.75 μs |         16.67 μs |        364.4 μs |     0.46 |      0.28 |       0.9766 |                    - |                - |        50.99 KB |        1.81 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Large**   |  **4,255.2 μs** |    **24.41 μs** |     **22.83 μs** |  **4,249.4 μs** | **1.00** |  **0.01** |  **31.2500** |                **-** |            **-** |  **2225.85 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Large       |      4,556.5 μs |        26.42 μs |         22.06 μs |      4,556.8 μs |     1.07 |      0.01 |      46.8750 |                    - |                - |      2754.97 KB |        1.24 |
| LogWithEncryptionBuffered | 1000          | Large       |      2,712.3 μs |        21.04 μs |         16.42 μs |      2,713.2 μs |     0.64 |      0.00 |      54.6875 |                    - |                - |      2713.71 KB |        1.22 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Medium**  |  **2,960.4 μs** |    **21.64 μs** |     **19.19 μs** |  **2,964.2 μs** | **1.00** |  **0.01** |   **7.8125** |                **-** |            **-** |   **652.49 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Medium      |      3,244.9 μs |        21.68 μs |         19.22 μs |      3,247.3 μs |     1.10 |      0.01 |      15.6250 |                    - |                - |       876.98 KB |        1.34 |
| LogWithEncryptionBuffered | 1000          | Medium      |      1,176.0 μs |        18.22 μs |         14.22 μs |      1,179.6 μs |     0.40 |      0.01 |      15.6250 |                    - |                - |       818.38 KB |        1.25 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Small**   |  **2,613.6 μs** |    **21.85 μs** |     **19.37 μs** |  **2,619.2 μs** | **1.00** |  **0.01** |        **-** |                **-** |            **-** |   **168.74 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Small       |      2,821.9 μs |        47.08 μs |         36.75 μs |      2,808.3 μs |     1.08 |      0.02 |            - |                    - |                - |        315.1 KB |        1.87 |
| LogWithEncryptionBuffered | 1000          | Small       |        707.4 μs |        11.72 μs |         13.03 μs |        702.2 μs |     0.27 |      0.01 |       3.9063 |                    - |                - |       248.03 KB |        1.47 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Large**   | **38,918.1 μs** |   **645.77 μs** |    **905.29 μs** | **38,849.5 μs** | **1.00** |  **0.03** | **375.0000** |                **-** |            **-** | **22265.07 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Large       |     41,315.5 μs |       283.00 μs |        250.87 μs |     41,247.2 μs |     1.06 |      0.02 |     500.0000 |                    - |                - |     27505.57 KB |        1.24 |
| LogWithEncryptionBuffered | 10000         | Large       |     23,035.1 μs |       266.23 μs |        236.01 μs |     23,047.6 μs |     0.59 |      0.01 |     531.2500 |                    - |                - |     27052.23 KB |        1.22 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Medium**  | **26,534.4 μs** |   **203.49 μs** |    **180.39 μs** | **26,554.2 μs** | **1.00** |  **0.01** | **125.0000** |                **-** |            **-** |  **6558.78 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Medium      |     29,145.1 μs |       181.88 μs |        170.13 μs |     29,187.0 μs |     1.10 |      0.01 |     156.2500 |                    - |                - |      8752.17 KB |        1.33 |
| LogWithEncryptionBuffered | 10000         | Medium      |      8,373.5 μs |        56.96 μs |         50.49 μs |      8,370.0 μs |     0.32 |      0.00 |     156.2500 |                    - |                - |      8088.73 KB |        1.23 |
|                           |               |             |                 |                 |                  |                 |          |           |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Small**   | **22,567.2 μs** |   **231.93 μs** |    **193.67 μs** | **22,598.3 μs** | **1.00** |  **0.01** |  **31.2500** |                **-** |            **-** |  **1575.01 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Small       |     25,104.1 μs |       143.52 μs |        127.22 μs |     25,120.8 μs |     1.11 |      0.01 |      31.2500 |                    - |                - |      2916.73 KB |        1.85 |
| LogWithEncryptionBuffered | 10000         | Small       |      4,210.9 μs |        80.96 μs |         71.77 μs |      4,183.8 μs |     0.19 |      0.00 |      31.2500 |                    - |                - |      2218.79 KB |        1.41 |

Run 2:

| Method                    | LogEntryCount | MessageSize |            Mean |         Error |        StdDev |          Median |    Ratio |  RatioSD |         Gen0 | Completed Work Items | Lock Contentions |       Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|----------------:|--------------:|--------------:|----------------:|---------:|---------:|-------------:|---------------------:|-----------------:|----------------:|------------:|
| **LogWithoutEncryption**  | **100**       | **Large**   |    **734.8 μs** |   **7.47 μs** |   **5.83 μs** |    **734.3 μs** | **1.00** | **0.01** |   **3.9063** |                **-** |            **-** |   **235.19 KB** |    **1.00** |
| LogWithEncryption         | 100           | Large       |        783.5 μs |      15.53 μs |      33.76 μs |        773.0 μs |     1.07 |     0.05 |       5.8594 |                    - |                - |       300.22 KB |        1.28 |
| LogWithEncryptionBuffered | 100           | Large       |        611.0 μs |      21.22 μs |      57.36 μs |        584.1 μs |     0.83 |     0.08 |       5.8594 |                    - |                - |       295.87 KB |        1.26 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Medium**  | **25,185.7 μs** | **382.53 μs** | **319.43 μs** | **25,180.2 μs** | **1.00** | **0.02** |        **-** |                **-** |            **-** |    **75.22 KB** |    **1.00** |
| LogWithEncryption         | 100           | Medium      |        643.9 μs |      12.76 μs |      27.20 μs |        633.1 μs |     0.03 |     0.00 |       1.9531 |                    - |                - |       109.75 KB |        1.46 |
| LogWithEncryptionBuffered | 100           | Medium      |        387.6 μs |       7.61 μs |       7.48 μs |        384.7 μs |     0.02 |     0.00 |       1.9531 |                    - |                - |       106.47 KB |        1.42 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Small**   |    **562.5 μs** |   **3.31 μs** |   **4.06 μs** |    **562.5 μs** | **1.00** | **0.01** |        **-** |                **-** |            **-** |    **28.13 KB** |    **1.00** |
| LogWithEncryption         | 100           | Small       |        569.0 μs |      11.05 μs |      15.85 μs |        562.4 μs |     1.01 |     0.03 |       0.9766 |                    - |                - |        54.85 KB |        1.95 |
| LogWithEncryptionBuffered | 100           | Small       |        354.8 μs |       7.00 μs |      11.70 μs |        351.7 μs |     0.63 |     0.02 |       0.9766 |                    - |                - |        50.99 KB |        1.81 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Large**   |  **4,220.4 μs** |  **74.23 μs** |  **61.99 μs** |  **4,205.0 μs** | **1.00** | **0.02** |  **31.2500** |                **-** |            **-** |  **2225.85 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Large       |      4,512.2 μs |      74.67 μs |      58.29 μs |      4,499.0 μs |     1.07 |     0.02 |      46.8750 |                    - |                - |      2754.96 KB |        1.24 |
| LogWithEncryptionBuffered | 1000          | Large       |      2,658.5 μs |      41.28 μs |      36.60 μs |      2,661.7 μs |     0.63 |     0.01 |      54.6875 |                    - |                - |      2713.64 KB |        1.22 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Medium**  |  **2,989.5 μs** |  **16.73 μs** |  **14.83 μs** |  **2,988.4 μs** | **1.00** | **0.01** |   **7.8125** |                **-** |            **-** |   **652.48 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Medium      |      3,240.2 μs |      15.12 μs |      12.62 μs |      3,239.4 μs |     1.08 |     0.01 |      15.6250 |                    - |                - |       876.98 KB |        1.34 |
| LogWithEncryptionBuffered | 1000          | Medium      |      1,151.2 μs |      22.10 μs |      17.25 μs |      1,147.9 μs |     0.39 |     0.01 |      15.6250 |                    - |                - |       818.37 KB |        1.25 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Small**   |  **2,569.2 μs** |  **42.73 μs** |  **41.97 μs** |  **2,557.9 μs** | **1.00** | **0.02** |        **-** |                **-** |            **-** |   **168.89 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Small       |      2,878.5 μs |      47.22 μs |      44.17 μs |      2,876.2 μs |     1.12 |     0.02 |            - |                    - |                - |       315.09 KB |        1.87 |
| LogWithEncryptionBuffered | 1000          | Small       |        701.2 μs |       5.71 μs |       4.77 μs |        701.0 μs |     0.27 |     0.00 |       3.9063 |                    - |                - |       248.07 KB |        1.47 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Large**   | **39,068.5 μs** | **555.90 μs** | **617.88 μs** | **39,021.4 μs** | **1.00** | **0.02** | **444.4444** |                **-** |            **-** | **22265.04 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Large       |     41,731.5 μs |     200.28 μs |     187.34 μs |     41,720.8 μs |     1.07 |     0.02 |     500.0000 |                    - |                - |      27505.5 KB |        1.24 |
| LogWithEncryptionBuffered | 10000         | Large       |     22,772.2 μs |     170.72 μs |     159.69 μs |     22,691.4 μs |     0.58 |     0.01 |     531.2500 |                    - |                - |     27052.23 KB |        1.22 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Medium**  | **25,958.9 μs** | **117.63 μs** |  **98.22 μs** | **25,940.3 μs** | **1.00** | **0.01** | **125.0000** |                **-** |            **-** |  **6558.78 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Medium      |     29,266.1 μs |     242.83 μs |     227.14 μs |     29,288.3 μs |     1.13 |     0.01 |     156.2500 |                    - |                - |      8752.16 KB |        1.33 |
| LogWithEncryptionBuffered | 10000         | Medium      |      8,425.6 μs |     155.33 μs |     137.70 μs |      8,372.7 μs |     0.32 |     0.01 |     156.2500 |                    - |                - |      8088.78 KB |        1.23 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Small**   | **22,529.9 μs** | **424.42 μs** | **397.00 μs** | **22,358.1 μs** | **1.00** | **0.02** |  **31.2500** |                **-** |            **-** |  **1575.01 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Small       |     25,293.8 μs |     162.36 μs |     151.87 μs |     25,308.5 μs |     1.12 |     0.02 |      31.2500 |                    - |                - |      2916.67 KB |        1.85 |
| LogWithEncryptionBuffered | 10000         | Small       |      4,232.4 μs |      83.31 μs |      81.82 μs |      4,190.6 μs |     0.19 |     0.00 |      39.0625 |                    - |                - |      2218.78 KB |        1.41 |

### Simulated Scenario Benchmarks

#### Web API Request Simulation

Run 1:

| Method                               | RequestCount |       Mean |    Error |   StdDev | Ratio | RatioSD | Completed Work Items | Lock Contentions |    Gen0 | Allocated | Alloc Ratio |
|--------------------------------------|--------------|-----------:|---------:|---------:|------:|--------:|---------------------:|-----------------:|--------:|----------:|------------:|
| SimulateApiRequestsWithoutEncryption | 100          |   814.8 us | 15.18 us | 13.46 us |  1.00 |    0.02 |                    - |                - |  1.9531 | 114.28 KB |        1.00 |
| SimulateApiRequestsWithEncryption    | 100          |   789.4 us |  8.34 us |  6.97 us |  0.97 |    0.02 |                    - |                - |  3.9063 | 215.11 KB |        1.88 |
|                                      |              |            |          |          |       |         |                      |                  |         |           |             |
| SimulateApiRequestsWithoutEncryption | 1000         | 4,073.2 us | 45.79 us | 38.24 us |  1.00 |    0.01 |                    - |                - |       - | 999.16 KB |        1.00 |
| SimulateApiRequestsWithEncryption    | 1000         | 4,675.1 us | 26.33 us | 21.98 us |  1.15 |    0.01 |                    - |                - | 31.2500 | 1889.3 KB |        1.89 |

Run 2:

| Method                               | RequestCount |       Mean |     Error |    StdDev | Ratio | RatioSD | Completed Work Items | Lock Contentions |    Gen0 |  Allocated | Alloc Ratio |
|--------------------------------------|--------------|-----------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|--------:|-----------:|------------:|
| SimulateApiRequestsWithoutEncryption | 100          |   872.6 us |  17.11 us |  16.00 us |  1.00 |    0.02 |                    - |                - |  1.9531 |  114.28 KB |        1.00 |
| SimulateApiRequestsWithEncryption    | 100          |   904.2 us |  26.63 us |  71.07 us |  1.04 |    0.08 |                    - |                - |  3.9063 |  215.13 KB |        1.88 |
|                                      |              |            |           |           |       |         |                      |                  |         |            |             |
| SimulateApiRequestsWithoutEncryption | 1000         | 4,460.7 us |  88.87 us | 185.51 us |  1.00 |    0.06 |                    - |                - | 15.6250 |   999.1 KB |        1.00 |
| SimulateApiRequestsWithEncryption    | 1000         | 5,117.5 us | 101.66 us | 253.17 us |  1.15 |    0.07 |                    - |                - | 31.2500 | 1889.29 KB |        1.89 |

##### Refactored Web API Request Simulation

Run 1:

| Method                                   | RequestCount |           Mean |        Error |       StdDev |         Median |    Ratio |  RatioSD |        Gen0 | Completed Work Items | Lock Contentions |     Allocated | Alloc Ratio |
|------------------------------------------|--------------|---------------:|-------------:|-------------:|---------------:|---------:|---------:|------------:|---------------------:|-----------------:|--------------:|------------:|
| **SimulateApiRequestsWithoutEncryption** | **100**      |   **703.8 μs** |  **7.00 μs** |  **5.85 μs** |   **704.1 μs** | **1.00** | **0.01** |       **-** |                **-** |            **-** | **114.41 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 100          |       711.0 μs |     14.16 μs |     32.25 μs |       694.6 μs |     1.01 |     0.05 |      1.9531 |                    - |                - |     153.53 KB |        1.34 |
|                                          |              |                |              |              |                |          |          |             |                      |                  |               |             |
| **SimulateApiRequestsWithoutEncryption** | **1000**     | **3,570.5 μs** | **32.99 μs** | **27.55 μs** | **3,574.5 μs** | **1.00** | **0.01** | **15.6250** |                **-** |            **-** |  **999.1 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 1000         |     3,910.3 μs |     51.10 μs |     42.67 μs |     3,908.3 μs |     1.10 |     0.01 |     23.4375 |                    - |                - |    1271.17 KB |        1.27 |

Run 2:

| Method                                   | RequestCount |           Mean |        Error |       StdDev |    Ratio |        Gen0 | Completed Work Items | Lock Contentions |     Allocated | Alloc Ratio |
|------------------------------------------|--------------|---------------:|-------------:|-------------:|---------:|------------:|---------------------:|-----------------:|--------------:|------------:|
| **SimulateApiRequestsWithoutEncryption** | **100**      |   **681.6 μs** |  **3.75 μs** |  **2.93 μs** | **1.00** |  **1.9531** |                **-** |            **-** | **114.28 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 100          |       691.9 μs |      9.45 μs |      7.89 μs |     1.02 |           - |                    - |                - |     153.48 KB |        1.34 |
|                                          |              |                |              |              |          |             |                      |                  |               |             |
| **SimulateApiRequestsWithoutEncryption** | **1000**     | **3,594.7 μs** | **30.86 μs** | **25.77 μs** | **1.00** | **15.6250** |                **-** |            **-** |  **999.1 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 1000         |     3,870.1 μs |     44.80 μs |     39.72 μs |     1.08 |     23.4375 |                    - |                - |    1271.22 KB |        1.27 |

#### Background Worker Simulation

Run 1:

| Method                                    | MessageCount |     Mean |     Error |    StdDev | Ratio | RatioSD | Completed Work Items | Lock Contentions |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
|-------------------------------------------|--------------|---------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|---------:|---------:|---------:|----------:|------------:|
| SimulateBackgroundWorkerWithoutEncryption | 5000         | 3.211 ms | 0.0440 ms | 0.0368 ms |  1.00 |    0.02 |                    - |                - |  31.2500 |        - |        - |   2.18 MB |        1.00 |
| SimulateBackgroundWorkerWithEncryption    | 5000         | 3.420 ms | 0.0282 ms | 0.0236 ms |  1.07 |    0.01 |                    - |                - | 390.6250 | 390.6250 | 390.6250 |   3.58 MB |        1.64 |
|                                           |              |          |           |           |       |         |                      |                  |          |          |          |           |             |
| SimulateBackgroundWorkerWithoutEncryption | 10000        | 6.080 ms | 0.1176 ms | 0.1723 ms |  1.00 |    0.04 |                    - |                - |  85.9375 |        - |        - |   4.34 MB |        1.00 |
| SimulateBackgroundWorkerWithEncryption    | 10000        | 6.446 ms | 0.0673 ms | 0.0597 ms |  1.06 |    0.03 |                    - |                - | 757.8125 | 671.8750 | 671.8750 |   7.13 MB |        1.64 |

Run 2:

| Method                                    | MessageCount |     Mean |     Error |    StdDev | Ratio | RatioSD | Completed Work Items | Lock Contentions |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
|-------------------------------------------|--------------|---------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|---------:|---------:|---------:|----------:|------------:|
| SimulateBackgroundWorkerWithoutEncryption | 5000         | 3.208 ms | 0.0404 ms | 0.0358 ms |  1.00 |    0.02 |                    - |                - |  31.2500 |        - |        - |   2.18 MB |        1.00 |
| SimulateBackgroundWorkerWithEncryption    | 5000         | 3.373 ms | 0.0300 ms | 0.0250 ms |  1.05 |    0.01 |                    - |                - | 398.4375 | 398.4375 | 398.4375 |   3.58 MB |        1.64 |
|                                           |              |          |           |           |       |         |                      |                  |          |          |          |           |             |
| SimulateBackgroundWorkerWithoutEncryption | 10000        | 6.025 ms | 0.1066 ms | 0.0890 ms |  1.00 |    0.02 |                    - |                - |  85.9375 |        - |        - |   4.34 MB |        1.00 |
| SimulateBackgroundWorkerWithEncryption    | 10000        | 6.480 ms | 0.0676 ms | 0.0599 ms |  1.08 |    0.02 |                    - |                - | 757.8125 | 671.8750 | 671.8750 |   7.13 MB |        1.64 |

##### Refactored Background Worker Simulation

Run 1:

| Method                                        | MessageCount |         Mean |         Error |        StdDev |    Ratio |  RatioSD |        Gen0 | Completed Work Items | Lock Contentions |   Allocated | Alloc Ratio |
|-----------------------------------------------|--------------|-------------:|--------------:|--------------:|---------:|---------:|------------:|---------------------:|-----------------:|------------:|------------:|
| **SimulateBackgroundWorkerWithoutEncryption** | **5000**     | **2.930 ms** | **0.0361 ms** | **0.0320 ms** | **1.00** | **0.01** | **31.2500** |                **-** |            **-** | **2.18 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 5000         |     3.332 ms |     0.0378 ms |     0.0492 ms |     1.14 |     0.02 |           - |                    - |                - |     2.61 MB |        1.20 |
|                                               |              |              |               |               |          |          |             |                      |                  |             |             |
| **SimulateBackgroundWorkerWithoutEncryption** | **10000**    | **5.813 ms** | **0.0441 ms** | **0.0391 ms** | **1.00** | **0.01** | **62.5000** |                **-** |            **-** | **4.34 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 10000        |     6.186 ms |     0.0812 ms |     0.0678 ms |     1.06 |     0.01 |    101.5625 |                    - |                - |     5.19 MB |        1.20 |

Run 2:

| Method                                        | MessageCount |         Mean |         Error |        StdDev |    Ratio |  RatioSD |        Gen0 | Completed Work Items | Lock Contentions |   Allocated | Alloc Ratio |
|-----------------------------------------------|--------------|-------------:|--------------:|--------------:|---------:|---------:|------------:|---------------------:|-----------------:|------------:|------------:|
| **SimulateBackgroundWorkerWithoutEncryption** | **5000**     | **3.010 ms** | **0.0352 ms** | **0.0312 ms** | **1.00** | **0.01** | **31.2500** |                **-** |            **-** | **2.18 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 5000         |     3.345 ms |     0.0364 ms |     0.0390 ms |     1.11 |     0.02 |     46.8750 |                    - |                - |     2.61 MB |        1.20 |
|                                               |              |              |               |               |          |          |             |                      |                  |             |             |
| **SimulateBackgroundWorkerWithoutEncryption** | **10000**    | **5.729 ms** | **0.0796 ms** | **0.0664 ms** | **1.00** | **0.02** | **85.9375** |                **-** |            **-** | **4.34 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 10000        |     6.131 ms |     0.0673 ms |     0.0596 ms |     1.07 |     0.02 |    101.5625 |                    - |                - |     5.19 MB |        1.20 |
