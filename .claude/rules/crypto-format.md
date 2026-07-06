---
paths:
  - "src/Serilog.Sinks.File.Encrypt.Core/**"
  - "src/Serilog.Sinks.File.Encrypt/**"
  - "src/Serilog.Sinks.File.Decrypt/**"
---

# Encrypted log format rules

`src/Serilog.Sinks.File.Encrypt.Core/EncryptionConstants.cs` is the single source of truth
for all wire-format constants. Read it before reasoning about the format; do not restate its
values from memory.

## Format versions

- **v1** — AES-GCM frames with no associated data and no seal. Read-only since v6.0.0; the
  writer never emits it again. The reader must decrypt v1 files byte-for-byte forever.
- **v2** (current) — every AES-GCM record binds 41 bytes of associated data:
  `SHA-256(full session header)` (32) + frame sequence (8, big-endian, starts at 0) +
  frame type (1). A clean close writes an authenticated end-of-log **seal record**
  (marker `0xFF 0x42 0x32 0x53`, encrypted payload = final frame count as big-endian ulong).

## Invariants that are easy to break

- **The seal nonce is reserved**: initial session nonce counter − 1, NOT the writer's rolling
  nonce. The reader's nonce only advances per successfully decrypted frame, so a
  tail-truncated file would otherwise desync and report generic tamper instead of the precise
  `SealCountMismatch` (declared N vs decrypted K) diagnostic.
- **The seal's frame count lives in the encrypted payload, not the AAD** — a count divergence
  must decrypt and be reportable, not surface as an opaque `CryptographicException`.
- **Frame sequence starts at 0** and is independent of the nonce counter (which starts random).
- **The header hash binds version + keyId + RSA payload**; the v2 header layout is
  byte-identical to v1 except the version byte. No separate session-id field exists.
- **Session poisoning**: any frame auth failure abandons the whole session; the reader resyncs
  only at the next session header.
- Seal semantics: `SealCountMismatch` always throws in `ThrowException` mode (positively
  detected truncation); `Unsealed` throws only with `DecryptionOptions.RequireSealed`
  (crash-tolerance trade-off). v1 sessions report `NotApplicable` and count as non-sealed
  under `RequireSealed`.

## Rules for changing the format

- Any change to bytes on disk requires a **new format version** (v3): add a new constant,
  bump `CurrentFormatVersion`, keep readers for every old version, and add frozen fixtures
  for the outgoing version before the old writer disappears. Never mutate the meaning of an
  existing version.
- Threat-model limits are documented (v1: issue #82 / PR #92; v2: issue #83 / PR #93). An
  attacker holding only the public key can still fabricate whole sessions — do not claim
  origin authentication anywhere in docs or XML comments.

## Project conventions

- **Core stays dependency-free** (pure BCL crypto). If a change to Core needs a package,
  the change belongs elsewhere.
- Encrypt and Decrypt both depend only on Core; never reference one from the other.
- Writers (`src/Serilog.Sinks.File.Encrypt/Writers/`) and readers
  (`src/Serilog.Sinks.File.Decrypt/Readers/`) mirror each other (session/header/frame). A
  change on one side almost always needs a symmetric change and tests on the other.
