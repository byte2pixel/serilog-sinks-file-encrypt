# Serilog.Sinks.File.Encrypt CLI Tool

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![codecov](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt/graph/badge.svg?component=encrypt-cli&token=HCDP3VVZ5B)](https://codecov.io/gh/byte2pixel/serilog-sinks-file-encrypt)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.File.Encrypt.Cli.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

A command-line tool for managing RSA key pairs and decrypting log files created by the [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab) package.

> [!WARNING]
> **v3.0.0 is a breaking change from v2.x.**
> The v3 CLI cannot decrypt log files written by v2. Decrypt existing v2 files with the v2 CLI **before** upgrading. See the [CHANGELOG](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/CHANGELOG.md) for the full migration guide.

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
- `-k|--key-size <KEY_SIZE>` (optional): The size of the RSA key in bits (default: 2048)
- `-f|--format <FORMAT>` (optional): The encoding format (Xml or Pem) for the RSA keys (default: Xml)

This creates two files:
- `private_key.xml`: The private key used for decryption (keep secure)
- `public_key.xml`: The public key used for encryption

### Decrypt Log Files

Decrypt encrypted log files using your RSA private key:

```bash
# Decrypt a single file (output: app.decrypted.log in same directory)
serilog-encrypt decrypt app.log -k private_key.xml

# Decrypt with a key ID (must match the keyId used during encryption)
serilog-encrypt decrypt app.log -k private_key.xml --id my-app-key-2026

# Decrypt a single file with custom output
serilog-encrypt decrypt app.log -k private_key.xml -o decrypted.log

# Decrypt all .log files using a glob pattern
serilog-encrypt decrypt "*.log" -k private_key.xml --id my-app-key-2026

# Decrypt all .log files under a directory using a glob pattern
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --id my-app-key-2026

# Decrypt to a specific output directory
serilog-encrypt decrypt "logs/*.log" -k private_key.xml -o ./decrypted
```

**Arguments:**
- `<PATH>`: Path to an encrypted log file, or a glob pattern (e.g., `*.log`, `logs/*.txt`). Directories are not accepted directly — append a pattern such as `logs/*.log`.

**Options:**
- `-k|--key <KEY>`: Path to the RSA private key file (default: `private_key.xml`)
- `--id <KEY_ID>`: The key ID that was supplied to `EncryptHooks` during encryption (default: `""` — matches files encrypted without a key ID)
- `-o|--output <OUTPUT>`: Output directory or file path (default: adds `.decrypted` to original filename)
- `-s|--strict`: Fail immediately on first decryption error (default: continues processing all files)
- `--audit-log <PATH>`: Write detailed audit information to a rolling log file (max 10 MB, 7 retained files). If omitted, a randomly-named file is created in the temp directory.

**Features:**
- Memory-optimized for large log files
- Simple error handling: continues on errors by default, or use `--strict` to fail fast
- Fixed memory usage regardless of log file size
- Support for structured logging formats (JSON, etc.)
- Batch processing with glob patterns
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

# Decrypt with key ID (recommended when keyId was set during encryption)
serilog-encrypt decrypt app.log -k ./keys/private_key.xml --id my-app-key-2026

# Decrypt with custom output name
serilog-encrypt decrypt app.log -k ./keys/private_key.xml -o readable.log

# Decrypt with strict error checking
serilog-encrypt decrypt app.log -k ./keys/private_key.xml --strict
```

### Batch Decryption
```bash
# Decrypt all .log files in the current directory
serilog-encrypt decrypt "*.log" -k ./keys/private_key.xml --id my-app-key-2026

# Decrypt all .log files under a subdirectory using a glob pattern
serilog-encrypt decrypt "logs/*.log" -k ./keys/private_key.xml --id my-app-key-2026

# Decrypt with a custom glob pattern (e.g., only specific file names)
serilog-encrypt decrypt "logs/app*.txt" -k ./keys/private_key.xml

# Decrypt to a different output directory
serilog-encrypt decrypt "logs/*.log" -k ./keys/private_key.xml -o ./decrypted-logs
```

### Key Rotation

When your application uses different keys for different time periods, decrypt each batch with the
corresponding key and `--id`:

```bash
# Files encrypted with the 2025 key
serilog-encrypt decrypt "logs/2025/*.log" -k ./keys/private_key_2025.xml --id my-app-key-2025

# Files encrypted with the 2026 key
serilog-encrypt decrypt "logs/2026/*.log" -k ./keys/private_key_2026.xml --id my-app-key-2026
```

> **Note:** The CLI supports one key per invocation. To decrypt a mixed directory containing files
> from multiple key rotations, use the programmatic `DecryptionOptions.DecryptionKeys` dictionary
> in your own application. See the [main package documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab).

### Error Handling

**Default Behavior (Recommended):**
By default, the tool continues processing all files even if some fail to decrypt:
```bash
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --id my-app-key-2026
```

**Strict Mode:**
Stop immediately on first error (useful for validation):
```bash
serilog-encrypt decrypt app.log -k private_key.xml --strict
```

**Audit Logging:**
Write detailed diagnostic information to a separate rolling log file:
```bash
# If not specified, a randomly-named audit log will be created in the temporary directory
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --audit-log decryption-audit.log
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
- Keep decrypted files alongside encrypted files in the same directory

**Example:**
```bash
# First run: decrypts app.log → app.decrypted.log
serilog-encrypt decrypt "logs/*.log" -k key.xml --id my-app-key-2026

# Later, after new logs are added
# Second run: only processes new encrypted files, skips app.decrypted.log
serilog-encrypt decrypt "logs/*.log" -k key.xml --id my-app-key-2026
```

## Integration with Serilog

This tool works with log files encrypted by the Serilog.Sinks.File.Encrypt package. For detailed information on how to configure Serilog with encryption, see the [main package documentation](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt#readme-body-tab).

## Requirements

- .NET 8.0 or higher
- Logs created with Serilog.Sinks.File.Encrypt v3.0.0 or later
