# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [6.0.0] - Unreleased

### ⚠️ Breaking Changes

#### New v2 on-disk format ([#83](https://github.com/byte2pixel/serilog-sinks-file-encrypt/issues/83))

The writer now emits **format version 2**, which closes the truncation/reordering/splicing
integrity gap documented in [#82](https://github.com/byte2pixel/serilog-sinks-file-encrypt/issues/82):

- Every AES-GCM record binds 41 bytes of **associated data**: the SHA-256 hash of the session
  header (covering version, keyId, and the RSA-wrapped key), the frame's sequence number
  (big-endian, starting at 0), and a record-type byte. Dropped, reordered, duplicated, or
  cross-session-spliced frames now fail authentication.
- On clean close (`Dispose` / `Log.CloseAndFlush()`) the writer appends a 28-byte authenticated
  **end-of-log seal** (marker `FF 42 32 53` + encrypted final frame count). The seal uses a
  reserved nonce (initial session nonce counter − 1) so it verifies regardless of how many
  trailing frames survive, which lets the reader distinguish *truncation of a cleanly closed
  log* (`SealCountMismatch`) from generic corruption.
- The header layout is byte-identical to v1 except the version byte (`0x02`).

**Compatibility: v6.x reads v1-format files produced by v3.x–v5.x. v5.x and earlier cannot
read v6 (v2-format) files.**

#### `version` parameter removed

The ignored `version` parameter is gone from `EncryptHooks` and `EncryptionOptions`:

```csharp
// v5
new EncryptHooks(publicKey, keyId: "my-key", version: 1);
new EncryptionOptions(rsa, KeyId: "my-key", Version: 1);

// v6 — drop the argument
new EncryptHooks(publicKey, keyId: "my-key");
new EncryptionOptions(rsa, KeyId: "my-key");
```

#### Decryption result and context shape

- `DecryptionResult` is now `sealed` and gains `Sessions` (`IReadOnlyList<SessionResult>`),
  `UnsealedSessions`, and `AllSessionsSealed`. The existing flat counters are unchanged.
- `DecryptionContext` gains `Version`, `HeaderHash`, `SealNonce`, and `KeyId`; its constructor
  signature changed.
- Internal writer/reader interfaces (`IFrameWriter`, `ISessionWriter`, `ISessionReader`) changed
  signatures.

#### CLI exit-code contract ([#96](https://github.com/byte2pixel/serilog-sinks-file-encrypt/issues/96))

The `serilog-encrypt` CLI now returns distinct exit codes so scripts can react without parsing
output: `0` success, `1` runtime failure, `2` usage error (parse/validation failures previously
surfaced as `-1`), `3` no input files matched (previously `0`), `4` nothing decrypted (previously
`0` — see below). When several apply across a multi-file run, the highest-priority code wins
(`1` > `4` > `3`).

#### CLI: zero-output decryption is no longer silent success ([#84](https://github.com/byte2pixel/serilog-sinks-file-encrypt/issues/84))

In the default (non-strict) mode, a file that decrypts to zero sessions and zero messages —
typically a wrong key, a wrong `--id`, or a file that is not an encrypted log — previously
produced an empty `.decrypted` output, a green success message, and exit code `0`. The CLI now
warns, removes the empty output file, and exits `4`. Library callers get the same signal via the
new `DecryptionResult.NothingDecrypted` property (additive, non-breaking).

### New Features

- **CLI `--quiet` / `--verbose`** — both commands accept `-q|--quiet` (suppress informational
  output; warnings and errors still shown) and `-v|--verbose` (adds per-file
  session/message/resync diagnostic detail).
- **Per-session seal verification** — `DecryptionResult.Sessions` reports each session's
  `SealStatus`: `Sealed`, `Unsealed` (crash *or* truncation — indistinguishable by design),
  `SealCountMismatch` (tail truncation of a sealed log), `SealInvalid` (tampering), or
  `NotApplicable` (v1 session).
- **`DecryptionOptions.RequireSealed`** — with `ErrorHandlingMode.ThrowException`, any session
  that is not cryptographically verified as sealed (including v1 sessions) throws. A positively
  detected `SealCountMismatch` always throws in `ThrowException` mode, even without the flag.
- **CLI `--require-sealed`** — the `decrypt` command reports per-session seal status and, combined
  with `--strict`, fails on unsealed/legacy sessions.

### Notes

- Crash tolerance is preserved: an unsealed log still decrypts fully by default and is *reported*
  as unsealed rather than failing. Buffered mode retains its documented data-loss caveat.
- Fabrication of whole *sessions* by an attacker holding the public key remains out of scope
  (requires a producer-side secret); see the threat model in the package README.
- Write-path performance is unchanged in structure: the AAD adds ~41 hashed bytes per frame with
  zero additional allocations, plus one 28-byte seal per session at close.

## [5.0.0] - 2026-06-29

### ⚠️ Breaking Changes

#### Package split — decryption types moved to `Serilog.Sinks.File.Decrypt`

The single `Serilog.Sinks.File.Encrypt` package has been split into three packages:

| Package | Purpose |
|---------|---------|
| `Serilog.Sinks.File.Encrypt` | File sink hook — encrypts log entries as they are written (unchanged API) |
| `Serilog.Sinks.File.Decrypt` | **New** — programmatic decryption library |
| `Serilog.Sinks.File.Encrypt.Core` | **New** — shared cryptographic primitives; transitive dependency, no direct reference needed |

All decryption types have moved from `Serilog.Sinks.File.Encrypt` to `Serilog.Sinks.File.Decrypt`:

| v4 namespace | v5 namespace |
|---|---|
| `Serilog.Sinks.File.Encrypt.LogReader` | `Serilog.Sinks.File.Decrypt.LogReader` |
| `Serilog.Sinks.File.Encrypt.LocalKeyProvider` | `Serilog.Sinks.File.Decrypt.LocalKeyProvider` |
| `Serilog.Sinks.File.Encrypt.Interfaces.IKeyProvider` | `Serilog.Sinks.File.Decrypt.Interfaces.IKeyProvider` |
| `Serilog.Sinks.File.Encrypt.Models.DecryptionOptions` | `Serilog.Sinks.File.Decrypt.Models.DecryptionOptions` |
| `Serilog.Sinks.File.Encrypt.Models.DecryptionResult` | `Serilog.Sinks.File.Decrypt.Models.DecryptionResult` |
| `Serilog.Sinks.File.Encrypt.Models.ErrorHandlingMode` | `Serilog.Sinks.File.Decrypt.Models.ErrorHandlingMode` |

#### `CryptographicUtils.DecryptLogFileAsync` replaced by `DecryptionUtils.DecryptLogFileAsync`

`CryptographicUtils` no longer contains decryption methods. Use `DecryptionUtils` from the `Serilog.Sinks.File.Decrypt` package instead. The method signatures are identical.

```csharp
// v4
using Serilog.Sinks.File.Encrypt;
using Serilog.Sinks.File.Encrypt.Models;

await CryptographicUtils.DecryptLogFileAsync(input, output, options);

// v5
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

await DecryptionUtils.DecryptLogFileAsync(input, output, options);
```

`CryptographicUtils.GenerateRsaKeyPair` and `KeyFormat` remain accessible from `Serilog.Sinks.File.Encrypt` via the transitive `Serilog.Sinks.File.Encrypt.Core` dependency.

#### `HeaderMetadata` made internal

`HeaderMetadataV1` (accidentally public in v4) has been renamed to `HeaderMetadata` and made `internal`. This type was never intended to be part of the public API.

> **v5.x can still decrypt v4.x and v3.x log files.** No file format changes; this is a code-only restructuring.

### New Features

#### `Serilog.Sinks.File.Decrypt` package

The new decryption package exposes a clean, focused API for reading encrypted log files. Install it independently when you only need decryption capabilities (e.g. a separate log-processing service):

```bash
dotnet add package Serilog.Sinks.File.Decrypt
```

```csharp
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

var keyMap = new Dictionary<string, string>
{
    { "my-app-key-2026", File.ReadAllText("private_key.xml") },
};
using LocalKeyProvider keyProvider = new LocalKeyProvider(keyMap);

var options = new DecryptionOptions { KeyProvider = keyProvider };

DecryptionResult result = await DecryptionUtils.DecryptLogFileAsync(
    "logs/app.log",
    "logs/app-decrypted.log",
    options);
```

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

## Migration Guide: v4 → v5

### Step 1 — Install the Decrypt package

If your application decrypts log files programmatically, add the new package:

```bash
dotnet add package Serilog.Sinks.File.Decrypt
```

Applications that only write encrypted logs (i.e. only use `EncryptHooks`) require no changes.

### Step 2 — Update using directives

```csharp
// Before (v4)
using Serilog.Sinks.File.Encrypt;
using Serilog.Sinks.File.Encrypt.Models;

// After (v5)
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;
```

### Step 3 — Replace `CryptographicUtils.DecryptLogFileAsync`

```csharp
// Before (v4)
await CryptographicUtils.DecryptLogFileAsync(input, output, options);

// After (v5)
await DecryptionUtils.DecryptLogFileAsync(input, output, options);
```

### Step 4 — Update custom `IKeyProvider` implementations

Change the implemented interface from `Serilog.Sinks.File.Encrypt.Interfaces.IKeyProvider` to
`Serilog.Sinks.File.Decrypt.Interfaces.IKeyProvider`. The interface contract is identical.

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