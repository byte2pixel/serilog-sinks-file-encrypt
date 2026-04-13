# Serilog.Sinks.File.Encrypt

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![CodeQL](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/codeql-analysis.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)

A [Serilog.File.Sink](https://github.com/serilog/serilog-sinks-file) hook that encrypts log files using RSA and AES encryption. This package provides secure logging by encrypting log data before writing to disk, ensuring sensitive information remains protected.

## 📦 Packages

- **[Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt)** - The main file hook that handles encrypting log entries.
- **[Serilog.Sinks.File.Encrypt.Cli](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)** - Command-line tool for key management and log decryption.

## ✨ Features

- Transparent encryption of log files using hybrid RSA + AES-GCM cryptography
- **Key rotation** — assign a key ID to each `EncryptHooks` instance; the decryption layer selects the correct key automatically
- CLI utilities for key generation, decryption, and batch processing of encrypted logs
- Memory-optimized streaming for large log files

## 📖 Documentation

Detailed installation, configuration, and usage instructions are provided in the package-specific README files:

- 📄 [Serilog.Sinks.File.Encrypt](resources/nuget/Serilog.Sinks.File.Encrypt.md)
- 📄 [Serilog.Sinks.File.Encrypt.Cli](resources/nuget/Serilog.Sinks.File.Encrypt.Cli.md)
- 📊 [Performance Benchmarks & Analysis](examples/Example.Benchmarks/README.md)
- 📋 [Changelog & Migration Guide](CHANGELOG.md)

Please refer to these files for up-to-date and comprehensive documentation for each package.

## 🤝 Build & Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to build, test, and contribute to the project.

## 📝 License

This project is licensed under the terms of the [MIT License](LICENSE.md).

## 🔐 Support & Security

For security issues, please see [SECURITY.md](SECURITY.md).

For questions or support, please open an issue on GitHub.
