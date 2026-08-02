# NuGet publish to nuget.org

| Field | Value |
|-------|-------|
| Slug | `nuget-org` |
| Status | implemented |
| Last code review | 2026-08-01 |

## Summary

Arcade pack/publish for Neox Aspire packages to **nuget.org** via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (GitHub Actions OIDC → short-lived API key with `NuGet/login@v1`), following microsoft/aspire conventions (`-restore -build -pack`, `OfficialBuildId`, `StabilizePackageVersion`). Packages are MIT-licensed (`PackageLicenseExpression`). Coexists with GitFlow automation ([`gitflow-ci`](gitflow-ci.md)); publish remains **main-only**.

## User scenarios

- A contributor opens a PR targeting `develop` or `main`; CI builds, tests, and packs with `*-ci` versions and does **not** push NuGet.
- A PR merges to `main`; the publish workflow packs with `OfficialBuildId`, exchanges an OIDC token for a temporary nuget.org API key, and pushes Shipping nupkgs (and snupkgs when present).
- A consumer installs packages from nuget.org with no private feed or PAT.
- When the repo goes GA (`StabilizePackageVersion=true`), packages stabilize via Arcade versioning in `eng/Versions.props`.

## Routes (if UI)

_N/A — CI / packaging._

## Dependencies

- Arcade SDK (local `eng/common`, `Build.cmd` / `build.sh`)
- GitHub Actions with `id-token: write` for OIDC
- nuget.org Trusted Publishing policy for this repository
- Repo secret `NUGET_USER` (nuget.org profile name, not email)
- GitFlow branch model and Actions ([`gitflow-ci`](gitflow-ci.md))

## Out of scope

- GitHub Packages publishing
- Long-lived nuget.org API keys stored as repo secrets
- Azure Artifacts / dnceng / 1ES / BAR / MicroBuild signing
- Publishing NuGet from `develop`
- WinGet / npm / CLI installer channels from microsoft/aspire

## Acceptance criteria

- [x] Spec documents PR → `develop`/`main` CI (pack `*-ci`, no push) and merge → publish to nuget.org via Trusted Publishing.
- [x] `Directory.Build.props` sets package metadata (`RepositoryUrl`, MIT via `PackageLicenseExpression`, symbols).
- [x] Root `LICENSE` is MIT (SPDX).
- [x] `.github/workflows/ci.yml` runs on `pull_request` to `develop` and `main` (build/test/pack, no push).
- [x] `.github/workflows/publish-nuget.yml` uses `NuGet/login@v1` + `id-token: write` and pushes Shipping packages to `https://api.nuget.org/v3/index.json` (main only + `workflow_dispatch`).
- [x] README documents nuget.org consumption, Arcade versioning, and MIT license (no GitHub Packages feed/auth).

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path |
|------|------|
| CI workflow | `.github/workflows/ci.yml` |
| Publish workflow | `.github/workflows/publish-nuget.yml` |
| Package metadata | `Directory.Build.props` / `Directory.Build.targets` (repo URL, MIT `PackageLicenseExpression`, Neox copyright, symbols) |
| License | Root `LICENSE` (MIT); NuGet `PackageLicenseExpression=MIT` |
| Versions | `eng/Versions.props` (`PreReleaseVersionLabel`; GA via `StabilizePackageVersion`) |
| Shipping packages | `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/`; `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`; `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/`; `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` |
| Secret | `NUGET_USER` — nuget.org profile name |

### Trusted Publishing policy (manual on nuget.org)

| Field | Value |
|-------|-------|
| Repository Owner | `neox-technology` |
| Repository | `aspire` |
| Workflow File | `publish-nuget.yml` |
| Environment | _(empty)_ |
| Owner | nuget.org account/org that owns the packages |
