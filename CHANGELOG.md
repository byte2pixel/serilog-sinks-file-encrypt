# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [4.0.0] - 2026-04-05

### ⚠️ Breaking Changes

#### `DecryptionOptions.DecryptionKeys` replaced by `DecryptionOptions.KeyProvider`

`DecryptionOptions` no longer holds a `Dictionary<string, string>` of key ID → private key pairs.
Instead, you must supply an `IKeyProvider` implementation that performs the actual RSA decryption of
the AES-GCM session key. The built-in `LocalKeyProvider` provides the same dictionary-based
behaviour as before; implement `IKeyProvider` directly when integrating with an external key
management system such as Azure Key Vault or AWS KMS.

| v3                                                                                          | v4                                                              |
|---------------------------------------------------------------------------------------------|-----------------------------------------------------------------|
| `DecryptionOptions { DecryptionKeys = new Dictionary<string, string> { { "id", key } } }`  | `DecryptionOptions { KeyProvider = new LocalKeyProvider(map) }` |

```csharp
// v3
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string>
    {
        { "logs-key-2025", oldPrivateKey },
        { "logs-key-2026", newPrivateKey },
    }
};

// v4
var keyMap = new Dictionary<string, string>
{
    { "logs-key-2025", oldPrivateKey },
    { "logs-key-2026", newPrivateKey },
};
using LocalKeyProvider keyProvider = new LocalKeyProvider(keyMap);
var options = new DecryptionOptions
{
    KeyProvider = keyProvider,
};
```

> **v4.x can still decrypt v3.x log files.** No need to archive or re-encrypt existing files before upgrading.

The `CryptographicUtils.MagicBytes` has been made public to allow custom decryption implementations to identify compatible log files.

---

## [3.0.0] - 2026-03-22

### ⚠️ Breaking Changes

> **Log files written by v2.x are not compatible with v3.0.0.**
> Do not append v3.0.0 output to existing v2.x log files. Roll or archive your log files before upgrading.

#### Encryption header format

The per-session binary header written at the start of each encryption session has been redesigned.
The v3 header embeds a fixed-length key ID field (32 bytes) and an explicit version byte, enabling
the decryption layer to select the correct key automatically. Files written by v2 do not contain
these fields and cannot be parsed by the v3 reader.

#### Decryption API

| v2                                                                                            | v3                                                                                      |
|-----------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------|
| `EncryptionUtils.DecryptLogFileAsync(stream, output, string privateKey, StreamingOptions?)`   | `CryptographicUtils.DecryptLogFileAsync(stream, output, DecryptionOptions, ILogger?)`   |
| `EncryptionUtils.DecryptLogFileAsync(path, outputPath, string privateKey, StreamingOptions?)` | `CryptographicUtils.DecryptLogFileAsync(path, outputPath, DecryptionOptions, ILogger?)` |
| `StreamingOptions`                                                                            | `DecryptionOptions`                                                                     |

`StreamingOptions` has been replaced by `DecryptionOptions`. The new type carries a
`Dictionary<string, string> DecryptionKeys` (key ID → private key XML/PEM) instead of a single
private key string, enabling decryption of files encrypted under different keys in one pass.

#### `EncryptHooks` constructor

The constructor now accepts an optional `keyId` (string, max 32 bytes UTF-8) and `version` (int).

```csharp
// v2
new EncryptHooks(publicKey)

// v3
new EncryptHooks(publicKey)              // keyId defaults to "" — still valid
new EncryptHooks(publicKey, "key-2026") // recommended: supply a key ID
```

### New Features

#### Key rotation support

Assign a `keyId` when constructing `EncryptHooks`. The key ID is written into every session header.
When decrypting, supply all relevant private keys in `DecryptionOptions.DecryptionKeys`; the reader
picks the right one automatically.

```csharp
// Encryption – new key from 2026
new EncryptHooks(publicKey, "logs-key-2026")

// Decryption – supply both the old and new private keys
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string>
    {
        { "logs-key-2025", oldPrivateKey },
        { "logs-key-2026", newPrivateKey },
    }
};
```

#### CLI `--id` option

The `decrypt` command now accepts `--id <KEY_ID>` to specify which registered key ID corresponds
to the supplied private key file.

```bash
serilog-encrypt decrypt app.log -k private_key_2026.xml --id logs-key-2026
```

#### Audit log (`--audit-log`)

The new `--audit-log <PATH>` option writes a rolling diagnostic log (max 10 MB, 7 retained files)
capturing details of every decryption error encountered. When omitted, a randomly-named file is
created in the system temp directory. Pass an `ILogger?` to `CryptographicUtils.DecryptLogFileAsync`
for the same capability in programmatic use.

---

## [2.0.0] - 2025-12-02

Initial public release with hybrid RSA+AES-GCM encryption.

---

## Migration Guide: v2 → v3

### Step 1 – Archive existing logs

Before upgrading, make sure current log files are closed and archived (or decrypted) with the
**v2 CLI tool**. The v3 reader cannot process v2 log files.

### Step 2 – Update `EncryptHooks`

No changes are required to start working, but assigning a `keyId` is strongly recommended so that
future key rotations are handled cleanly:

```csharp
// Before
hooks: new EncryptHooks(publicKey)

// After (recommended)
hooks: new EncryptHooks(publicKey, "my-app-key-2026")
```

### Step 3 – Update decryption code

Replace `StreamingOptions` with `DecryptionOptions` and pass a key dictionary instead of a plain
private key string:

```csharp
// Before (v2)
await CryptographicUtils.DecryptLogFileAsync(inputStream, outputStream, privateKey);

// After (v3)
var options = new DecryptionOptions
{
    DecryptionKeys = new Dictionary<string, string> { { "my-app-key-2026", privateKey } },
    ErrorHandlingMode = ErrorHandlingMode.Skip
};
await CryptographicUtils.DecryptLogFileAsync(inputStream, outputStream, options);
```

### Step 4 – Update the CLI decrypt command

```bash
# Before (v2)
serilog-encrypt decrypt app.log -k private_key.xml

# After (v3) — pass the key ID that matches what was used during encryption.
# Note: directories are not accepted; use a glob pattern for batch decryption.
serilog-encrypt decrypt app.log -k private_key.xml --id my-app-key-2026

# Batch decryption with a glob pattern
serilog-encrypt decrypt "logs/*.log" -k private_key.xml --id my-app-key-2026

# Key ID defaults to "" when omitted, so the --id flag is optional
# if you did not assign a key ID (i.e. used the default).
serilog-encrypt decrypt app.log -k private_key.xml
```
