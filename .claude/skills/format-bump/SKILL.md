---
name: format-bump
description: Guardrailed checklist for introducing a new on-disk format version (e.g. v3). Use when a change cannot avoid altering the encrypted log wire format - new fields, different AAD, header changes, new record types.
---

# Introduce a new on-disk format version

A format bump is the highest-risk change in this repo: it is a breaking-compat event for
every consumer and permanently adds a version the reader must support forever. Read
`.claude/rules/crypto-format.md` and `EncryptionConstants.cs` first, and confirm with the
user that a bump is truly unavoidable (many changes fit inside the current format).

## Checklist (in order)

1. **Design against the threat model.** Write the new layout down (AAD contents, nonce
   discipline, seal semantics) and check it preserves the v2 guarantees: frame reorder/splice
   detection, truncation fingerprinting (`SealCountMismatch`), crash tolerance (`Unsealed` vs
   `RequireSealed`). Get the design agreed in a GitHub issue before coding (pattern: #83).
2. **Freeze the outgoing format FIRST**: while the current writer still emits vN, generate
   `Fixtures/v<N>/` fixtures using the `new-fixture` skill. This is the step that cannot be
   done later.
3. **Constants**: add `FormatVersionV<N+1>` to `EncryptionConstants.cs`, point
   `CurrentFormatVersion` at it, add any new lengths/markers as named constants with XML
   docs explaining the invariant (existing constants show the expected style).
4. **Writer**: emit only the new version. Keep `Writers/` and `Readers/` symmetric.
5. **Reader**: dispatch on the header version byte; every prior version keeps working —
   never delete or alter old-version read paths.
6. **Tests**: byte-exact fixture tests for the frozen vN set; integrity/tamper tests for the
   new version (model on `V2IntegrityTests.cs`); seal/truncation semantics on both TFMs.
   Run the `verify-v1-compat` skill — v1 must still pass untouched.
7. **Docs**: update `.claude/rules/crypto-format.md`, `CHANGELOG.md` (breaking-changes
   section with a compatibility matrix like the 6.0.0 entry), `SECURITY.md` if the threat
   model moved, and the `resources/nuget/*.md` package docs.
8. **Benchmark**: run the `benchmark` skill before/after — AAD or record layout changes hit
   the per-log-event hot path.
9. **Version**: this requires a major version bump (see the `release` skill when shipping).
