---
name: new-fixture
description: Generate and register a frozen on-disk format fixture (encrypted .log + .expected.txt pair) for backward-compatibility testing. Use before a format-version bump to freeze the outgoing format, or when a new compat scenario needs a pinned fixture.
---

# Create a frozen format fixture

Fixtures pin an on-disk format exactly as a specific writer version produced it, so future
readers can prove byte-exact backward compatibility. Model everything on the existing v1 set:
`tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/v1/` and its `README.md`.

## Procedure

1. **Timing matters**: fixtures for format vN must be generated while the vN-emitting writer
   is still the current code (e.g. freeze v2 fixtures BEFORE merging a v3 writer). Once the
   writer is gone, regeneration requires checking out the old release tag.
2. **Dedicated throwaway key pair** — never reuse the v1 fixture keys or any real key:
   generate a fresh 2048-bit pair with `CryptographicUtils.GenerateRsaKeyPair()` (or the CLI
   `generate` command) and commit both halves as `fixture-private-key.pem` /
   `fixture-public-key.pem` in the new fixture folder. Document that they are not secrets.
3. **Generate** with a one-off scratch program or temporary test using the real `LogWriter`
   (see `Serilog.Sinks.File.Tests.Shared/EncryptionTestBase.CreateEncryptedStream` for the
   pattern — dispose the writer so the session is sealed). Cover at least: a single session,
   multiple appended sessions, and a non-empty `keyId`.
4. **Write the pair**: `<scenario>.log` (encrypted bytes) and `<scenario>.expected.txt`
   (exact plaintext the decryption must yield — byte-exact, LF endings, no trailing edits).
5. **Register** in a `Fixtures/v<N>/` folder with a `README.md` copying the v1 README's
   structure: what each file contains, the writer version/commit that produced them, and the
   do-not-regenerate rule. Confirm the test csproj copies fixtures
   (`<None Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />` covers new
   folders automatically).
6. **Protecting the bytes**: `.gitattributes` already marks `tests/**/Fixtures/** -text` —
   verify with `git check-attr text -- <new .log file>` (should say `unset`).
7. **Prove them**: add or extend a decryption test that reads each fixture in
   `ErrorHandlingMode.ThrowException` and compares byte-for-byte against `.expected.txt`
   (see `V1Fixtures_DecryptByteExact_ReportNotApplicable` in `V2IntegrityTests.cs`).
   Run it on both TFMs before committing.

After committing, the fixtures are frozen: never regenerate, reformat, or re-encode them.
