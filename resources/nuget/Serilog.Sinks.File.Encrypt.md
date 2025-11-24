# Serilog.Sinks.File.Encrypt

A Serilog sink that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## Features

- **Hybrid Encryption**: Uses RSA encryption for key exchange and AES for efficient data encryption
- **Seamless Integration**: Plugs directly into Serilog's file sink using lifecycle hooks
- **Individual Message Encryption**: Each log entry is encrypted separately with its own AES key and IV
- **CLI Tool Integration**: Companion CLI tool for key generation and log decryption
- **High Performance**: Optimized encryption with chunked processing

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

First, generate an RSA key pair using the CLI tool:

```bash
serilog-encrypt generate --output ./keys
```

This creates two files:
- `public_key.xml`: Used for encryption (safe to include with your application)
- `private_key.xml`: Used for decryption (keep secure, do not distribute)

### 2. Configure Serilog with Encryption

```csharp
using Serilog;
using Serilog.Sinks.File.Encrypt;

// Load your public key (this example reads from a file)
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

You can generate keys programmatically using the `EncryptionUtils` class:

```csharp
using Serilog.Sinks.File.Encrypt;

// Generate a new RSA key pair
var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair(2048);

// Save keys to files
File.WriteAllText("public_key.xml", publicKey);
File.WriteAllText("private_key.xml", privateKey);

// Use the public key for encryption
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/app.log", hooks: new EncryptHooks(publicKey))
    .CreateLogger();
```

### Programmatic Decryption

Decrypt log files programmatically:

```csharp
using Serilog.Sinks.File.Encrypt;

string privateKeyXml = File.ReadAllText("private_key.xml");
string decryptedContent = EncryptionUtils.DecryptLogFile("logs/app.log", privateKeyXml);
Console.WriteLine(decryptedContent);

// Or decrypt directly to a file
EncryptionUtils.DecryptLogFileToFile("logs/app.log", privateKeyXml, "logs/decrypted.log");
```

### Integration with Configuration

You can integrate encryption with Serilog configuration files:

```csharp
// Load public key from configuration, environment, or secure storage
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string publicKeyXml = configuration["Logging:PublicKey"];

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.File(
        path: "logs/app.log",
        hooks: new EncryptHooks(publicKeyXml))
    .CreateLogger();
```

## File Format

The encrypted log files use a custom format:

```
[HEADER_MARKER][key_length][iv_length][encrypted_aes_key][encrypted_aes_iv]
[CHUNK_MARKER][data_length][encrypted_log_data]
[CHUNK_MARKER][data_length][encrypted_log_data]
...
```

Each log chunk is encrypted with AES using a unique key and IV that are encrypted with RSA. This format allows for secure storage while maintaining the ability to decrypt individual log entries.

## Security Considerations

- **Key Management**: Keep private keys secure and never include them in your application deployment
- **Key Size**: Default RSA key size is 2048 bits. For enhanced security, use 4096 bits
- **Storage**: Store private keys in secure key management systems in production
- **Access Control**: Restrict access to encrypted log files and private keys
- **Rotation**: Consider implementing key rotation strategies for long-term deployments

## Performance

The encryption process is optimized for logging scenarios:
- Minimal overhead during log writing
- Chunked encryption for better performance
- Efficient memory usage with streaming
- [ ] Compatible with Serilog's async logging (TODO)

## Examples

### Basic Console Application

```csharp
using Serilog;
using Serilog.Sinks.File.Encrypt;

class Program
{
    static void Main()
    {
        // Generate keys (do this once, store securely)
        var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        
        // Configure encrypted logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/app-.log",
                rollingInterval: RollingInterval.Day,
                hooks: new EncryptHooks(publicKey))
            .CreateLogger();

        Log.Information("Application started");
        Log.Warning("This is a warning");
        Log.Error("This is an error");
        
        Log.CloseAndFlush();
        
        // Later, decrypt the logs
        string decrypted = EncryptionUtils.DecryptLogFile("logs/app-20231123.log", privateKey);
        Console.WriteLine("Decrypted content:");
        Console.WriteLine(decrypted);
    }
}
```

### Web Application

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
            retainedFileCountLimit: 30,
            hooks: new EncryptHooks(publicKeyXml)));

var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Home page accessed at {Timestamp}", DateTime.UtcNow);
    return "Hello World!";
});

app.Run();
```

## CLI Tool

The companion CLI tool (`Serilog.Sinks.File.Encrypt.Cli`) provides key management and decryption capabilities:

### Generate Keys
```bash
serilog-encrypt generate --output /path/to/keys
```

### Decrypt Logs
```bash
serilog-encrypt decrypt --key private_key.xml --file log.txt --output decrypted.txt
```

For detailed CLI documentation, see the [CLI tool documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli).

## Requirements

- .NET 8.0 or higher
- Serilog.Sinks.File package
- RSA key pair for encryption/decryption in XML format

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.
