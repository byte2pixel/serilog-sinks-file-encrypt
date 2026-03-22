# Serilog.Sinks.File.Encrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?component=encrypt-lib&token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) hook that encrypts log files using RSA and AES-GCM hybrid encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

> [!WARNING]
> **v3.0.0 is a breaking change from v2.x.**
> Log files written by v2 cannot be appended to or read by v3. See the [Migration Guide](#migration-from-v2x-to-v300) below and the full [CHANGELOG](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md) before upgrading.

## Features

- **Hybrid Encryption**: Uses RSA for key exchange and AES-GCM for efficient, authenticated data encryption
- **Key Rotation**: Assign a key ID to `EncryptHooks`; the decryption layer selects the correct private key automatically
- **Seamless Integration**: Plugs directly into [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) using file lifecycle hooks
- **CLI Tool Integration**: Companion CLI tool for key generation and simple log decryption scenarios
- **Optimal Performance**: Optimized encryption performance using hybrid encryption

## Use Cases

- Secure logging of sensitive application data, especially in desktop applications
- Compliance with data protection regulations by encrypting log files
- Protection against unauthorized access to log files in shared or cloud environments

## Performance

Production-ready performance with minimal overhead:

- ✅ **8-12% time overhead** in real-world unbuffered scenarios (well under typical targets)
- ✅ **300K+ logs/second** throughput with buffered writes (vs. ~174K baseline)
- ✅ **AES-GCM: ~1.03–1.07x memory overhead** — near-baseline; AES: ~2.02–2.15x
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

For key management and decryption capabilities, also install the CLI tool:

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

> **💡 Performance Tip:** For high-volume scenarios where you can tolerate potential data loss on crashes, use `buffered: true` to reduce overhead from 15-17% to 6-8%. See the [Advanced Usage](#advanced-usage) section below.

### 3. Decrypt Logs

Use the CLI tool to decrypt your log files:

```bash
# Pass the key ID that was used during encryption
serilog-encrypt decrypt logs/app.log -k ./keys/private_key.xml --id my-app-key-2026

# If no key ID was assigned (keyId defaults to ""), --id can be omitted
serilog-encrypt decrypt logs/app.log -k ./keys/private_key.xml
```

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
        buffered: true,              // Enables high-performance mode
        flushToDiskInterval: TimeSpan.FromSeconds(1),
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

**Encryption side** — set a new `keyId` when you rotate keys:

```csharp
// Old deployment — key from 2025
hooks: new EncryptHooks(oldPublicKey, keyId: "my-app-key-2025")

// New deployment — key from 2026
hooks: new EncryptHooks(newPublicKey, keyId: "my-app-key-2026")
```

**Decryption side** — supply all relevant private keys in `DecryptionOptions.DecryptionKeys`:

```csharp
using Serilog.Sinks.File.Encrypt;
using Serilog.Sinks.File.Encrypt.Models;

string oldPrivateKey = File.ReadAllText("./keys/private_key_2025.xml");
string newPrivateKey = File.ReadAllText("./keys/private_key_2026.xml");

var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string>
    {
        { "my-app-key-2025", oldPrivateKey },
        { "my-app-key-2026", newPrivateKey },
    }
};

using var input  = File.OpenRead("logs/app.log");
using var output = File.Create("logs/app-decrypted.log");
await CryptographicUtils.DecryptLogFileAsync(input, output, options);
```

### Programmatic Key Generation

```csharp
using Serilog.Sinks.File.Encrypt;

// Generate a new RSA key pair
var (publicKey, privateKey) = CryptographicUtils.GenerateRsaKeyPair();

// Save keys to files
File.WriteAllText("public_key.xml", publicKey);
File.WriteAllText("private_key.xml", privateKey);
```

### Programmatic Decryption

For large files, use the memory-optimized streaming API:

```csharp
using Serilog.Sinks.File.Encrypt;
using Serilog.Sinks.File.Encrypt.Models;

// Build decryption options with one or more keys
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string>
    {
        { "my-app-key-2026", File.ReadAllText("private_key.xml") },
    },
    ErrorHandlingMode = ErrorHandlingMode.Skip  // default
};

// File-to-file decryption
await CryptographicUtils.DecryptLogFileAsync(
    "logs/app.log",
    "logs/decrypted.log",
    options);

// Stream-to-stream decryption
using var input  = File.OpenRead("large-log.encrypted");
using var output = File.Create("large-log.decrypted");
await CryptographicUtils.DecryptLogFileAsync(input, output, options);
```

### Error Handling Modes

Choose how to handle decryption errors:

```csharp
using Serilog.Sinks.File.Encrypt.Models;

// Skip corrupted sections silently (DEFAULT — ideal for JSON/structured logs)
var skipOptions = new DecryptionOptions
{
    DecryptionKeys = ...,
    ErrorHandlingMode = ErrorHandlingMode.Skip  // This is the default
};

// Throw exception on first error (strict validation)
var strictOptions = new DecryptionOptions
{
    DecryptionKeys = ...,
    ErrorHandlingMode = ErrorHandlingMode.ThrowException
};

await CryptographicUtils.DecryptLogFileAsync(input, output, skipOptions);
```

### Error Handling Use Cases

**Skip Mode** - Recommended for all production use (default):
```csharp
// Continues past corrupted sections; output is clean and safe for structured log parsers.
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string> { { "key-id", privateKey } },
    ErrorHandlingMode = ErrorHandlingMode.Skip
};

// Without audit logging — errors are silently skipped
await CryptographicUtils.DecryptLogFileAsync(input, output, options);

// With audit logging — pass any Serilog ILogger to capture skipped-error details
ILogger auditLogger = new LoggerConfiguration()
    .WriteTo.File("decryption-audit.log")
    .CreateLogger();
await CryptographicUtils.DecryptLogFileAsync(input, output, options, auditLogger);
```

**ThrowException Mode** - For Data Integrity Validation:
```csharp
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string> { { "key-id", privateKey } },
    ErrorHandlingMode = ErrorHandlingMode.ThrowException
};

try
{
    await CryptographicUtils.DecryptLogFileAsync(input, output, options);
}
catch (CryptographicException ex)
{
    Console.WriteLine($"Decryption failed: {ex.Message}");
}
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
(string publicKey, string privateKey) CryptographicUtils.GenerateRsaKeyPair(int keySize = 2048, KeyFormat format = KeyFormat.Xml)
```

### Encryption Hook
```csharp
// publicKey — RSA public key in XML or PEM format
// keyId     — optional identifier embedded in every session header (max 32 bytes UTF-8); default ""
// version   — header format version; default 1
new EncryptHooks(string publicKey, string keyId = "", int version = 1)
```

### Decryption
```csharp
// Stream-to-stream async decryption
Task<DecryptionResult> CryptographicUtils.DecryptLogFileAsync(
    Stream inputStream,
    Stream outputStream,
    DecryptionOptions options,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)

// File-to-file async decryption
Task CryptographicUtils.DecryptLogFileAsync(
    string encryptedFilePath,
    string outputFilePath,
    DecryptionOptions options,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
```

### DecryptionOptions
```csharp
public sealed record DecryptionOptions
{
    // Dictionary of key ID → RSA private key (XML or PEM).
    // Use "" as the key when no keyId was assigned during encryption.
    public required Dictionary<string, string> DecryptionKeys { get; init; }

    // How decryption errors are handled. Default: Skip.
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = ErrorHandlingMode.Skip;
}
```

### ErrorHandlingMode
```csharp
public enum ErrorHandlingMode
{
    Skip            = 0,  // Skip errors silently (DEFAULT — safe for all log formats)
    ThrowException  = 1   // Throw on first error
}
```

### DecryptionResult
```csharp
public class DecryptionResult
{
    public int DecryptedSessions { get; init; }
    public int DecryptedMessages { get; init; }
    public int FailedHeaders     { get; init; }
    public int FailedMessages    { get; init; }
    public int ResyncAttempts    { get; init; }
}
```

## Security Considerations

- Keep private keys secure and never include them in your application deployment
- Store private keys in secure key management systems in production (Azure Key Vault, AWS Secrets Manager, etc.)
- Use 2048-bit RSA keys minimum (4096-bit for enhanced security)
- Restrict filesystem access to encrypted log files and private keys
- Rotate keys periodically and use the `keyId` parameter to track which key encrypted which files

## CLI Tool

The companion CLI tool provides key management and decryption with full error handling:

```bash
# Generate keys
serilog-encrypt generate --output /path/to/keys

# Decrypt a single file (key ID matches the value used during encryption)
serilog-encrypt decrypt app.log -k private_key.xml --id my-app-key-2026

# Decrypt without a key ID (when keyId was left as the default "")
serilog-encrypt decrypt app.log -k private_key.xml

# Decrypt all .log files using a glob pattern
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --id my-app-key-2026

# Decrypt with audit log
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --id my-app-key-2026 --audit-log audit.log
```

For detailed CLI documentation, see the [CLI tool documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli).

## Migration from v2.x to v3.0.0

> [!IMPORTANT]
> **Log files written by v2 cannot be read by v3.** Archive or decrypt your existing log files with the v2 CLI before deploying v3.

See the full [Migration Guide in CHANGELOG.md](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md#migration-guide-v2--v3) for step-by-step instructions.

**Summary of required code changes:**

| Area                    | v2                            | v3                                                                             |
|-------------------------|-------------------------------|--------------------------------------------------------------------------------|
| Encryption hook         | `new EncryptHooks(publicKey)` | `new EncryptHooks(publicKey, keyId: "...")` *(keyId optional but recommended)* |
| Decryption class        | `EncryptionUtils`             | `CryptographicUtils`                                                           |
| Decryption options type | `StreamingOptions`            | `DecryptionOptions`                                                            |
| Private key argument    | `string privateKey`           | `DecryptionOptions { DecryptionKeys = { { "id", key } } }`                     |

## Requirements

- .NET 8.0 or higher
- A project using [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)
- RSA key pair for encryption/decryption in XML or PEM format (generated via CLI tool or programmatically)
