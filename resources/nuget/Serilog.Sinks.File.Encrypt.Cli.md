# Serilog.Sinks.File.Encrypt CLI Tool

A command-line tool for managing RSA key pairs and decrypting log files created by the Serilog.Sinks.File.Encrypt package.

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

## Usage

The tool provides two main commands: `generate` for creating RSA key pairs and `decrypt` for decrypting encrypted log files.

### Generate RSA Key Pair

Generate a new RSA public/private key pair for encrypting log files:

```bash
serilog-encrypt generate --output /path/to/keys
```

**Options:**
- `-o|--output <OUTPUT>` (required): The directory where the key files will be saved

This command creates two files:
- `private_key.xml`: The private key used for decryption
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
# Decrypt using default file names
serilog-encrypt decrypt

# Decrypt with specific files
serilog-encrypt decrypt --key ./keys/private_key.xml --file ./logs/app.log --output ./logs/app-decrypted.log
```

## Security Notes

- Keep your private key secure and never share it
- The private key is required to decrypt log files
- Store keys separately from your application code
- Consider using secure key management systems in production environments

## Integration with Serilog

This tool works with log files encrypted by the Serilog.Sinks.File.Encrypt package. Configure your Serilog file sink to use encryption:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/app.log", hooks: new EncryptHooks("path/to/public_key.xml"))
    .CreateLogger();
```

## Requirements

- .NET 8.0 or higher
- RSA key pair (generated using this tool or compatible format)
