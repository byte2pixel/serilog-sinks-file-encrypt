# Serilog.Sinks.File.Encrypt

A Serilog sink that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## Features

- **Hybrid Encryption**: Uses RSA encryption for key exchange and AES for efficient data encryption
- **Seamless Integration**: Plugs directly into Serilog's file sink using lifecycle hooks
- **Memory-Optimized**: Producer-consumer architecture for efficient processing of large files
- **CLI Tool Integration**: Companion CLI tool for key generation and log decryption
- **High Performance**: Optimized encryption with chunked processing

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
var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair(2048);

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
};

using var input = File.OpenRead("large-log.encrypted");
using var output = File.Create("large-log.decrypted");
await EncryptionUtils.DecryptLogFileAsync(input, output, privateKeyXml, options);
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
    public int BufferSize { get; init; } = 16 * 1024;     // 16KB default
    public int QueueDepth { get; init; } = 10;            // Queue depth
    public bool ContinueOnError { get; init; } = true;    // Error handling
}
```

## Security Considerations

- Keep private keys secure and never include them in your application deployment
- Store private keys in secure key management systems in production
- Use 2048-bit RSA keys minimum (4096-bit for enhanced security)
- Restrict access to encrypted log files and private keys

## CLI Tool

The companion CLI tool provides key management and decryption:

```bash
# Generate keys
serilog-encrypt generate --output /path/to/keys

# Decrypt logs
serilog-encrypt decrypt --key private_key.xml --file log.txt --output decrypted.txt
```

For detailed CLI documentation, see the [CLI tool documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli).

## Requirements

- .NET 8.0 or higher
- Serilog.Sinks.File package
