---
paths:
  - "build.cs"
  - ".github/workflows/**"
  - "resources/nuget/**"
  - "src/**/*.csproj"
  - "**/Directory.Packages.props"
  - "**/Directory.Build.props"
---

# Packaging and release rules

## Versioning and publishing

- Versions come from **MinVer** (git tags) — never hand-edit a version in a csproj or props
  file. A release = create a git tag + GitHub Release; `publish.yaml` then runs
  `dotnet make publish` with a NuGet API key from OIDC login.
- The `Publish-NuGet` task in `build.cs` is guarded with
  `WithCriteria(BuildSystem.IsRunningOnGitHubActions)` — it cannot and should not run
  locally. Local `dotnet make` stops at Package (nupkgs land in `./.artifacts/`).
- Only `src/**` projects are packed; examples and tests never ship.

## Project/package configuration

- **Central package management**: `ManagePackageVersionsCentrally` is on. Add or bump
  versions in the `Directory.Packages.props` of the right tree (`src/`, `tests/`,
  `examples/`); `PackageReference` items carry no `Version=`.
- `src/Directory.Build.props` defines the shared shipping config: C# 14, nullable, embedded
  debug symbols + SourceLink, strong-name signing with `resources/serilog-sinks-file-encrypt.snk`,
  Roslynator analyzers, NuGet metadata (logo, readme, MIT license). Changes there affect
  every shipped package — treat edits as release-engineering changes.
- Each package embeds its readme from `resources/nuget/<PackageId>.md` — update the matching
  file whenever public API or usage changes.
- `global.json` pins the SDK (10.0.100, rollForward latestFeature); CI installs 8.0.x and
  10.0.x. Changing target frameworks is a policy decision (LTS-only) — raise it, don't do it.

## CI expectations

- `ci.yaml` = `dotnet tool restore` + `dotnet make` (default target: Package, which chains
  Lint → Build → Test) + Codecov upload. Anything that fails `dotnet make` locally fails CI.
- `codeql-analysis.yaml` does a raw `dotnet build` — it does not use the Cake script.
- Warnings are errors in both `build.cs` (TreatAllWarningsAs Error) and the props — do not
  suppress warnings to get green; fix them or discuss.
