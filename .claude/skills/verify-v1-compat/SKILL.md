---
name: verify-v1-compat
description: Verify the frozen v1 fixtures are untouched and still decrypt byte-exact. Use after any change to Decrypt/Core code, before merging a branch that touches readers or the on-disk format, or when asked to check v1 backward compatibility.
---

# Verify v1 backward compatibility

The v1 fixtures in `tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/v1/` are the only proof
that the current reader still decrypts files written by the v5.x (v1-format) writer. This
skill checks both halves of that guarantee: the fixture bytes are unmodified, and the reader
still decrypts them byte-exact.

## Steps

1. **Fixture bytes unchanged in the working tree:**

   ```
   git status --porcelain -- tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/
   ```

   Must print nothing. Fixtures are `-text` in `.gitattributes`, so ANY entry here is a real
   byte change — stop and report it; do not commit.

2. **Fixture bytes unchanged vs main** (when on a branch):

   ```
   git diff main --name-only -- tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/
   ```

   Must print nothing. If a fixture differs from main, the branch has broken the compat gate.

3. **Fixtures still decrypt byte-exact** (runs on both net8.0 and net10.0):

   ```
   dotnet test tests/Serilog.Sinks.File.Decrypt.Tests --filter "FullyQualifiedName~V1Fixtures"
   ```

   This runs `V1Fixtures_DecryptByteExact_ReportNotApplicable` in `V2IntegrityTests.cs`,
   which decrypts every fixture and compares against its `.expected.txt` byte-for-byte.

4. Report all three results explicitly. Pass = empty, empty, all tests green on both TFMs.

## If something fails

- Modified fixture bytes: restore them (`git restore <fixture path>` / from main) — never
  regenerate with the current writer. Regeneration is only valid from the `5.x` release tag.
- Failing decryption with unmodified fixtures: the reader regressed — this is a release
  blocker, not a test to update.
