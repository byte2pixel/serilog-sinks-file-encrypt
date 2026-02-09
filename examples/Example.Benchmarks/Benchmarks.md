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

| Method                     | BufferSize |         Mean |        Error |       StdDev |    Ratio |  RatioSD |       Gen0 |  Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-------------:|-------------:|---------:|---------:|-----------:|-----------:|------------:|
| **PlainMemoryStreamWrite** | **512**    | **24.08 ns** | **0.121 ns** | **0.101 ns** | **1.00** | **0.01** | **0.0105** |  **528 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 512        | 38,664.38 ns |    77.790 ns |    72.764 ns | 1,605.36 |     7.12 |     0.0610 |     4496 B |        8.52 |
|                            |            |              |              |              |          |          |            |            |             |
| **PlainMemoryStreamWrite** | **1024**   | **32.62 ns** | **0.164 ns** | **0.153 ns** | **1.00** | **0.01** | **0.0191** |  **960 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 1024       | 38,658.60 ns |    30.808 ns |    28.818 ns | 1,185.19 |     5.48 |     0.0610 |     5024 B |        5.23 |
|                            |            |              |              |              |          |          |            |            |             |
| **PlainMemoryStreamWrite** | **2048**   | **57.80 ns** | **0.290 ns** | **0.257 ns** | **1.00** | **0.01** | **0.0404** | **2032 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 2048       | 38,926.02 ns |   104.531 ns |    97.779 ns |   673.51 |     3.33 |     0.1221 |     7200 B |        3.54 |

Run 2:

| Method                     | BufferSize |         Mean |        Error |       StdDev |    Ratio |  RatioSD |       Gen0 |       Gen1 |  Allocated | Alloc Ratio |
|----------------------------|------------|-------------:|-------------:|-------------:|---------:|---------:|-----------:|-----------:|-----------:|------------:|
| **PlainMemoryStreamWrite** | **512**    | **23.39 ns** | **0.067 ns** | **0.059 ns** | **1.00** | **0.00** | **0.0105** |      **-** |  **528 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 512        | 38,479.93 ns |    62.861 ns |    55.724 ns | 1,645.29 |     4.64 |     0.0610 |          - |     4496 B |        8.52 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **1024**   | **31.87 ns** | **0.156 ns** | **0.145 ns** | **1.00** | **0.01** | **0.0191** |      **-** |  **960 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 1024       | 38,579.88 ns |    49.957 ns |    44.286 ns | 1,210.61 |     5.51 |     0.0610 |          - |     5024 B |        5.23 |
|                            |            |              |              |              |          |          |            |            |            |             |
| **PlainMemoryStreamWrite** | **2048**   | **57.97 ns** | **0.175 ns** | **0.155 ns** | **1.00** | **0.00** | **0.0408** | **0.0001** | **2048 B** |    **1.00** |
| EncryptedMemoryStreamWrite | 2048       | 38,903.03 ns |    48.255 ns |    42.776 ns |   671.14 |     1.88 |     0.1221 |          - |     7200 B |        3.52 |

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

