# Serilog.Sinks.File.Encrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?component=encrypt-lib&token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) hook that encrypts log files using RSA and AES-GCM hybrid encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

> [!WARNING]
> **v5.0.0 Breaking Changes**
> Split the NuGet package `Serilog.Sinks.File.Encrypt` into 3 separate packages:
>   - `Serilog.Sinks.File.Encrypt` (this package — the file hook for encryption only)
>   - [`Serilog.Sinks.File.Decrypt`](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt) (decryption library — `LogReader`, `LocalKeyProvider`, `DecryptionOptions`, `DecryptionUtils`, `IKeyProvider`)
>   - [`Serilog.Sinks.File.Encrypt.Core`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core) (shared cryptographic primitives — transitive dependency, no direct reference needed)

## Features

- **Hybrid Encryption**: Uses RSA for key exchange and AES-GCM for efficient, authenticated data encryption
- **Key Rotation**: Assign a key ID to `EncryptHooks`; the decryption layer selects the correct private key automatically
- **Seamless Integration**: Plugs directly into [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) using file lifecycle hooks
- **Optimal Performance**: Optimized encryption performance using hybrid encryption

## Use Cases

- Secure logging of sensitive application data, especially in desktop applications
- Compliance with data protection regulations by encrypting log files
- Protection against unauthorized access to log files in shared or cloud environments

## Performance

Production-ready performance with minimal overhead:

- ✅ **8-12% time overhead** in real-world unbuffered scenarios (well under typical targets)
- ✅ **300K+ logs/second** throughput with buffered writes (vs. ~174K baseline)
- ✅ **AES-GCM: ~1.03–1.07x memory overhead** — near-baseline
- ✅ **~5-16% throughput reduction** with unbuffered encryption (148K–153K logs/sec)
- 🚀 **Buffered mode outperforms non-encrypted unbuffered by ~66%** — encrypted buffered I/O is faster than plain unbuffered
- ✅ **Zero lock contentions** — safe for multithreaded applications handled by Serilog.File.Sink

**Buffering Trade-off:** While buffered writes provide excellent performance, they carry a risk of data loss if the application crashes before flushing. **Always call `Log.CloseAndFlush()` on application shutdown.**

For detailed benchmarks and analysis, see the [Benchmark Documentation](https://github.com/byte2pixel/serilog-sinks-file-encrypt/tree/main/examples/Example.Benchmarks#readme).

## Installation

Install the package via NuGet:

```bash
dotnet add package Serilog.Sinks.File.Encrypt
```

For decryption in your application, install the Decrypt package:

```bash
dotnet add package Serilog.Sinks.File.Decrypt
```

For key generation and ad-hoc log decryption, install the CLI tool:

```bash
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

## Quick Start

### 1. Generate RSA Key Pair

Generate an RSA key pair using the CLI tool:

```bash
serilog-encrypt generate --output ./keys
```

This creates:
- `public_key.xml`: Used for encryption (safe to include with your application)
- `private_key.xml`: Used for decryption (keep secure, do not distribute)

### 2. Configure Serilog with Encryption

```csharp
using Serilog;
using Serilog.Sinks.File.Encrypt;

// Load your public key
string publicKeyXml = File.ReadAllText("./keys/public_key.xml");

// Configure Serilog with encryption.
// Assign a key ID to support future key rotation (recommended).
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        hooks: new EncryptHooks(publicKeyXml, keyId: "my-app-key-2026"))
    .CreateLogger();

// Log as usual
Log.Information("This message will be encrypted!");

// Always flush on shutdown
Log.CloseAndFlush();
```

> **💡 Performance Tip:** For high-volume scenarios where you can tolerate potential data loss on crashes, use `buffered: true`. See the [Advanced Usage](#advanced-usage) section below.

### 3. Decrypt Logs

To decrypt log files, see the [Serilog.Sinks.File.Decrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt) package for programmatic decryption, or the [Serilog.Sinks.File.Encrypt.Cli](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli) tool for ad-hoc decryption.

## Advanced Usage

### High-Performance Configuration (Buffered Mode)

For high-volume logging scenarios where you can tolerate potential data loss on crashes:

```csharp
using Serilog;
using Serilog.Sinks.File.Encrypt;

string publicKeyXml = File.ReadAllText("./keys/public_key.xml");

Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        buffered: true, // buffered writes
        flushToDiskInterval: TimeSpan.FromSeconds(5), // flush every X seconds (adjust as needed)
        hooks: new EncryptHooks(publicKeyXml, keyId: "my-app-key-2026"))
    .CreateLogger();

