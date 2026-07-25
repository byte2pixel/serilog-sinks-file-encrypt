# Example.FileBasedApp — decrypting mixed-version logs

This example proves that v6.0.0 decrypts **mixed-version log files**: a single encrypted log
containing sessions written by an old release (v4.0.0, legacy v1 on-disk format) *and* by the
current release (v2 on-disk format) decrypts in one pass with one key.

The programs are .NET 10 **file-based apps** — standalone `.cs` files run directly with
`dotnet run <file>.cs`, no project files needed. The directives on the first line declare
their dependencies:

| File | Dependency | What it does |
|------|------------|--------------|
| `v4Log.cs` | `#:package` → `Serilog.Sinks.File.Encrypt` **4.0.0** from NuGet | Appends an encrypted **v1-format** session to `file-based.log` |
| `vLatestLog.cs` | `#:project` → the in-repo `Serilog.Sinks.File.Encrypt` | Appends an encrypted **v2-format** session (with end-of-log seal) to the same file |
| `vLatestDecrypt.cs` | `#:project` → the in-repo `Serilog.Sinks.File.Decrypt` | Decrypts the mixed file and prints per-session format version and seal status |

Both writers use the same key (`keyId: "file-based-key"`) and prefix their messages with
`[v4]` / `[vLatest]` so you can tell which version produced each entry in the decrypted output.

## Prerequisites

- .NET 10 SDK (the repo's `global.json` already pins one)
- The `serilog-encrypt` CLI for key generation:

  ```bash
  dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli
  ```

## Walkthrough

Run everything from this directory.

### 1. Generate a key pair

```bash
serilog-encrypt generate --output . --format Xml --plaintext
```

This creates `public_key.xml` and `private_key.xml`, which the programs read directly with
`File.ReadAllText`. The demo uses plaintext XML keys to stay self-contained and compatible
with the v4.0.0 package; for real applications prefer the CLI's default — a
passphrase-protected PEM private key.

### 2. Write logs with both versions

```bash
dotnet run v4Log.cs
dotnet run vLatestLog.cs
```

Run them in any order, as many times as you like — each run appends one more encrypted
session to `file-based.log`. The point is that decryption works regardless of which version
wrote which session, as long as you hold the matching private key.

### 3. Decrypt the mixed file

```bash
dotnet run vLatestDecrypt.cs
```

After one run of each writer you should see:

```text
Decrypted 26 message(s) from 2 session(s):
  Session 0: format v1, 13 message(s), seal: NotApplicable
  Session 1: format v2, 13 message(s), seal: Sealed
Decrypted log written to file-based-decrypted.log
```

Sessions written by v4.0.0 report `format v1` with seal `NotApplicable` (the legacy format
has no end-of-log seal); sessions written by the current version report `format v2` with
seal `Sealed`, cryptographically verifying the session was cleanly closed and complete.

Open `file-based-decrypted.log` to see the interleaved `[v4]` and `[vLatest]` entries in
plain text.

You can also decrypt the same file with the CLI instead of the library:

```bash
serilog-encrypt decrypt file-based.log -k private_key.xml --id file-based-key
```

## Starting over

All generated files (`file-based.log`, `file-based-decrypted.log`, and the key pair) are
gitignored. Delete `file-based.log` and `file-based-decrypted.log` to start a fresh mix;
keep the keys unless you also want to re-encrypt from scratch.