| Method                    | LogEntryCount | MessageSize |            Mean |         Error |        StdDev |          Median |    Ratio |  RatioSD | Completed Work Items | Lock Contentions |         Gen0 |       Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|----------------:|--------------:|--------------:|----------------:|---------:|---------:|---------------------:|-----------------:|-------------:|----------------:|------------:|
| **LogWithoutEncryption**  | **100**       | **Large**   |    **739.2 μs** |   **5.70 μs** |   **4.76 μs** |    **739.1 μs** | **1.00** | **0.01** |                **-** |            **-** |   **3.9063** |   **235.19 KB** |    **1.00** |
| LogWithEncryption         | 100           | Large       |        778.7 μs |      15.38 μs |      30.72 μs |        764.3 μs |     1.05 |     0.04 |                    - |                - |       3.9063 |       251.78 KB |        1.07 |
| LogWithEncryptionBuffered | 100           | Large       |        604.9 μs |      12.05 μs |      33.79 μs |        602.8 μs |     0.82 |     0.05 |                    - |                - |       3.9063 |       250.09 KB |        1.06 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **100**       | **Medium**  |    **601.1 μs** |   **8.66 μs** |   **7.68 μs** |    **600.7 μs** | **1.00** | **0.02** |                **-** |            **-** |   **0.9766** |    **75.13 KB** |    **1.00** |
| LogWithEncryption         | 100           | Medium      |        607.3 μs |       3.62 μs |       3.02 μs |        607.1 μs |     1.01 |     0.01 |                    - |                - |            - |        91.72 KB |        1.22 |
| LogWithEncryptionBuffered | 100           | Medium      |        391.6 μs |       7.06 μs |       8.67 μs |        389.3 μs |     0.65 |     0.02 |                    - |                - |       0.9766 |         92.1 KB |        1.23 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **100**       | **Small**   |    **568.2 μs** |  **10.96 μs** |  **12.18 μs** |    **568.3 μs** | **1.00** | **0.03** |                **-** |            **-** |        **-** |    **28.14 KB** |    **1.00** |
| LogWithEncryption         | 100           | Small       |        559.8 μs |       8.00 μs |       6.68 μs |        558.0 μs |     0.99 |     0.02 |                    - |                - |            - |         44.7 KB |        1.59 |
| LogWithEncryptionBuffered | 100           | Small       |        349.7 μs |       5.38 μs |       4.20 μs |        350.1 μs |     0.62 |     0.01 |                    - |                - |            - |        44.82 KB |        1.59 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Large**   |  **4,153.8 μs** |  **37.08 μs** |  **32.87 μs** |  **4,151.8 μs** | **1.00** | **0.01** |                **-** |            **-** |  **31.2500** |  **2225.83 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Large       |      4,512.2 μs |      18.05 μs |      15.07 μs |      4,505.9 μs |     1.09 |     0.01 |                    - |                - |      31.2500 |      2270.59 KB |        1.02 |
| LogWithEncryptionBuffered | 1000          | Large       |      2,623.5 μs |      23.40 μs |      19.54 μs |      2,621.5 μs |     0.63 |     0.01 |                    - |                - |      39.0625 |      2253.26 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Medium**  |  **2,949.4 μs** |  **20.52 μs** |  **17.14 μs** |  **2,953.9 μs** | **1.00** | **0.01** |                **-** |            **-** |   **7.8125** |   **652.49 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Medium      |      3,229.1 μs |      10.61 μs |       8.86 μs |      3,226.8 μs |     1.09 |     0.01 |                    - |                - |       7.8125 |       697.21 KB |        1.07 |
| LogWithEncryptionBuffered | 1000          | Medium      |      1,145.2 μs |      11.29 μs |       9.43 μs |      1,146.1 μs |     0.39 |     0.00 |                    - |                - |      11.7188 |       673.45 KB |        1.03 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Small**   |  **2,584.5 μs** |  **30.46 μs** |  **28.50 μs** |  **2,573.9 μs** | **1.00** | **0.02** |                **-** |            **-** |        **-** |   **168.76 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Small       |      2,843.8 μs |      16.65 μs |      13.00 μs |      2,845.6 μs |     1.10 |     0.01 |                    - |                - |            - |       213.52 KB |        1.27 |
| LogWithEncryptionBuffered | 1000          | Small       |        712.3 μs |      14.15 μs |      19.36 μs |        703.0 μs |     0.28 |     0.01 |                    - |                - |       1.9531 |       187.11 KB |        1.11 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Large**   | **38,731.7 μs** | **270.44 μs** | **239.74 μs** | **38,642.1 μs** | **1.00** | **0.01** |                **-** |            **-** | **375.0000** | **22265.07 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Large       |     41,800.5 μs |     198.49 μs |     165.75 μs |     41,858.1 μs |     1.08 |     0.01 |                    - |                - |     416.6667 |     22591.49 KB |        1.01 |
| LogWithEncryptionBuffered | 10000         | Large       |     22,603.5 μs |     237.17 μs |     221.85 μs |     22,666.6 μs |     0.58 |     0.01 |                    - |                - |     437.5000 |     22418.27 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Medium**  | **26,325.8 μs** |  **79.64 μs** |  **62.18 μs** | **26,311.5 μs** | **1.00** | **0.00** |                **-** |            **-** | **125.0000** |  **6558.78 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Medium      |     29,624.2 μs |      83.19 μs |      64.95 μs |     29,636.9 μs |     1.13 |     0.00 |                    - |                - |     125.0000 |      6884.88 KB |        1.05 |
| LogWithEncryptionBuffered | 10000         | Medium      |      8,444.4 μs |      41.58 μs |      34.72 μs |      8,439.1 μs |     0.32 |     0.00 |                    - |                - |     125.0000 |       6619.6 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |                      |                  |              |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Small**   | **22,458.7 μs** | **164.13 μs** | **137.05 μs** | **22,445.4 μs** | **1.00** | **0.01** |                **-** |            **-** |  **31.2500** |  **1575.01 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Small       |     25,642.8 μs |      87.40 μs |      77.48 μs |     25,640.4 μs |     1.14 |     0.01 |                    - |                - |      31.2500 |      1901.09 KB |        1.21 |
| LogWithEncryptionBuffered | 10000         | Small       |      4,137.7 μs |      52.08 μs |      48.71 μs |      4,125.3 μs |     0.18 |     0.00 |                    - |                - |      31.2500 |      1609.93 KB |        1.02 |

