# Example .NET Console Application

This example demonstrates encrypted logging in a .NET Console application using the Serilog.Sinks.File.Encrypt package. Follow this walkthrough to see encryption and decryption in action.

## Prerequisites

- .NET 8.0 SDK or higher
- Basic familiarity with .NET Console applications and logging

## Setup and Walkthrough

### 1. Install the CLI Tool

First, install the CLI tool globally to manage keys and decrypt logs:

```bash
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
```

### 2. Generate RSA Key Pair

Generate fresh encryption keys for this example:

```bash
# Navigate to the example directory
cd examples/Example.Console

# Generate new RSA keys
serilog-encrypt generate --output .
```

This creates two files:
- `public_key.xml` - Used by the application for encryption
- `private_key.xml` - Used for decryption (keep secure!)

### 3. Build and Run the Example

Build and run the console application:

```bash
dotnet run
```

The application will:
- Create encrypted log files in the `bin/Debug/<framework>/Logs` directory
- Show the path where encrypted logs are stored

### 4. Run Multiple Times

Run the application several times to generate more log data:

```bash
dotnet run
dotnet run
dotnet run
```

Each run will append to the daily log file with encrypted content.

### 5. Examine the Encrypted Log Files

Navigate to the logs directory and examine the files:

```bash
# Go to the logs directory
cd bin/Debug/<framework>/Logs

# List the log files
ls

# Try to view the encrypted content (you'll see binary data)
cat log20251123.txt
```

The log files contain encrypted binary data that cannot be read without decryption.

### 6. Decrypt the Log Files

Use the CLI tool to decrypt and view the log contents:

```bash
# Decrypt a specific log file (creates log20251123.decrypted.txt)
serilog-encrypt decrypt log20251123.txt -k ../../../private_key.xml

# Or decrypt with custom output name
serilog-encrypt decrypt log20251123.txt -k ../../../private_key.xml -o decrypted-log.txt

# View the decrypted content
cat log20251123.decrypted.txt
```

You should now see the original log messages in plain text format.

## What This Example Demonstrates

- **Transparent Encryption**: The application logs normally while encryption happens automatically
- **Key Management**: Using the CLI tool to generate and manage RSA key pairs
- **File Security**: Log files are unreadable without the private key
- **Decryption Workflow**: How to decrypt logs for analysis or troubleshooting

## Security Notes

- Generate unique keys for each environment
- Store private keys securely (Azure Key Vault, AWS Secrets Manager, etc.)
- Never commit private keys to source control
- Implement proper key rotation strategies

## Application Code

The example uses a `KeyService` class to load the public key and configures Serilog with `EncryptHooks`:

```csharp
Logger logger = new LoggerConfiguration()
    .WriteTo.File(
        path: Path.Join(logDirectory, "log.txt"),
        rollingInterval: RollingInterval.Day,
        hooks: new EncryptHooks(keyService.PublicKey)
    )
    .CreateLogger();
```

This setup ensures all log messages are encrypted before being written to disk.
