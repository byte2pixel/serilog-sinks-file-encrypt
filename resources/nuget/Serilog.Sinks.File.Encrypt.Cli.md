# Serilog.Sinks.File.Encrypt CLI Tool

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?component=encrypt-cli&token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A command-line tool for managing RSA key pairs and decrypting log files created by the [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab) package.

> [!Note]
> :construction: **Newly Released** :construction:
> This Cli is newly released. Commands and options may change in future versions. Please report any issues you encounter or suggestions for improvement.

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

# Decrypt all .log files in current directory using a glob pattern
serilog-encrypt decrypt *.log -k private_key.xml

# Decrypt with custom glob pattern (e.g., only app logs)
serilog-encrypt decrypt "app*.log" -k private_key.xml

# Decrypt all .log files in a directory
serilog-encrypt decrypt ./logs -k private_key.xml

# Decrypt all .log files in a directory recursively
serilog-encrypt decrypt ./logs -k private_key.xml -r

# Decrypt to a specific output directory
serilog-encrypt decrypt ./logs -k private_key.xml -o ./decrypted
```

**Arguments:**
- `<PATH>`: Path to encrypted log file, directory (uses `*.log` pattern), or glob pattern (e.g., `*.log`, `logs/*.txt`)

**Options:**
- `-k|--key <KEY>`: Path to the RSA private key file (default: `private_key.xml`)
- `-o|--output <OUTPUT>`: Output directory or file path (default: adds `.decrypted` to original filename)
- `-r|--recursive`: Process directories recursively
- `-s|--strict`: Fail immediately on first decryption error (default: continues processing all files)
- `--error-log <PATH>`: Write detailed error information to a separate log file

**Features:**
- Memory-optimized for large log files
- Simple error handling: continues on errors by default, or use `--strict` to fail fast
- Fixed memory usage regardless of log file size
- Support for structured logging formats (JSON, etc.)
- Batch processing with glob patterns
- Directory traversal with recursive option
- Automatically skips files with `.decrypted.` in the name to prevent re-decryption

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

# Decrypt with strict error checking
serilog-encrypt decrypt app.log -k ./keys/private_key.xml --strict
```

### Batch Decryption
```bash
# Decrypt all .log files in current directory
serilog-encrypt decrypt *.log -k ./keys/private_key.xml

# Decrypt all .log files in a directory (uses *.log pattern automatically)
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml

# Decrypt recursively through subdirectories
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml -r

# Decrypt with custom glob pattern
serilog-encrypt decrypt "logs/app*.txt" -k ./keys/private_key.xml

# Decrypt to a different output directory
serilog-encrypt decrypt ./logs -k ./keys/private_key.xml -o ./decrypted-logs
```

### Error Handling

**Default Behavior (Recommended):**
By default, the tool continues processing all files even if some fail to decrypt:
```bash
serilog-encrypt decrypt ./logs -k private_key.xml
```

**Strict Mode:**
Stop immediately on first error (useful for validation):
```bash
serilog-encrypt decrypt app.log -k private_key.xml --strict
```

**Error Logging:**
Log detailed error information to a separate file while continuing to process files:
```bash
serilog-encrypt decrypt ./logs -k private_key.xml --error-log decryption-errors.log
```

## Security Notes

- Keep your private key secure and never share it
- The private key is required to decrypt log files
- Store keys separately from your application code
- Consider using secure key management systems in production

## Usage Notes

### Re-decryption Safety
The tool automatically skips files with `.decrypted.` in the filename to prevent accidental re-decryption. This means you can safely:
- Run decrypt multiple times on the same directory as new encrypted logs are added
- Use the `-r` (recursive) option without worrying about processing already-decrypted files
- Keep decrypted files alongside encrypted files in the same directory

**Example:**
```bash
# First run: decrypts app.log → app.decrypted.log
serilog-encrypt decrypt ./logs -k key.xml

# Later, after new logs are added
# Second run: only processes new encrypted files, skips app.decrypted.log
serilog-encrypt decrypt ./logs -k key.xml
```

## Integration with Serilog

This tool works with log files encrypted by the Serilog.Sinks.File.Encrypt package. For detailed information on how to configure Serilog with encryption, see the [main package documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab).

## Requirements

- .NET 8.0 or higher
- Logs created with Serilog.Sinks.File.Encrypt
