# 6.0.0 CLI Overhaul — Manual Acceptance Test Plan (build artifacts)

Tests the packaged output of PR #103 end-to-end: the `serilog-encrypt` tool installed from
`./.artifacts/`, real encrypted logs produced by `Example.Console` referencing your generated
key, and the frozen v1 fixtures for backward compatibility.

> This file is untracked — commit it, move it to `docs/`, or delete it when done.
>
> Shell convention: PowerShell commands check `$LASTEXITCODE`; WSL/bash sections check `$?`.
> Run everything from the repo root unless stated. `$SB` is a scratch sandbox:
>
> ```powershell
> $SB = "$env:TEMP\serilog-encrypt-testplan"
> New-Item -ItemType Directory -Force $SB | Out-Null
> ```

## Exit-code oracle (used throughout)

| Code | Meaning | Multi-file precedence |
|---|---|---|
| 0 | Success | 1 > 2 > 4 > 5 > 3 |
| 1 | Runtime failure (IO/crypto) on ≥1 file | |
| 2 | Usage error / refused overwrite / missing passphrase (non-interactive) | |
| 3 | No input files matched | |
| 4 | Nothing decrypted (wrong key / not an encrypted log) | |
| 5 | `--require-sealed` not met (unsealed, tampered, or v1 session) | |

---

## 1. Build and install the artifacts

- [x] **1.1** `dotnet tool restore` then `dotnet make` completes green (Clean → Restore →
      Lint → Build → Test → Package).
- [x] **1.2** `.artifacts/` contains exactly 4 `.nupkg` files: `Serilog.Sinks.File.Encrypt`,
      `.Decrypt`, `.Encrypt.Core`, `.Encrypt.Cli`, all with the **same version**. Note it:
      ```powershell
      Get-ChildItem .artifacts\*.nupkg | Select-Object Name
      $VER = "<version from the filenames, e.g. 3.0.0-alpha.0.123>"
      ```
      (Highest reachable tag is currently `v3.0.0-alpha.0`, so expect a `3.0.0-alpha.0.N`
      MinVer height version — the number is irrelevant to the tests, but **pin it** so
      nuget.org's 5.0.0 can't shadow the local build.)
- [x] **1.3** Install the CLI from the local artifacts, pinned:
      ```powershell
      dotnet tool uninstall --global Serilog.Sinks.File.Encrypt.Cli 2>$null
      dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli --add-source ./.artifacts --version $VER
      serilog-encrypt --version
      ```
- [x] **1.4** Help text: `serilog-encrypt --help`, `serilog-encrypt generate --help`,
      `serilog-encrypt decrypt --help`. Decrypt help lists the exit codes and all new
      options (`-f|--force`, `--passphrase-env`, `--passphrase-file`, `--json`,
      `--require-sealed`, `-q`, `-v`). Generate help shows `--key-size` default 3072,
      format default Pem, `--plaintext`, `--force`.
