# Security Policy

## Supported Versions

We release security updates for the following versions:

| Version | Supported          |
|---------|--------------------|
| 0.x.x   | :white_check_mark: |

As this project is in pre-release (0.x.x versions), we provide security updates for the latest pre-release version. Once version 1.0.0 is released, we will provide security updates for the current major version and the previous major version for a minimum of 6 months after the next major release.

## Reporting a Vulnerability

We take the security of Serilog.Sinks.File.Encrypt seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### How to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via GitHub Security Advisories:

1. Navigate to the [Security tab](https://github.com/byte2pixel/serilog-sinks-file-encrypt/security/advisories) of this repository
2. Click "Report a vulnerability"
3. Fill out the advisory form with as much detail as possible

### What to Include

Please include the following information in your report:

- Type of issue (e.g., buffer overflow, encryption weakness, key exposure)
- Full paths of source file(s) related to the issue
- Location of the affected source code (tag/branch/commit or direct URL)
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: We will acknowledge receipt of your vulnerability report within 10 business days
- **Assessment**: We will assess the vulnerability and determine its severity within 10 business days
- **Fix Development**: For confirmed vulnerabilities, we will work on a fix and aim to release it within:
  - Critical vulnerabilities: 14 days
  - High severity: 30 days
  - Medium/Low severity: 60 days

## Security Best Practices

When using Serilog.Sinks.File.Encrypt in production, follow these security best practices:

### Key Management

#### Key Storage

**Never store private keys in your application code or configuration files that are committed to version control.**

Recommended key storage solutions:

- **Azure Key Vault**: Store keys in Azure Key Vault and retrieve them at runtime
  ```csharp
  var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
  KeyVaultSecret secret = await client.GetSecretAsync("PublicEncryptionKey");
  string publicKey = secret.Value;
  ```

- **AWS Secrets Manager**: Use AWS Secrets Manager for key storage
  ```csharp
  var client = new AmazonSecretsManagerClient();
  var request = new GetSecretValueRequest { SecretId = "PublicEncryptionKey" };
  var response = await client.GetSecretValueAsync(request);
  string publicKey = response.SecretString;
  ```

- **HashiCorp Vault**: Enterprise-grade secret management
- **Environment Variables**: For development/testing only (not recommended for production)
- **Encrypted Configuration Files**: Use ASP.NET Core Data Protection or similar

#### Key Generation

- **Use 2048-bit RSA keys minimum**: Default is 2048-bit, but consider 4096-bit for enhanced security
  ```bash
  serilog-encrypt generate --output ./keys --key-size 4096
  ```

- **Generate unique keys per environment**: Don't reuse the same keys across dev/staging/production
- **Store private keys separately**: Only deploy public keys with your application

#### Key Rotation

Key rotation is essential for long-term security:

1. **Generate new key pair** without replacing the old one
2. **Deploy application with new public key** for writing new logs
3. **Keep old private key available** for reading historical encrypted logs
4. **Archive old logs** after retention period expires
5. **Safely destroy old private keys** when logs are no longer needed

### Access Control

- **Restrict file system permissions**: Ensure only authorized users/processes can read encrypted log files
- **Secure the private key**: Private keys should have the most restrictive permissions possible
- **Audit access**: Monitor and log access to encrypted log files and private keys
- **Principle of least privilege**: Only grant decryption capabilities to systems/users that absolutely need them

### Encryption Scope

#### What This Library Protects

✅ **Protects log data at rest**: Encrypted files on disk are unreadable without the private key
✅ **Protects against unauthorized file access**: Even with file system access, logs can't be read
✅ **Supports compliance requirements**: Helps meet data protection regulations (GDPR, HIPAA, etc.)

#### What This Library Does NOT Protect Against

- ❌ **In-memory data**: Log data is unencrypted in application memory before encryption
- ❌ **Transport encryption**: Logs are encrypted on disk, not during network transmission (use TLS for that)
- ❌ **Key compromise**: If an attacker obtains the private key, all logs can be decrypted
- ❌ **Side-channel attacks**: Physical access to the machine may enable advanced attacks
- ❌ **Malicious code**: Malware running with application privileges can capture logs before encryption

### Threat Model

**Attack Vectors Mitigated:**
- Unauthorized access to backup drives
- Stolen or disposed hard drives
- Log file exposure through misconfigured file shares
- Compliance audits requiring encryption at rest

**Out of Scope:**
- Protection against privileged users on the host system
- Defense against memory dumping or process inspection
- Protection if the application itself is compromised

## Dependency Security

This library uses the .NET cryptographic libraries (`System.Security.Cryptography`) and relies on:

- **RSA-OAEP-SHA256**: For encrypting AES keys and IVs
- **AES-256-CBC**: For log content encryption with PKCS7 padding

We monitor dependencies through:
- GitHub Dependabot (automated PRs for dependency updates)
- CodeQL analysis (static security scanning)
- Regular security audits of direct dependencies

## Security Updates

When security updates are released:

1. We publish a GitHub Security Advisory
2. We release a new package version to NuGet
3. We update this SECURITY.md file with affected versions
4. We notify users through GitHub release notes

Subscribe to repository releases to stay informed about security updates.

## Additional Resources

- [OWASP Cryptographic Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
- [.NET Cryptography Model](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptography-model)
- [Key Management Best Practices](https://csrc.nist.gov/Projects/Key-Management/Key-Management-Guidelines)

---

Thank you for helping keep Serilog.Sinks.File.Encrypt and its users safe!