// CRITICAL: Always flush on shutdown
Log.CloseAndFlush();
```

⚠️ **Warning:** Buffered writes risk data loss on crashes. Only use when:
- Application has reliable shutdown handling
- You can tolerate loss of recent logs (up to `flushToDiskInterval`)
- Performance is critical (background workers, high-volume systems)

⚠️ **Minor Risks**
- Theoretical nonce reuse on crash with corrupted header (extremely low probability)
- Nonce counter wrapping not explicitly handled (would require 2^96 encryptions per session)
  - At 1 million logs/second, this would take 2.5 trillion years to reach.

### Key Rotation

Assign a unique `keyId` to each key generation cycle. The ID is embedded in every session header
so the decryption layer knows which private key to use without any manual lookup.

```csharp
// Old deployment — key from 2025
hooks: new EncryptHooks(oldPublicKey, keyId: "my-app-key-2025")

// New deployment — key from 2026
hooks: new EncryptHooks(newPublicKey, keyId: "my-app-key-2026")
```

For the decryption side of key rotation, see the [Serilog.Sinks.File.Decrypt documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt#readme-body-tab).

### Programmatic Key Generation

```csharp
using Serilog.Sinks.File.Encrypt;

// Generate a new RSA key pair
var (publicKey, privateKey) = CryptographicUtils.GenerateRsaKeyPair();

// Save keys to files
File.WriteAllText("public_key.xml", publicKey);
File.WriteAllText("private_key.xml", privateKey);
```

### Web Application Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load public key from configuration
string publicKeyXml = builder.Configuration["Logging:PublicKeyXml"]
    ?? throw new InvalidOperationException("Logging:PublicKeyXml is required");

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.File(
            path: "logs/webapp-.log",
            rollingInterval: RollingInterval.Day,
            hooks: new EncryptHooks(publicKeyXml, keyId: "webapp-key-2026")));

var app = builder.Build();
app.Run();
```

## API Reference

### Key Management
```csharp
// Generates a new RSA key pair with the specified key size and format (XML or PEM).
(string publicKey, string privateKey) CryptographicUtils.GenerateRsaKeyPair(int keySize = 2048, KeyFormat format = KeyFormat.Xml)
```

### Encryption Hook
```csharp
// publicKey — RSA public key in XML or PEM format
// keyId     — optional identifier embedded in every session header (max 32 bytes UTF-8); default ""
// version   — header format version; default 1 - Obsolete and has no effect anymore.
new EncryptHooks(string publicKey, string keyId = "", int version = 1)
```

## Security Considerations

- Keep private keys secure and never include them in your application deployment
- Store private keys in secure key management systems in production (Azure Key Vault, AWS Secrets Manager, etc.)
- Use 2048-bit RSA keys minimum (4096-bit for enhanced security)
- Restrict filesystem access to encrypted log files and private keys
- Rotate keys periodically and use the `keyId` parameter to track which key encrypted which files

## Migration

For step-by-step migration guides, see the [CHANGELOG.md](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md):

- [v4.x → v5.0.0](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md#migration-guide-v4--v5)
- [v3.x → v4.0.0](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md#400---2026-04-05)
- [v2.x → v3.0.0](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md#migration-guide-v2--v3)

## Requirements## Requirements

- **.NET 8.0** (LTS) or **.NET 10.0** (LTS), or a compatible higher runtime
- A project using [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)
- RSA key pair in XML or PEM format (generated via CLI tool or programmatically)

> **Support policy:** This library targets .NET Long-Term Support (LTS) releases only. A new LTS TFM is added when it ships; the oldest LTS TFM is dropped when Microsoft ends support for it. Users on STS or EOL runtimes can pin an older package version that targets a compatible LTS TFM.

## Related Packages

| Package                                                                                             | Purpose                                                 |
|-----------------------------------------------------------------------------------------------------|---------------------------------------------------------|
| [`Serilog.Sinks.File.Decrypt`](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt)           | Programmatic decryption of encrypted log files          |
| [`Serilog.Sinks.File.Encrypt.Cli`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)   | CLI tool for key generation and ad-hoc log decryption   |
| [`Serilog.Sinks.File.Encrypt.Core`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core) | Shared cryptographic primitives (transitive dependency) |

