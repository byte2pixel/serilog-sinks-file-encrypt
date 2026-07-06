# CLAUDE.md

## Project Overview

Serilog.Sinks.File.Encrypt is a hook for Serilog.Sinks.File that transparently encrypts log
files using hybrid encryption (RSA key exchange + AES-256-GCM frames), plus a matching
decryption library and a Spectre.Console CLI. MIT-licensed, published to NuGet.

- Targets **net8.0 + net10.0** (LTS releases only). C# 14, nullable enabled, implicit usings.
- Warnings are errors; assemblies are strong-named; versioning is **MinVer** from git tags.
- **Central package management**: versions live in `Directory.Packages.props` per tree
  (`src/`, `tests/`, `examples/`) — never put `Version=` on a `PackageReference`.

## Repository Layout

```
build.cs                            # Cake.Sdk build script — run via `dotnet make`
src/
  Serilog.Sinks.File.Encrypt/      # Sink hook: EncryptHooks, LogWriter, Writers/
  Serilog.Sinks.File.Decrypt/      # Decryption library: LogReader, Readers/
  Serilog.Sinks.File.Encrypt.Core/ # Shared crypto primitives; EncryptionConstants.cs
  Serilog.Sinks.File.Encrypt.Cli/  # `serilog-encrypt` CLI (Spectre.Console.Cli + DI)
tests/                              # xunit.v3 + NSubstitute + Shouldly; Tests.Shared helpers
examples/                           # Console, WebApi, Benchmarks — excluded from Cake build
resources/nuget/                    # Per-package NuGet readme files
.github/workflows/                  # ci.yaml, publish.yaml, codeql-analysis.yaml
```

## Architecture Notes

- **The on-disk format is versioned**: v1 (legacy, read-only) and v2 (current: AAD-bound
  frames + end-of-log seal). `src/Serilog.Sinks.File.Encrypt.Core/EncryptionConstants.cs` is
  the source of truth. Any change to bytes on disk is a breaking-compat event — flag it
  explicitly before touching the wire format.
- **The Encrypt/Decrypt split is deliberate**: sink consumers never pull in decryption code.
  Both depend only on Core; never add a reference between Encrypt and Decrypt.
- **Core has zero external dependencies** — keep it that way.
- **`tests/Serilog.Sinks.File.Decrypt.Tests/Fixtures/v1/` is frozen**: never regenerate or
  modify these files; they are the v1 backward-compatibility gate (see the README there).
- **Examples are intentionally excluded from the build pipeline** so their dependencies'
  advisories can't fail CI — don't "fix" that.

## Build, Test, Format

One-time per clone: `dotnet tool restore` (installs `make`, `csharpier`, `husky`).

- **Prefer `dotnet make` over raw `dotnet build`/`dotnet test`** — it runs the real pipeline
  (Clean → Restore → Lint → Build → Test) with warnings-as-errors, matching CI exactly.
  - `dotnet make Test` / `dotnet make Build` / `dotnet make Lint`; default target = Package.
  - Args: `--configuration=Debug|Release` (default Release), `--collect-coverage`
    (→ `./.coverage`).
- Raw `dotnet test tests/<project>` is fine for a fast inner loop on one test project.
- **Format with CSharpier**: `dotnet csharpier format <paths>` (config in `.csharpierrc.json`:
  printWidth 100, 4-space indent). Husky pre-commit formats staged files automatically.
- **Line endings are OS-native in the working tree** and normalized to LF in git
  (`* text=auto` in `.gitattributes`; CSharpier `endOfLine: auto`). Keep a file's existing
  endings when editing; never hand-normalize line endings — git does it at commit.
- The Lint target additionally runs `dotnet format style --verify-no-changes` — run both
  CSharpier and `dotnet make Lint` before considering work done.
- Style rules enforced as errors: file-scoped namespaces, usings outside namespace,
  `_camelCase` private fields.
- SDK is pinned in `global.json` (10.0.100, rollForward latestFeature).

## Pre-Change Workflow

Before writing any code:

1. **Read the GitHub issue first.** If the task references an issue (or likely has one), run
   `gh issue view <n>` and read it fully before planning.
2. **Ask before deciding.** When a design decision comes up, present the options and clearly
   label which is **best practice** and which is merely **easiest**, with a one-line
   trade-off for each. Never silently pick one.
3. **Ask about missing information** instead of assuming — especially public-API impact,
   on-disk format impact, and net8.0 vs net10.0 behavior differences.
4. **Plan verification before implementing.** State which tests cover the change (existing
   or new, and in which test project), whether an example or benchmark run is warranted, and
   the v1/v2 compatibility implications if the format is touched.
5. Implement, then finish with `dotnet csharpier format <changed paths>` and
   `dotnet make Test`.

## Releases & CI

- CI (`ci.yaml`) runs `dotnet tool restore` + `dotnet make` on PRs and pushes to main.
- Publishing happens on GitHub Release via `publish.yaml` → `dotnet make publish`.
- Versions come from MinVer git tags — never hand-edit version numbers.
- Update `CHANGELOG.md` for user-visible changes; per-package docs live in `resources/nuget/`.
