# Serilog.Sinks.File.Encrypt

A Serilog sink that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## 📦 Packages

This repository contains two packages:

- **[Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)** - The main encryption sink for Serilog
- **[Serilog.Sinks.File.Encrypt.Cli](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)** - Command-line tool for key management and log decryption

## ✨ Features

- **Hybrid Encryption**: Uses RSA encryption for key exchange and AES for efficient data encryption
- **Seamless Integration**: Plugs directly into Serilog's file sink using lifecycle hooks
- **Individual Message Encryption**: Each log entry is encrypted separately with its own AES key and IV
- **CLI Tool Integration**: Companion CLI tool for key generation and log decryption
- **High Performance**: Optimized encryption with chunked processing

## 🚀 Quick Start

### 1. Install the packages

```bash
# Install the main package
dotnet add package Serilog.Sinks.File.Encrypt

# Install the CLI tool globally
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

### 2. Generate RSA Key Pair

```bash
serilog-encrypt generate --output ./keys
```

### 3. Configure Serilog with Encryption

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

### 4. Decrypt Logs

```bash
serilog-encrypt decrypt --key ./keys/private_key.xml --file logs/app.log --output logs/app-decrypted.log
```

## 📚 Documentation

- **[Main Package Documentation](./resources/nuget/Serilog.Sinks.File.Encrypt.md)** - Comprehensive guide for the encryption sink
- **[CLI Tool Documentation](./resources/nuget/Serilog.Sinks.File.Encrypt.Cli.md)** - Guide for the command-line tool

## 🛡️ Security

- **Key Management**: Keep private keys secure and never include them in your application deployment
- **Key Size**: Default RSA key size is 2048 bits. For enhanced security, use 4096 bits
- **Storage**: Store private keys in secure key management systems in production
- **Access Control**: Restrict access to encrypted log files and private keys

## 🏗️ Building

This project uses .NET 8.0 and Cake for building:

```bash
# Restore tools and build
dotnet tool restore
dotnet make
```

## 📁 Repository Structure

```
├── src/
│   ├── Serilog.Sinks.File.Encrypt/          # Main encryption sink
│   └── Serilog.Sinks.File.Encrypt.Cli/      # CLI tool
├── test/
│   └── Serilog.Sinks.File.Encrypt.Tests/    # Unit tests
├── examples/
│   ├── Example.Console/                     # Console application example
│   └── Example.Benchmarks/                  # Performance benchmarks
└── resources/
    └── nuget/                               # NuGet package documentation
```

## 🔧 Requirements

- .NET 8.0 or higher
- Serilog.Sinks.File package
- RSA key pair for encryption/decryption in XML format

## 📄 License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📊 Performance

Encrypting log files introduces some overhead due to the encryption process. However, the use of hybrid encryption (RSA + AES) ensures that the performance impact is minimized. AES is used for encrypting the actual log messages.
For performance benchmarks, refer to the [Example.Benchmarks](./examples/Example.Benchmarks) project in the repository.
