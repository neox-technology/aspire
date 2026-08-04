# NuGet publish to nuget.org

| Field | Value |
|-------|-------|
| Slug | `nuget-org` |
| Status | defined |
| Last code review | 2026-08-04 |

## Summary

Arcade pack/publish for Neox Aspire packages: **public** delivery to **nuget.org** via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (GitHub Actions OIDC → short-lived API key with `NuGet/login@v1`) on `main`, and **private daily** validation builds to **GitHub Packages** on `release/**` before the public ship. Follows microsoft/aspire conventions (`-restore -build -pack`, `OfficialBuildId`, `StabilizePackageVersion` → `DotNetFinalVersionKind`). Packages are MIT-licensed (`PackageLicenseExpression`). Coexists with GitFlow automation ([`gitflow-ci`](gitflow-ci.md)). Release identity lives in `eng/Versions.props`, updated by start-release so shipping versions align with the git tag core.

## User scenarios

- A contributor opens a PR targeting `develop` or `main`; CI builds, tests, and packs with `*-ci` versions and does **not** push NuGet.
- A contributor pushes (or updates) a `release/**` branch; the publish workflow packs Shipping as `X.Y.Z-daily.{OfficialBuildId}` (forcing `PreReleaseVersionLabel=daily` and `StabilizePackageVersion=false` regardless of committed props) and pushes to the private GitHub Packages NuGet feed for pre-ship validation.
- A PR merges to `main` (typically via GitFlow release/hotfix); the publish workflow packs with `OfficialBuildId`, exchanges an OIDC token for a temporary nuget.org API key, and pushes Shipping nupkgs (and snupkgs when present) using committed `Versions.props`.
- A consumer installs packages from nuget.org with no private feed or PAT.
- An internal validator installs a release-branch daily from GitHub Packages (`https://nuget.pkg.github.com/neox-technology/index.json`) with a GitHub credential that can read packages.
- When `StabilizePackageVersion=true` on `main` (stable release / promote), packages stabilize to exact `X.Y.Z` via `DotNetFinalVersionKind=release`.
- Preview releases on `main` keep `StabilizePackageVersion=false`: packages use Arcade’s date-based prerelease suffix with the release `VersionPrefix`.

## Routes (if UI)

_N/A — CI / packaging._

## Dependencies

- Arcade SDK (local `eng/common`, `Build.cmd` / `build.sh`)
- GitHub Actions with `id-token: write` for OIDC (nuget.org) and `packages: write` (GitHub Packages)
- nuget.org Trusted Publishing policy for this repository
- Repo secret `NUGET_USER` (nuget.org profile name, not email)
- `GITHUB_TOKEN` for GitHub Packages push on `release/**`
- GitFlow branch model and Actions ([`gitflow-ci`](gitflow-ci.md))

## Out of scope

- Long-lived nuget.org API keys stored as repo secrets
- Azure Artifacts / dnceng / 1ES / BAR / MicroBuild signing
- Publishing NuGet from `develop` or `hotfix/**` (daily is `release/**` only; public is `main` only)
- WinGet / npm / CLI installer channels from microsoft/aspire
- Replacing nuget.org as the public consumer channel

## Acceptance criteria

- [x] Spec documents PR → `develop`/`main` CI (pack `*-ci`, no push), push `release/**` → daily GitHub Packages, and merge → `main` publish to nuget.org via Trusted Publishing.
- [x] `Directory.Build.props` sets package metadata (`RepositoryUrl`, MIT via `PackageLicenseExpression`, symbols).
- [x] Root `LICENSE` is MIT (SPDX).
- [x] `eng/Versions.props` maps `StabilizePackageVersion=true` → `DotNetFinalVersionKind=release`.
- [x] `.github/workflows/ci.yml` runs on `pull_request` to `develop` and `main` (build/test/pack, no push) and guards release/hotfix version alignment.
- [x] `.github/workflows/publish-nuget.yml` on `main` (+ `workflow_dispatch` from main) uses `NuGet/login@v1` + `id-token: write` and pushes Shipping packages to `https://api.nuget.org/v3/index.json`.
- [x] `.github/workflows/publish-nuget.yml` on `release/**` packs with `/p:StabilizePackageVersion=false` `/p:PreReleaseVersionLabel=daily` `/p:OfficialBuildId=…` and pushes Shipping to `https://nuget.pkg.github.com/neox-technology/index.json` with `GITHUB_TOKEN` (`packages: write`).
- [x] README documents nuget.org consumption, Arcade versioning (including tag alignment), MIT license, and the private daily GitHub Packages feed for release-branch validation.

## Terminology

See [`domain-glossary`](domain-glossary.md). **Daily** = Arcade prerelease label `daily` producing `X.Y.Z-daily.{OfficialBuildId}` for private pre-ship packages (not the public nuget.org identity).

## Implementation notes

| Item | Path |
|------|------|
| CI workflow | `.github/workflows/ci.yml` |
| Publish workflow | `.github/workflows/publish-nuget.yml` |
| Package metadata | `Directory.Build.props` / `Directory.Build.targets` (repo URL, MIT `PackageLicenseExpression`, Neox copyright, symbols) |
| License | Root `LICENSE` (MIT); NuGet `PackageLicenseExpression=MIT` |
| Versions | `eng/Versions.props` (`VersionPrefix`, `PreReleaseVersionLabel`, `StabilizePackageVersion` → `DotNetFinalVersionKind`) |
| Daily overrides | MSBuild `/p:StabilizePackageVersion=false` `/p:PreReleaseVersionLabel=daily` on `release/**` only |
| Shipping packages | `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/`; `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`; `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/`; `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` |
| GitFlow version bump | [`gitflow-ci`](gitflow-ci.md) start-release commits `Versions.props` |
| Secret | `NUGET_USER` — nuget.org profile name |
| GitHub Packages | org `neox-technology`; auth `GITHUB_TOKEN` |

### Trusted Publishing policy (manual on nuget.org)

| Field | Value |
|-------|-------|
| Repository Owner | `neox-technology` |
| Repository | `aspire` |
| Workflow File | `publish-nuget.yml` |
| Environment | _(empty)_ |
| Owner | nuget.org account/org that owns the packages |