Run 2:

| Method                    | LogEntryCount | MessageSize |            Mean |         Error |        StdDev |          Median |    Ratio |  RatioSD |         Gen0 | Completed Work Items | Lock Contentions |       Allocated | Alloc Ratio |
|---------------------------|---------------|-------------|----------------:|--------------:|--------------:|----------------:|---------:|---------:|-------------:|---------------------:|-----------------:|----------------:|------------:|
| **LogWithoutEncryption**  | **100**       | **Large**   |    **743.7 μs** |   **3.35 μs** |   **2.62 μs** |    **743.9 μs** | **1.00** | **0.00** |   **3.9063** |                **-** |            **-** |   **235.19 KB** |    **1.00** |
| LogWithEncryption         | 100           | Large       |        764.7 μs |      11.30 μs |      12.09 μs |        761.2 μs |     1.03 |     0.02 |       3.9063 |                    - |                - |       251.85 KB |        1.07 |
| LogWithEncryptionBuffered | 100           | Large       |        592.3 μs |      11.55 μs |      24.61 μs |        579.3 μs |     0.80 |     0.03 |       3.9063 |                    - |                - |       250.09 KB |        1.06 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Medium**  |    **588.5 μs** |   **7.76 μs** |   **7.62 μs** |    **588.7 μs** | **1.00** | **0.02** |        **-** |                **-** |            **-** |    **75.16 KB** |    **1.00** |
| LogWithEncryption         | 100           | Medium      |        636.1 μs |      12.68 μs |      28.09 μs |        623.6 μs |     1.08 |     0.05 |            - |                    - |                - |        91.72 KB |        1.22 |
| LogWithEncryptionBuffered | 100           | Medium      |        386.2 μs |       7.55 μs |       6.31 μs |        384.2 μs |     0.66 |     0.01 |       0.9766 |                    - |                - |         92.1 KB |        1.23 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **100**       | **Small**   |    **582.9 μs** |  **11.07 μs** |   **9.24 μs** |    **579.0 μs** | **1.00** | **0.02** |        **-** |                **-** |            **-** |    **28.11 KB** |    **1.00** |
| LogWithEncryption         | 100           | Small       |        564.9 μs |       3.85 μs |       3.41 μs |        564.3 μs |     0.97 |     0.02 |            - |                    - |                - |        44.77 KB |        1.59 |
| LogWithEncryptionBuffered | 100           | Small       |        353.7 μs |       6.67 μs |       6.85 μs |        353.2 μs |     0.61 |     0.01 |            - |                    - |                - |         44.9 KB |        1.60 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Large**   |  **4,140.3 μs** |  **55.71 μs** |  **52.11 μs** |  **4,136.9 μs** | **1.00** | **0.02** |  **31.2500** |                **-** |            **-** |  **2225.83 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Large       |      4,449.5 μs |      49.80 μs |      44.14 μs |      4,453.1 μs |     1.07 |     0.02 |      31.2500 |                    - |                - |      2270.59 KB |        1.02 |
| LogWithEncryptionBuffered | 1000          | Large       |      2,628.8 μs |      18.36 μs |      15.33 μs |      2,625.7 μs |     0.64 |     0.01 |      39.0625 |                    - |                - |      2253.26 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Medium**  |  **2,986.9 μs** |  **12.42 μs** |  **10.37 μs** |  **2,987.1 μs** | **1.00** | **0.00** |   **7.8125** |                **-** |            **-** |   **652.49 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Medium      |      3,251.3 μs |      22.94 μs |      19.15 μs |      3,242.9 μs |     1.09 |     0.01 |       7.8125 |                    - |                - |       697.27 KB |        1.07 |
| LogWithEncryptionBuffered | 1000          | Medium      |      2,265.0 μs |      42.52 μs |      35.50 μs |      2,258.7 μs |     0.76 |     0.01 |            - |                    - |                - |       673.49 KB |        1.03 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **1000**      | **Small**   |  **2,582.4 μs** |  **11.66 μs** |   **9.74 μs** |  **2,584.6 μs** | **1.00** | **0.01** |        **-** |                **-** |            **-** |   **168.74 KB** |    **1.00** |
| LogWithEncryption         | 1000          | Small       |      2,858.2 μs |      14.19 μs |      11.08 μs |      2,860.0 μs |     1.11 |     0.01 |            - |                    - |                - |       213.46 KB |        1.27 |
| LogWithEncryptionBuffered | 1000          | Small       |        692.4 μs |      12.16 μs |      10.78 μs |        689.9 μs |     0.27 |     0.00 |       1.9531 |                    - |                - |       187.11 KB |        1.11 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Large**   | **38,645.3 μs** | **762.06 μs** | **748.45 μs** | **38,598.8 μs** | **1.00** | **0.03** | **384.6154** |                **-** |            **-** | **22265.02 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Large       |     42,136.3 μs |     175.71 μs |     164.36 μs |     42,167.1 μs |     1.09 |     0.02 |     416.6667 |                    - |                - |     22591.48 KB |        1.01 |
| LogWithEncryptionBuffered | 10000         | Large       |     22,778.0 μs |     302.93 μs |     252.96 μs |     22,701.8 μs |     0.59 |     0.01 |     437.5000 |                    - |                - |     22418.26 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Medium**  | **26,632.9 μs** | **142.54 μs** | **126.35 μs** | **26,615.7 μs** | **1.00** | **0.01** | **125.0000** |                **-** |            **-** |  **6558.78 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Medium      |     29,470.8 μs |     140.01 μs |     130.96 μs |     29,480.1 μs |     1.11 |     0.01 |     125.0000 |                    - |                - |      6884.95 KB |        1.05 |
| LogWithEncryptionBuffered | 10000         | Medium      |      8,307.0 μs |      22.59 μs |      18.87 μs |      8,306.4 μs |     0.31 |     0.00 |     125.0000 |                    - |                - |      6619.67 KB |        1.01 |
|                           |               |             |                 |               |               |                 |          |          |              |                      |                  |                 |             |
| **LogWithoutEncryption**  | **10000**     | **Small**   | **22,628.2 μs** | **142.96 μs** | **119.38 μs** | **22,638.2 μs** | **1.00** | **0.01** |  **31.2500** |                **-** |            **-** |  **1575.01 KB** |    **1.00** |
| LogWithEncryption         | 10000         | Small       |     25,682.4 μs |     157.40 μs |     147.23 μs |     25,646.9 μs |     1.14 |     0.01 |      31.2500 |                    - |                - |       1901.1 KB |        1.21 |
| LogWithEncryptionBuffered | 10000         | Small       |      4,146.8 μs |      79.17 μs |      88.00 μs |      4,102.4 μs |     0.18 |     0.00 |      31.2500 |                    - |                - |      1609.94 KB |        1.02 |

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