- [x] **1.5** Unknown option → exit 2: `serilog-encrypt decrypt x.log --nope`;
      `echo $LASTEXITCODE` → `2` (Spectre's −1 normalized).

## 2. `generate` command

- [x] **2.1 Interactive default (prompt + confirm):** `serilog-encrypt generate -o $SB\keys`
      → prompts twice (hidden input), creates `private_key.pem` starting with
      `-----BEGIN ENCRYPTED PRIVATE KEY-----` and `public_key.pem` with
      `-----BEGIN RSA PUBLIC KEY-----`; message says passphrase-encrypted + no-recovery
      warning. Exit 0.
- [x] **2.2 Prompt mismatch:** run again with `--force`, type two different passphrases →
      "Passphrases do not match.", exit 2, key files unchanged.
- [x] **2.3 `--passphrase-env`:**
      ```powershell
      $env:TP_PASS = "test-passphrase-1"
      serilog-encrypt generate -o $SB\keys-env --passphrase-env TP_PASS
      ```
      No prompt, exit 0. Unset var and rerun with `--force` → exit 2 with "not set or empty".
- [x] **2.4 `--passphrase-file`:** first line of a file is used:
      ```powershell
      Set-Content $SB\pass.txt "file-passphrase`nsecond line ignored"
      serilog-encrypt generate -o $SB\keys-file --passphrase-file $SB\pass.txt
      ```
      Exit 0. Missing file → exit 2.
- [x] **2.5 Fallback env var:** with only `$env:SERILOG_ENCRYPT_PASSPHRASE = "fallback-pass"`
      set, `generate -o $SB\keys-fb` succeeds without prompting. Unset afterwards.
- [x] **2.6 Non-interactive, no source:** from a redirected shell:
      `"" | serilog-encrypt generate -o $SB\keys-ni` → exit 2 with guidance mentioning
      `--plaintext` and `SERILOG_ENCRYPT_PASSPHRASE` (no hang waiting for input).
- [x] **2.7 `--plaintext`:** `generate -o $SB\keys-plain --plaintext` → no prompt,
      `private_key.pem` starts `-----BEGIN RSA PRIVATE KEY-----`, output warns
      "NOT passphrase-protected". Exit 0.
- [x] **2.8 Xml requires `--plaintext`:** `generate -o $SB\keys-xml -f Xml` → validation
      error mentioning `--plaintext`, exit 2. With `--plaintext` added → `private_key.xml`
      + `public_key.xml`, exit 0.
- [x] **2.9 Overwrite protection:** rerun 2.1's command non-interactively → exit 2,
      "key file(s) already exist", original bytes untouched. With `--force` (+ passphrase
      source) → exit 0, files replaced.
- [x] **2.10 `-k` removed:** `generate -o $SB\x -k 4096` → exit 2 (unknown option);
      `--key-size 4096` works.
- [x] **2.11 Minimum key size:** `--key-size 1024 --plaintext` → exit 1 with the
      2048-minimum message.
- [x] **2.12 Quiet/verbose:** `-q` prints nothing on success (warnings only);
      `-q -v` together → validation error, exit 2.

### Key file permissions

- [x] **2.13 Windows ACL:** `icacls $SB\keys\private_key.pem` → inheritance disabled,
      exactly one ACE: your user with full control. `public_key.pem` keeps inherited ACEs.
- [x] **2.14 Unix 600 (WSL):** in WSL, generate onto the **Linux** filesystem (drvfs
      ignores chmod): `serilog-encrypt generate -o ~/tp-keys --passphrase-env TP_PASS` →
      `ls -l ~/tp-keys` shows `-rw-------` for `private_key.pem`, default mode for the
      public key. (Install the tool inside WSL from `/mnt/d/repos/serilog-sinks-file-encrypt/.artifacts` first.)

## 3. Produce encrypted test data

- [x] **3.1** Point `Example.Console` at your generated key (PEM content in the `.xml`
      resource name is fine — the key loader sniffs the format):
      ```powershell
      Copy-Item examples\Example.Console\public_key.xml $SB\public_key.xml.bak -ErrorAction SilentlyContinue
      Copy-Item $SB\keys\public_key.pem examples\Example.Console\public_key.xml -Force
      # Example targets net8.0;net10.0 — dotnet run needs an explicit framework:
      dotnet run --project examples\Example.Console -f net10.0
      ```
      Note the printed log directory; copy the produced `log*.txt` to `$SB\logs\sealed.log`.
      File content is binary gibberish (no plaintext log lines visible).
      **Restore the example key afterwards.** NOTE: `examples/Example.Console/public_key.xml`
      is **gitignored/untracked**, so `git restore` is a no-op — restore from the backup
      instead: `Copy-Item $SB\public_key.xml.bak examples\Example.Console\public_key.xml -Force`.
      Verified: `sealed.log` = 4344 bytes, header magic `FF 42 32 50`, no plaintext leaks.
      (`public_key.xml` is an *embedded resource*, so `dotnet run` must rebuild to pick up the swap.)
- [x] **3.2 Unsealed variant** (simulated crash — strip the 28-byte seal record):
      ```powershell
      $b = [IO.File]::ReadAllBytes("$SB\logs\sealed.log")
      [IO.File]::WriteAllBytes("$SB\logs\unsealed.log", $b[0..($b.Length-29)])
      ```
      Verified: seal marker `FF 42 32 53` sat at end−28; `unsealed.log` = 4316 bytes (4344 − 28).
- [x] **3.3 v1 fixture copy** (never touch the originals):
      ```powershell
      Copy-Item tests\Serilog.Sinks.File.Decrypt.Tests\Fixtures\v1\single-session.log $SB\logs\v1.log
      ```
      Verified: `v1.log` = 532 bytes.
- [x] **3.4 Junk file:** `Set-Content $SB\logs\junk.log "not encrypted at all"` (22 bytes).

## 4. `decrypt` command

Throughout: `$K = "$SB\keys\private_key.pem"` (encrypted key), keyId is `console-key-2026`
for the example-produced logs. Set `$env:SERILOG_ENCRYPT_PASSPHRASE` to your 2.1 passphrase
for non-interactive runs (or pass `--passphrase-env`).

- [x] **4.1 Happy path, encrypted key:**
      `serilog-encrypt decrypt $SB\logs\sealed.log -k $K --id console-key-2026`
      → exit 0; `sealed.decrypted.log` next to input; contents are readable Serilog lines
      including "Processing item 20"; session table shows v2 / Sealed; summary table renders;
      "All 1 session(s) sealed and complete".
- [x] **4.2 Interactive passphrase prompt:** clear the env var, rerun 4.1 with `--force` →
      hidden single prompt (no confirm), exit 0.
- [x] **4.3 Wrong passphrase:** `--passphrase-env` pointing at a wrong value → exit 1,
      clear "Decryption failed"/invalid-key message.
- [x] **4.4 Missing passphrase non-interactive:** no sources, stdin redirected → exit 2
      with guidance (no hang, no prompt).
- [x] **4.5 Default key hint:** in a dir containing only a legacy `private_key.xml`
      (from 2.8), run `serilog-encrypt decrypt some.log` → error mentions
      "Found 'private_key.xml' — pass -k private_key.xml".
- [x] **4.6 Overwrite refusal / `--force`:** rerun 4.1 → exit 2,
      "already exists (use --force to overwrite)", output file bytes unchanged;
      with `-f` → exit 0, overwritten.
- [x] **4.7 No files matched:** `decrypt "$SB\logs\*.nope" -k $K` → warning, exit 3.
- [x] **4.8 Wrong key → nothing decrypted:** decrypt `sealed.log` with a *different*
      generated key (e.g. `$SB\keys-plain\private_key.pem`) → exit 4, "Nothing decrypted"
      warning, and **no empty output file left behind**.
- [x] **4.9 Junk file:** decrypt `junk.log` with the right key → exit 4, same behavior.
- [x] **4.10 Wrong `--id`:** decrypt `sealed.log` with `--id wrong` → exit 4 (header can't
      be matched).
- [x] **4.11 Unsealed file:** decrypt `unsealed.log` → exit 0 but "UNSEALED" warning; all
      messages still recovered (crash tolerance).
- [x] **4.12 `--require-sealed` (no `--strict`):** same file + `--require-sealed` → decrypts
      and reports, exit **5**.
- [x] **4.13 `--require-sealed --strict`:** → file fails, exit **1**.
- [x] **4.14 v1 compat:** decrypt `v1.log` with the fixture key:
      ```powershell
      serilog-encrypt decrypt $SB\logs\v1.log -k tests\Serilog.Sinks.File.Decrypt.Tests\Fixtures\v1\fixture-private-key.pem
      ```
      → exit 0; session shows v1 / "legacy format, completeness cannot be verified"; output
      is **byte-identical** to the fixture expectation:
      ```powershell
      fc.exe /b $SB\logs\v1.decrypted.log tests\Serilog.Sinks.File.Decrypt.Tests\Fixtures\v1\single-session.expected.txt
      ```
- [x] **4.15 v1 + `--require-sealed`:** → exit **5** (NotApplicable counts as unverified).
- [x] **4.16 Glob batch + precedence:** put `sealed.log`, `unsealed.log`, `junk.log`, and a
      pre-existing `.decrypted` output in one dir; run over `*.log` **without** `-f` →
      the refused file wins: exit **2**; others still processed; `.decrypted.` inputs are
      skipped, not re-decrypted. Then with `-f` → exit **4** (junk's nothing-decrypted
      outranks 5 and 3).
- [x] **4.17 Output to directory:** `-o $SB\outdir` (no trailing name) → files land inside
      with `.decrypted` names; directory auto-created.
- [x] **4.18 Quiet / verbose:** `-q` shows only warnings/errors (still exits correctly);
      `-v` adds the sessions/messages/resync detail line.
- [x] **4.19 Markup-hostile name:** copy `sealed.log` to `weird[1].log`, decrypt → exit 0,
      path renders literally, no crash.

## 5. `--json` contract

- [x] **5.1 stdout is pure JSON:**
      ```powershell
      $json = serilog-encrypt decrypt $SB\logs\sealed.log -k $K --id console-key-2026 -f --json 2>$null
      $r = $json | ConvertFrom-Json
      $r.schemaVersion   # 1
      $r.exitCode        # 0, matches $LASTEXITCODE
      $r.files[0].outcome            # Success
      $r.files[0].sessions[0].sealStatus  # Sealed
      $r.summary.succeeded           # 1
      ```
- [x] **5.2 Human text on stderr only:** rerun with `2> $SB\stderr.txt` — stderr contains
      the tables/messages; stdout parses standalone. Piping works: `... --json | jq .` (WSL).
      (WSL had no jq/python3; pipe verified via `--json 2>$null | ConvertFrom-Json` instead.)
- [x] **5.3 Failure shapes:** wrong-key run with `--json` → `exitCode: 4`,
      `outcome: NothingDecrypted`; unsealed + `--require-sealed` → `exitCode: 5`,
      `summary.allSessionsSealed: false` while the file's own `allSessionsSealed` reflects
      the v1-tolerant per-file meaning.

## 6. Library packages smoke test (optional but recommended)

Round-trips the **packaged** Encrypt/Decrypt libraries rather than project references:

- [x] **6.1**
      ```powershell
      dotnet new console -o $SB\libsmoke && cd $SB\libsmoke
      dotnet add package Serilog.Sinks.File.Encrypt --source D:\repos\serilog-sinks-file-encrypt\.artifacts --version $VER
      dotnet add package Serilog.Sinks.File.Decrypt --source D:\repos\serilog-sinks-file-encrypt\.artifacts --version $VER
      ```
      Write a ~20-line Program.cs: `GenerateRsaKeyPair(2048, KeyFormat.Pem, "pw")` →
      encrypt a few lines through `LogWriter` → decrypt via `DecryptionUtils` +
      `new LocalKeyProvider("", privateKey, "pw")` → assert round trip and
      `result.NothingDecrypted == false`, `AllSessionsSealed == true`. Confirms the new
      passphrase APIs ship in the packages and the Encrypt/Decrypt/Core dependency chain
      resolves from the artifacts alone.

## 7. Cleanup

- [x] `dotnet tool uninstall --global Serilog.Sinks.File.Encrypt.Cli` (reinstall from
      nuget.org if you use the released tool day-to-day). Also uninstalled the WSL-side
      tool from 2.14 (was a stale 6.0.0-preview.1.4).
- [x] `git restore examples/Example.Console/public_key.xml`; `git status` clean.
      (Untracked, so restored from `$SB\public_key.xml.bak` — byte-identical.)
- [x] `Remove-Item -Recurse -Force $SB` and the WSL `~/tp-keys`.
- [x] Verify fixtures untouched: `git status --porcelain -- tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/` prints nothing.

## Known coverage notes

- Path-traversal containment and the interactive `TestConsole` paths are covered by unit
  tests only — traversal isn't reachable from the shell (`GetFileName` strips `..`), so no
  manual scenario exists for it.
- `SealCountMismatch` requires surgically dropping interior frames while keeping the seal;
  it's exercised by `V2IntegrityTests`, not reproduced manually here.
- Windows ACL restriction (2.13) silently skips under mocked filesystems; the manual check
  here is the authoritative one for real disks.
