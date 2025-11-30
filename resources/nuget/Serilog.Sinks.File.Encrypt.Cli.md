# Serilog.Sinks.File.Encrypt CLI Tool

[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A command-line tool for managing RSA key pairs and decrypting log files created by the [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab) package.

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
# Decrypt a single file (output: app.decrypted.log in same directory)
serilog-encrypt decrypt app.log -k private_key.xml

# Decrypt a single file with custom output
serilog-encrypt decrypt app.log -k private_key.xml -o decrypted.log

# Decrypt all .log files in current directory
serilog-encrypt decrypt *.log -k private_key.xml

# Decrypt all files in a directory (default pattern: *.log)
serilog-encrypt decrypt ./logs -k private_key.xml

# Decrypt all files in a directory recursively
serilog-encrypt decrypt ./logs -k private_key.xml -r

# Decrypt with custom pattern
serilog-encrypt decrypt ./logs -k private_key.xml -p "app*.txt"

# Decrypt to a specific output directory
serilog-encrypt decrypt ./logs -k private_key.xml -o ./decrypted

# Overwrite existing decrypted files
serilog-encrypt decrypt app.log -k private_key.xml --overwrite
```

**Arguments:**
- `<PATH>`: Path to encrypted log file, directory, or glob pattern (e.g., *.log)

**Options:**
- `-k|--key <KEY>`: Path to the RSA private key file (default: `private_key.xml`)
- `-o|--output <OUTPUT>`: Output directory or file path (default: adds `.decrypted` to original filename)
- `-r|--recursive`: Process directories recursively
- `-p|--pattern <PATTERN>`: File pattern to match when processing directories (default: `*.log`)
- `--overwrite`: Overwrite existing decrypted files without prompting
- `-e|--error-mode <MODE>`: Error handling mode (default: `Skip`)
  - `Skip`: Silently skip corrupted sections (clean output)
  - `WriteInline`: Write error messages inline
  - `WriteToErrorLog`: Write errors to a separate log file
  - `ThrowException`: Stop immediately on first error
- `--error-log <PATH>`: Path for error log file (only used with `WriteToErrorLog` mode)
- `--continue-on-error`: Continue decryption even when errors are encountered (default: `true`)

**Features:**
- Memory-optimized for large log files
- Flexible error handling for corrupted data
- Fixed memory usage regardless of log file size
- Support for structured logging formats (JSON, etc.)
- Batch processing with glob patterns
- Directory traversal with recursive option

## Examples

### Basic Key Generation
```bash
# Generate keys in the current directory
serilog-encrypt generate --output .

# Generate keys in a specific directory
serilog-encrypt generate --output ./keys
```

### Single File Decryption
```bash
# Decrypt a single file (creates app.decrypted.log)
serilog-encrypt decrypt app.log -k ./keys/private_key.xml

# Decrypt with custom output name
serilog-encrypt decrypt app.log -k ./keys/private_key.xml -o readable.log

# Decrypt and overwrite existing output
serilog-encrypt decrypt app.log -k ./keys/private_key.xml --overwrite
```

### Batch Decryption
```bash
# Decrypt all .log files in current directory
serilog-encrypt decrypt *.log -k ./keys/private_key.xml

# Decrypt all files in a directory
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml

# Decrypt recursively through subdirectories
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml -r

# Decrypt with custom pattern (e.g., only app logs)
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml -p "app*.log"

# Decrypt to a different output directory
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml -o ./decrypted-logs
```

### Error Handling Scenarios

**For Structured Logging (JSON, Compact JSON):**
Use `Skip` mode to avoid corrupting the log format:
```bash
serilog-encrypt decrypt app.json.log -k key.xml -e Skip
```

**For Troubleshooting:**
Use `WriteToErrorLog` mode to track decryption issues:
```bash
serilog-encrypt decrypt app.log -k key.xml -e WriteToErrorLog --error-log issues.log
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

This tool works with log files encrypted by the Serilog.Sinks.File.Encrypt package. For detailed information on how to configure Serilog with encryption, see the [main package documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab).

## Requirements

- .NET 8.0 or higher
- Logs created with Serilog.Sinks.File.Encrypt
