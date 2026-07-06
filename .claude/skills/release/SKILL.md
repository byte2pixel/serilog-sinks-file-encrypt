---
name: release
description: Cut a release - CHANGELOG finalization, GitHub Release with a MinVer semver tag, and NuGet publish verification. Use when asked to release, tag, or publish a new version (stable or preview).
---

# Release a new version

Versioning is **MinVer**: the version comes from the git tag on the released commit. Tags are
**bare semver with NO `v` prefix** (`5.0.0`, `6.0.0-preview.1`) — ignore the ancient `v*`
tags in history. Publishing is fully automated: creating a GitHub Release triggers
`.github/workflows/publish.yaml` → `dotnet make publish` → nuget.org (OIDC key, no secrets
to handle). Never hand-edit version numbers anywhere.

## Steps

1. **Preconditions** — confirm before anything else:
   - On `main`, up to date, clean tree; CI green for HEAD (`gh run list --branch main -L 3`).
   - `gh release list -L 5` to see the latest version; pick the next one per semver
     (breaking → major; preview suffix `-preview.N` for pre-releases).
2. **CHANGELOG** (stable releases): change `## [X.Y.Z] - Unreleased` to the release date
   (`## [X.Y.Z] - YYYY-MM-DD`). Follows Keep a Changelog. Commit via PR if branch protection
   requires it. Previews can release with the section still `Unreleased`.
3. **Confirm with the user** the exact version and target commit — a release is public and
   publishes to nuget.org; it cannot be quietly undone.
4. **Create the release** (this creates the tag):

   ```
   gh release create <version> --title "<version>" --notes "<summary or CHANGELOG excerpt>"
   ```

   Add `--prerelease` for `-preview.N` versions. Add `--target <sha>` if not releasing HEAD.
5. **Verify the publish**: `gh run watch` the `publish.yaml` run, then confirm the packages
   (Encrypt, Decrypt, Cli, Core) show the new version:

   ```
   dotnet package search Serilog.Sinks.File.Encrypt --exact-match --prerelease
   ```

## Notes

- A failed publish run can be re-run from the release's workflow (`gh run rerun <id>`); do
  not delete/recreate tags — nuget.org versions are immutable once pushed.
- `MinVerSkip` is set for Debug builds — version checks must use Release builds.
