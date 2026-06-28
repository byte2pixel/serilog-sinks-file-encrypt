# Serilog.Sinks.File.Decrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Decrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Decrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A library for decrypting log files encrypted with [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt).

> [!WARNING]
> **v5.0.0 — New package**
> Decryption types have moved out of `Serilog.Sinks.File.Encrypt` into this package. Namespaces have changed:
>
> | v4.x (`Serilog.Sinks.File.Encrypt`) | v5.0.0 (`Serilog.Sinks.File.Decrypt`) |
> |--------------------------------------|----------------------------------------|
> | `LogReader` | `LogReader` |
> | `LocalKeyProvider` | `LocalKeyProvider` |
> | `Models.DecryptionOptions` | `Models.DecryptionOptions` |
> | `Models.DecryptionResult` | `Models.DecryptionResult` |
> | `Models.ErrorHandlingMode` | `Models.ErrorHandlingMode` |
> | `Interfaces.IKeyProvider` | `Interfaces.IKeyProvider` |
> | `CryptographicUtils.DecryptLogFileAsync(...)` | `DecryptionUtils.DecryptLogFileAsync(...)` |

## Installation

```bash
dotnet add package Serilog.Sinks.File.Decrypt
```

For ad-hoc decryption from the command line, install the CLI tool:

```bash
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

## Quick Start

```csharp
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

// Load your RSA private key
string privateKey = File.ReadAllText("private_key.xml");

var options = new DecryptionOptions
{
    KeyProvider = new LocalKeyProvider("my-app-key-2026", privateKey)
};

// Decrypt a file
DecryptionResult result = await DecryptionUtils.DecryptLogFileAsync(
    "logs/app.log",
    "logs/app-decrypted.log",
    options
);
```

## Key Rotation

When your application has rotated keys over time, supply all active private keys to `LocalKeyProvider`. The key ID embedded in each session header selects the correct key automatically.

```csharp
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

string oldPrivateKey = File.ReadAllText("./keys/private_key_2025.xml");
string newPrivateKey = File.ReadAllText("./keys/private_key_2026.xml");

var keyMap = new Dictionary<string, string>
{
    { "my-app-key-2025", oldPrivateKey },
    { "my-app-key-2026", newPrivateKey },
};

using LocalKeyProvider keyProvider = new LocalKeyProvider(keyMap);
var options = new DecryptionOptions { KeyProvider = keyProvider };

await DecryptionUtils.DecryptLogFileAsync("logs/app.log", "logs/app-decrypted.log", options);
```

> [!NOTE]
> The CLI tool only supports one key per invocation. For mixed-key directories, use `LocalKeyProvider` with a full key map as shown above, or implement `IKeyProvider` to integrate with your key management system.

## Programmatic Decryption

```csharp
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

var keyMap = new Dictionary<string, string>
{
    { "my-app-key-2026", File.ReadAllText("private_key.xml") },
};
using LocalKeyProvider keyProvider = new LocalKeyProvider(keyMap);

var options = new DecryptionOptions { KeyProvider = keyProvider };

// File-to-file
await DecryptionUtils.DecryptLogFileAsync("logs/app.log", "logs/decrypted.log", options);

// Stream-to-stream (memory-efficient for large files)
using var input  = File.OpenRead("large-log.encrypted");
using var output = File.Create("large-log.decrypted");
await DecryptionUtils.DecryptLogFileAsync(input, output, options);
```

## Error Handling

Choose how the decryption engine responds to corrupted or unreadable sections:

```csharp
// Skip corrupted sections silently (DEFAULT — recommended for production)
var skipOptions = new DecryptionOptions
{
    KeyProvider = keyProvider,
    ErrorHandlingMode = ErrorHandlingMode.Skip
};

// With audit logging — pass any Serilog ILogger to capture error details
ILogger auditLogger = new LoggerConfiguration()
    .WriteTo.File("decryption-audit.log")
    .CreateLogger();
await DecryptionUtils.DecryptLogFileAsync(input, output, skipOptions, auditLogger);

// Throw on first error (strict validation)
var strictOptions = new DecryptionOptions
{
    KeyProvider = keyProvider,
    ErrorHandlingMode = ErrorHandlingMode.ThrowException
};

try
{
    await DecryptionUtils.DecryptLogFileAsync(input, output, strictOptions);
}
catch (CryptographicException ex)
{
    Console.WriteLine($"Decryption failed: {ex.Message}");
}
```

## Custom Key Provider

Implement `IKeyProvider` to integrate with external key management systems (Azure Key Vault, AWS KMS, etc.):

```csharp
using Serilog.Sinks.File.Decrypt.Interfaces;

public class AzureKeyVaultProvider : IKeyProvider
{
    public async Task<byte[]> DecryptAsync(
        string keyId,
        ReadOnlyMemory<byte> cipherText,
        CancellationToken cancellationToken = default)
    {
        // Use Azure SDK to decrypt with the key identified by keyId
        // ...
    }

    public async Task<int> GetKeySizeAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        // Return the RSA key size in bits for the given keyId
        // ...
    }
}
```

## API Reference

### DecryptionUtils
```csharp
// Stream-to-stream async decryption
Task<DecryptionResult> DecryptionUtils.DecryptLogFileAsync(
    Stream inputStream,
    Stream outputStream,
    DecryptionOptions options,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)

// File-to-file async decryption
Task<DecryptionResult> DecryptionUtils.DecryptLogFileAsync(
    string encryptedFilePath,
    string outputFilePath,
    DecryptionOptions options,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
```

### IKeyProvider
```csharp
public interface IKeyProvider
{
    // Decrypts the AES-GCM session key using the RSA private key for the given keyId.
    Task<byte[]> DecryptAsync(
        string keyId,
        ReadOnlyMemory<byte> cipherText,
        CancellationToken cancellationToken = default);

    // Returns the RSA key size in bits for the given keyId.
    // Used to determine how many bytes to read from the session header.
    Task<int> GetKeySizeAsync(string keyId, CancellationToken cancellationToken = default);
}
```

### DecryptionOptions
```csharp
public sealed record DecryptionOptions
{
    public required IKeyProvider KeyProvider { get; init; }
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = ErrorHandlingMode.Skip;
}
```

### ErrorHandlingMode
```csharp
public enum ErrorHandlingMode
{
    Skip           = 0,  // Skip corrupted sections silently (DEFAULT)
    ThrowException = 1   // Throw CryptographicException on first error
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

- Keep private keys secure; never include them in application deployment artifacts
- Store private keys in secure key management systems in production (Azure Key Vault, AWS Secrets Manager, etc.)
- Restrict filesystem access to encrypted log files and private keys
- Use 2048-bit RSA keys minimum (4096-bit for enhanced security)

## Requirements

- **.NET 8.0** (LTS) or **.NET 10.0** (LTS), or a compatible higher runtime — see the [support policy](https://github.com/byte2pixel/serilog-sinks-file-encrypt#-net-support-policy)
- Log files created with [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt) v3.0.0 or later

## Related Packages

| Package | Purpose |
|---------|---------|
| [`Serilog.Sinks.File.Encrypt`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt) | Encrypts log files written by Serilog |
| [`Serilog.Sinks.File.Encrypt.Cli`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli) | CLI tool for key generation and ad-hoc log decryption |
| [`Serilog.Sinks.File.Encrypt.Core`](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core) | Shared cryptographic primitives (transitive dependency) |

