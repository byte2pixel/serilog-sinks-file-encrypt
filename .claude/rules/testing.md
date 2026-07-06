---
paths:
  - "tests/**"
---

# Testing rules

## Stack and layout

- xunit **v3** (`xunit.v3` packages), NSubstitute for mocking, Shouldly for assertions,
  coverlet for coverage. Test settings in each project's `xunit.runner.json`.
- `tests/Serilog.Sinks.File.Tests.Shared/` holds shared helpers — reuse before writing new ones:
  - `EncryptionTestBase` — generates a throwaway RSA key pair, provides ready
    `EncryptionOptions`/`DecryptionOptions`, tracks and disposes created streams.
    `CreateEncryptedStream(messages)` returns a sealed (cleanly closed) v2 session.
  - `V1TestStreamBuilder` — builds v1-format streams in memory for compat tests, so v1
    scenarios never require touching the frozen fixtures.
  - `TestUtils` — misc helpers.

## Frozen v1 fixtures (critical)

`tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/v1/` pins the v1 on-disk format as written
by the shipping v5.x writer. Full policy in the `README.md` in that folder. Rules:

- **Never regenerate, edit, re-encode, or reformat any file there.** If regeneration is ever
  truly needed, it must be done from the `5.x` release tag's `LogWriter`.
- `.gitattributes` marks fixtures `-text` and `.gitignore` carries a negation for
  `tests/**/Fixtures/**/*.log` — don't "clean up" either; the `*.expected.txt` comparisons
  are byte-exact and CRLF normalization breaks them.
- The fixture key pair (`fixture-private-key.pem`/`fixture-public-key.pem`) is a dedicated
  throwaway — not a secret, but never reuse it outside these fixtures.
- Fixtures are copied to the output directory via `CopyToOutputDirectory=PreserveNewest`.

New fixtures for the **current** format are fine — generate them with the current writer and
commit them alongside an `.expected.txt`, following the v1 folder's naming pattern.

## Running tests

- Full pipeline (what CI runs): `dotnet make Test`.
- Fast inner loop: `dotnet test tests/<Project>.Tests`.
- Coverage: `dotnet make Test --collect-coverage` → cobertura + trx under
  `./.coverage/<ProjectName>/`.
- Tests run on both net8.0 and net10.0 — a fix verified on one TFM isn't done until both pass.