| Method                                   | RequestCount |           Mean |        Error |       StdDev |    Ratio | Completed Work Items | Lock Contentions |       Gen0 |     Allocated | Alloc Ratio |
|------------------------------------------|--------------|---------------:|-------------:|-------------:|---------:|---------------------:|-----------------:|-----------:|--------------:|------------:|
| **SimulateApiRequestsWithoutEncryption** | **100**      |   **687.7 μs** |  **4.45 μs** |  **3.47 μs** | **1.00** |                **-** |            **-** | **1.9531** | **114.28 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 100          |       685.7 μs |      4.06 μs |      3.39 μs |     1.00 |                    - |                - |     1.9531 |     131.46 KB |        1.15 |
|                                          |              |                |              |              |          |                      |                  |            |               |             |
| **SimulateApiRequestsWithoutEncryption** | **1000**     | **3,565.4 μs** | **30.18 μs** | **26.76 μs** | **1.00** |                **-** |            **-** |      **-** | **999.12 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 1000         |     3,937.5 μs |     22.83 μs |     19.07 μs |     1.10 |                    - |                - |    15.6250 |    1049.45 KB |        1.05 |

Run 2:

| Method                                   | RequestCount |           Mean |        Error |       StdDev |    Ratio |  RatioSD |       Gen0 | Completed Work Items | Lock Contentions |     Allocated | Alloc Ratio |
|------------------------------------------|--------------|---------------:|-------------:|-------------:|---------:|---------:|-----------:|---------------------:|-----------------:|--------------:|------------:|
| **SimulateApiRequestsWithoutEncryption** | **100**      |   **707.0 μs** | **13.88 μs** | **28.66 μs** | **1.00** | **0.05** | **1.9531** |                **-** |            **-** | **114.28 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 100          |       699.2 μs |     13.91 μs |     19.94 μs |     0.99 |     0.05 |     1.9531 |                    - |                - |     131.48 KB |        1.15 |
|                                          |              |                |              |              |          |          |            |                      |                  |               |             |
| **SimulateApiRequestsWithoutEncryption** | **1000**     | **3,556.7 μs** | **27.30 μs** | **26.82 μs** | **1.00** | **0.01** |      **-** |                **-** |            **-** | **999.11 KB** |    **1.00** |
| SimulateApiRequestsWithEncryption        | 1000         |     4,017.7 μs |     26.66 μs |     23.64 μs |     1.13 |     0.01 |    15.6250 |                    - |                - |    1049.45 KB |        1.05 |

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

