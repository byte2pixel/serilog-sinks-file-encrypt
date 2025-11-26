# Serilog.Sinks.File.Encrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![CodeQL](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

A Serilog sink that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

> **⚠️ Pre-release Software**: This library is currently in pre-release (0.x.x versions). APIs may change before the 1.0 release. See [SECURITY.md](SECURITY.md) for security considerations and best practices.

## 📦 Packages

This repository contains two packages:

- **[Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)** - The main encryption sink for Serilog
- **[Serilog.Sinks.File.Encrypt.Cli](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)** - Command-line tool for key management and log decryption

## ✨ Features

- **Hybrid Encryption**: Uses RSA encryption for key exchange and AES for efficient data encryption
- **Seamless Integration**: Plugs directly into Serilog's file sink using lifecycle hooks
- **Individual Open Encryption**: Each time the log file is opened, a new AES key and IV are generated and encrypted with RSA
- **CLI Tool Integration**: Companion CLI tool for key generation and log decryption
- **High Performance**: Optimized encryption with chunked processing

## 🚀 Quick Start

### 🔧 Requirements

- .NET 8.0 or higher
- Serilog.Sinks.File package
- RSA key pair for encryption/decryption in XML format

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

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:

- Setting up your development environment
- Code style and conventions
- Submitting pull requests
- Running tests

Please also read our [Code of Conduct](CODE_OF_CONDUCT.md) before contributing.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

## 🔒 Security

For security concerns and vulnerability reporting, please see our [Security Policy](SECURITY.md).

## 📋 Changelog

See [CHANGELOG.md](CHANGELOG.md) for a list of changes in each release.

