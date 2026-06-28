# Serilog.Sinks.File.Encrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![CodeQL](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)

A [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) hook that encrypts log files using RSA and AES-GCM hybrid encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## 📦 Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| **[Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)** | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt) | File sink hook — encrypts log entries as they are written |
| **[Serilog.Sinks.File.Decrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt)** | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Decrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt) | Library for programmatic decryption of encrypted log files |
| **[Serilog.Sinks.File.Encrypt.Cli](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)** | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli) | CLI tool for key generation and ad-hoc log decryption |
| **[Serilog.Sinks.File.Encrypt.Core](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core)** | [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Core.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core) | Shared cryptographic primitives — transitive dependency, no direct reference needed |

## ✨ Features

- Transparent encryption of log files using hybrid RSA + AES-GCM cryptography
- **Key rotation** — assign a key ID to each `EncryptHooks` instance; the decryption layer selects the correct key automatically
- CLI utilities for key generation, decryption, and batch processing of encrypted logs
- Memory-optimized streaming for large log files
- Programmatic decryption via `Serilog.Sinks.File.Decrypt` — supports custom key providers for Azure Key Vault, AWS KMS, etc.

## 📖 Documentation

Detailed installation, configuration, and usage instructions are provided in the package-specific README files:

- 📄 [Serilog.Sinks.File.Encrypt](resources/nuget/Serilog.Sinks.File.Encrypt.md) — encrypting log files with Serilog
- 📄 [Serilog.Sinks.File.Decrypt](resources/nuget/Serilog.Sinks.File.Decrypt.md) — programmatic decryption API
- 📄 [Serilog.Sinks.File.Encrypt.Cli](resources/nuget/Serilog.Sinks.File.Encrypt.Cli.md) — CLI key generation and decryption tool
- 📄 [Serilog.Sinks.File.Encrypt.Core](resources/nuget/Serilog.Sinks.File.Encrypt.Core.md) — shared primitives (transitive dependency)
- 📊 [Performance Benchmarks & Analysis](examples/Example.Benchmarks/README.md)
- 📋 [Changelog & Migration Guide](CHANGELOG.md)

Please refer to these files for up-to-date and comprehensive documentation for each package.

## 🎯 .NET Support Policy

This library targets .NET **Long-Term Support (LTS)** releases only. Current targets: `net8.0` and `net10.0`.

- A new LTS TFM is added when Microsoft ships it (approximately every 2 years).
- The oldest LTS TFM is dropped when Microsoft ends support for it.
- Users on STS or end-of-life runtimes can pin an older package version — .NET's runtime forward-compatibility means a `net8.0` or `net10.0` package will run on any higher runtime version.

## 🤝 Build & Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to build, test, and contribute to the project.

## 📝 License

This project is licensed under the terms of the [MIT License](LICENSE.md).

## 🔐 Support & Security

For security issues, please see [SECURITY.md](SECURITY.md).

For questions or support, please open an issue on GitHub.