| Method                                        | MessageCount |         Mean |         Error |        StdDev |    Ratio | Completed Work Items | Lock Contentions |        Gen0 |   Allocated | Alloc Ratio |
|-----------------------------------------------|--------------|-------------:|--------------:|--------------:|---------:|---------------------:|-----------------:|------------:|------------:|------------:|
| **SimulateBackgroundWorkerWithoutEncryption** | **5000**     | **3.075 ms** | **0.0212 ms** | **0.0188 ms** | **1.00** |                **-** |            **-** | **31.2500** | **2.18 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 5000         |     3.259 ms |     0.0241 ms |     0.0201 ms |     1.06 |                    - |                - |     31.2500 |      2.2 MB |        1.01 |
|                                               |              |              |               |               |          |                      |                  |             |             |             |
| **SimulateBackgroundWorkerWithoutEncryption** | **10000**    | **5.652 ms** | **0.0547 ms** | **0.0485 ms** | **1.00** |                **-** |            **-** | **85.9375** | **4.34 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 10000        |     6.065 ms |     0.0543 ms |     0.0481 ms |     1.07 |                    - |                - |     85.9375 |     4.38 MB |        1.01 |

Run 2:

| Method                                        | MessageCount |         Mean |         Error |        StdDev |    Ratio |        Gen0 | Completed Work Items | Lock Contentions |   Allocated | Alloc Ratio |
|-----------------------------------------------|--------------|-------------:|--------------:|--------------:|---------:|------------:|---------------------:|-----------------:|------------:|------------:|
| **SimulateBackgroundWorkerWithoutEncryption** | **5000**     | **3.052 ms** | **0.0317 ms** | **0.0297 ms** | **1.00** | **31.2500** |                **-** |            **-** | **2.18 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 5000         |     3.142 ms |     0.0358 ms |     0.0317 ms |     1.03 |     31.2500 |                    - |                - |      2.2 MB |        1.01 |
|                                               |              |              |               |               |          |             |                      |                  |             |             |
| **SimulateBackgroundWorkerWithoutEncryption** | **10000**    | **5.680 ms** | **0.0350 ms** | **0.0292 ms** | **1.00** | **85.9375** |                **-** |            **-** | **4.34 MB** |    **1.00** |
| SimulateBackgroundWorkerWithEncryption        | 10000        |     6.128 ms |     0.0343 ms |     0.0304 ms |     1.08 |     85.9375 |                    - |                - |     4.38 MB |        1.01 |
