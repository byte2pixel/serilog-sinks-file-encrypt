# Serilog.Sinks.File.Encrypt

[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) hook that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## Features

- **Hybrid Encryption**: Uses RSA encryption for key exchange and AES for efficient data encryption
- **Seamless Integration**: Plugs directly into [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) using file lifecycle hooks
- **Memory-Optimized**: Producer-consumer architecture for efficient processing of large files
- **CLI Tool Integration**: Companion CLI tool for key generation and log decryption
- **Optimal Performance**: Optimized encryption performance using hybrid encryption.

## Use Cases

- Secure logging of sensitive application data especially in desktop applications.
- Compliance with data protection regulations by encrypting log files.
- Protection against unauthorized access to log files in shared or cloud environments.

There is overhead due to encryption; suitable for scenarios where security is prioritized over raw performance.

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

// Configure Serilog with encryption
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        hooks: new EncryptHooks(publicKeyXml))
    .CreateLogger();

// Log as usual
Log.Information("This message will be encrypted!");
Log.CloseAndFlush();
```

### 3. Decrypt Logs

Use the CLI tool to decrypt your log files:

```bash
serilog-encrypt decrypt --key ./keys/private_key.xml --file logs/app.log --output logs/app-decrypted.log
```

## Advanced Usage

### Programmatic Key Generation

```csharp
using Serilog.Sinks.File.Encrypt;

// Generate a new RSA key pair
var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair(4096);

// Save keys to files
File.WriteAllText("public_key.xml", publicKey);
File.WriteAllText("private_key.xml", privateKey);
```

### Programmatic Decryption

For large files, use the memory-optimized streaming API:

```csharp
using Serilog.Sinks.File.Encrypt;

// File-to-file decryption
string privateKeyXml = File.ReadAllText("private_key.xml");
await EncryptionUtils.DecryptLogFileToFileAsync(
    "logs/app.log", 
    "logs/decrypted.log", 
    privateKeyXml);

// Stream-to-stream decryption with custom options
var options = new StreamingOptions 
{
    BufferSize = 64 * 1024,  // 64KB chunks
    QueueDepth = 20,         // Queue depth
    ContinueOnError = true   // Continue on corruption
    ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog, // Log errors to a separate file
    ErrorLogPath = "decryption-errors.log" // Custom error log path
};

using var input = File.OpenRead("large-log.encrypted");
using var output = File.Create("large-log.decrypted");
await EncryptionUtils.DecryptLogFileAsync(input, output, privateKey, options);
```

### Error Handling Modes

Choose how to handle decryption errors:

```csharp
using Serilog.Sinks.File.Encrypt.Models;

// Skip corrupted sections silently (DEFAULT - ideal for JSON/structured logs)
var skipOptions = new StreamingOptions 
{
    ErrorHandlingMode = ErrorHandlingMode.Skip  // This is the default
};

// Write error messages inline to output (use only for human-readable logs)
var inlineOptions = new StreamingOptions 
{
    ErrorHandlingMode = ErrorHandlingMode.WriteInline
};

// Write errors to separate log file (for troubleshooting)
var errorLogOptions = new StreamingOptions 
{
    ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
    ErrorLogPath = "decryption-errors.log"
};

// Throw exception on first error (strict validation)
var strictOptions = new StreamingOptions 
{
    ErrorHandlingMode = ErrorHandlingMode.ThrowException,
    ContinueOnError = false
};

await EncryptionUtils.DecryptLogFileAsync(input, output, privateKey, skipOptions);
```

### Error Handling Use Cases

**Skip Mode** - For Structured Logging (JSON, Compact JSON):
```csharp
// Prevents corrupted error messages from breaking JSON parsing
var options = new StreamingOptions { ErrorHandlingMode = ErrorHandlingMode.Skip };
await EncryptionUtils.DecryptLogFileAsync(input, output, privateKey, options);
```

**WriteToErrorLog Mode** - For Production Troubleshooting:
```csharp
// Clean output + separate error tracking
var options = new StreamingOptions 
{ 
    ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
    ErrorLogPath = "errors.log"  // Optional, auto-generated if omitted
};
await EncryptionUtils.DecryptLogFileAsync(input, output, privateKey, options);
```

**ThrowException Mode** - For Data Integrity Validation:
```csharp
// Fail fast on any corruption
var options = new StreamingOptions 
{ 
    ErrorHandlingMode = ErrorHandlingMode.ThrowException,
    ContinueOnError = false
};

try 
{
    await EncryptionUtils.DecryptLogFileAsync(input, output, privateKey, options);
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
string publicKeyXml = builder.Configuration["Logging:PublicKeyXml"];

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.File(
            path: "logs/webapp-.log",
            rollingInterval: RollingInterval.Day,
            hooks: new EncryptHooks(publicKeyXml)));

var app = builder.Build();
app.Run();
```

## API Reference

### Key Management
```csharp
(string publicKey, string privateKey) EncryptionUtils.GenerateRsaKeyPair(int keySize = 2048)
```

### Decryption
```csharp
// File-to-file async decryption
Task EncryptionUtils.DecryptLogFileToFileAsync(string encryptedFilePath, string outputFilePath, string rsaPrivateKey, StreamingOptions? options = null, CancellationToken cancellationToken = default)

// Stream-to-stream async decryption  
Task EncryptionUtils.DecryptLogFileAsync(Stream inputStream, Stream outputStream, string rsaPrivateKey, StreamingOptions? options = null, CancellationToken cancellationToken = default)
```

### StreamingOptions
```csharp
public class StreamingOptions
{
    public int BufferSize { get; init; } = 16 * 1024;                   // 16KB default
    public int QueueDepth { get; init; } = 10;                          // Queue depth
    public bool ContinueOnError { get; init; } = true;                  // Error handling
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = Skip;   // Error mode (default: Skip)
    public string? ErrorLogPath { get; init; }                          // Error log file path
}
```

### ErrorHandlingMode
```csharp
public enum ErrorHandlingMode
{
    Skip = 0,              // Skip errors silently (DEFAULT - safe for all log formats)
    WriteInline = 1,       // Write error messages inline (use only for human-readable logs)
    WriteToErrorLog = 2,   // Write errors to separate log file
    ThrowException = 3     // Throw exception on first error
}
```

## Security Considerations

- Keep private keys secure and never include them in your application deployment
- Store private keys in secure key management systems in production
- Use 2048-bit RSA keys minimum (4096-bit for enhanced security)
- Restrict access to encrypted log files and private keys

## CLI Tool

The companion CLI tool provides key management and decryption with full error handling control:

```bash
# Generate keys
serilog-encrypt generate --output /path/to/keys

# Decrypt with default settings
serilog-encrypt decrypt --key private_key.xml --file log.txt --output decrypted.txt

# Decrypt with error handling options
serilog-encrypt decrypt -k key.xml -f log.txt -o out.txt -e Skip                    # Skip errors
serilog-encrypt decrypt -k key.xml -f log.txt -o out.txt -e WriteToErrorLog --error-log errors.log  # Log errors
serilog-encrypt decrypt -k key.xml -f log.txt -o out.txt -e ThrowException          # Fail on errors
```

For detailed CLI documentation, see the [CLI tool documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli).

## Requirements

- .NET 8.0 or higher
- A project using [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)
- RSA key pair for encryption/decryption in XML format (generated via CLI tool or programmatically)
