# Serilog.Sinks.File.Encrypt CLI Tool

[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A command-line tool for managing RSA key pairs and decrypting log files created by the Serilog.Sinks.File.Encrypt package.

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

## Usage

### Generate RSA Key Pair

Generate a new RSA public/private key pair for encrypting log files:

```bash
serilog-encrypt generate --output /path/to/keys
```

**Options:**
- `-o|--output <OUTPUT>` (required): The directory where the key files will be saved

This creates two files:
- `private_key.xml`: The private key used for decryption (keep secure)
- `public_key.xml`: The public key used for encryption

### Decrypt Log Files

Decrypt encrypted log files using your RSA private key:

```bash
serilog-encrypt decrypt --key private_key.xml --file log.encrypted.txt --output log.decrypted.txt
```

**Options:**
- `-k|--key <KEY>`: Path to the RSA private key file (default: `privateKey.xml`)
- `-f|--file <FILE>`: Path to the encrypted log file (default: `log.encrypted.txt`)
- `-o|--output <OUTPUT>`: Path for the decrypted output file (default: `log.decrypted.txt`)
- `-e|--error-mode <MODE>`: Error handling mode (default: `WriteInline`)
  - `Skip`: Silently skip corrupted sections
  - `WriteInline`: Write error messages inline (default)
  - `WriteToErrorLog`: Write errors to a separate log file
  - `ThrowException`: Stop immediately on first error
- `--error-log <PATH>`: Path for error log file (only used with `WriteToErrorLog` mode)
- `--continue-on-error`: Continue decryption even when errors are encountered (default: `true`)

**Features:**
- Memory-optimized for large log files
- Flexible error handling for corrupted data
- Fixed memory usage regardless of log file size
- Support for structured logging formats (JSON, etc.)

## Examples

### Basic Key Generation
```bash
# Generate keys in the current directory
serilog-encrypt generate --output .

# Generate keys in a specific directory
serilog-encrypt generate --output ./keys
```

### Basic Log Decryption
```bash
# Decrypt with default settings (errors skipped silently)
serilog-encrypt decrypt --key ./keys/private_key.xml --file ./logs/app.log --output ./logs/app-decrypted.log

# Write errors inline (for human-readable logs only)
serilog-encrypt decrypt --key ./keys/private_key.xml --file ./logs/app.log --output ./logs/app-decrypted.log --error-mode WriteInline

# Write errors to a separate log file
serilog-encrypt decrypt --key ./keys/private_key.xml --file ./logs/app.log --output ./logs/app-decrypted.log --error-mode WriteToErrorLog --error-log ./logs/errors.log

# Stop on first error (strict validation)
serilog-encrypt decrypt --key ./keys/private_key.xml --file ./logs/app.log --output ./logs/app-decrypted.log --error-mode ThrowException --continue-on-error false
```

### Error Handling Scenarios

**For Structured Logging (JSON, Compact JSON):**
Use `Skip` mode to avoid corrupting the log format:
```bash
serilog-encrypt decrypt -k key.xml -f app.json.log -o decrypted.json.log -e Skip
```

**For Troubleshooting:**
Use `WriteToErrorLog` mode to track decryption issues:
```bash
serilog-encrypt decrypt -k key.xml -f app.log -o decrypted.log -e WriteToErrorLog --error-log issues.log
```

**For Data Integrity Validation:**
Use `ThrowException` mode to ensure no data loss:
```bash
serilog-encrypt decrypt -k key.xml -f app.log -o decrypted.log -e ThrowException --continue-on-error false
```

## Security Notes

- Keep your private key secure and never share it
- The private key is required to decrypt log files
- Store keys separately from your application code
- Consider using secure key management systems in production

## Integration with Serilog

This tool works with log files encrypted by the Serilog.Sinks.File.Encrypt package. For detailed information on how to configure Serilog with encryption, see the [main package documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt).

## Requirements

- .NET 8.0 or higher
- Logs created with Serilog.Sinks.File.Encrypt
