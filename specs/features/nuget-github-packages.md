# NuGet publish to GitHub Packages

| Field | Value |
|-------|-------|
| Slug | `nuget-github-packages` |
| Status | defined |
| Last code review | 2026-07-28 |

## Summary

Arcade pack/publish for Neox Aspire packages to **GitHub Packages linked to this repository** (`neox-technology/aspire`), following microsoft/aspire conventions (`-restore -build -pack`, `OfficialBuildId`, `StabilizePackageVersion`). Packages are MIT-licensed (`PackageLicenseExpression`).

## User scenarios

- A contributor opens a PR targeting `main`; CI packs with `*-ci` versions and does **not** push NuGet.
- A PR merges to `main`; the publish workflow packs with `OfficialBuildId` and pushes Shipping nupkgs to GitHub Packages associated with this repo.
- A consumer restores packages from `https://nuget.pkg.github.com/neox-technology/index.json` with credentials that can read this repository’s packages (`read:packages`).
- When the repo goes GA (`StabilizePackageVersion=true`), packages stabilize via Arcade versioning in `eng/Versions.props`.

## Routes (if UI)

_N/A — CI / packaging._

## Dependencies

- Arcade SDK (local `eng/common`, `Build.cmd` / `build.sh`)
- GitHub Actions + `GITHUB_TOKEN` with `packages: write` on this repository

## Out of scope

- Public NuGet.org publishing
- Organization-scoped packages with no repository association
- Azure Artifacts / dnceng / 1ES / BAR / MicroBuild signing
- CI or publish on `develop`
- WinGet / npm / CLI installer channels from microsoft/aspire

## Acceptance criteria

- [x] Spec documents PR → `main` CI (pack `*-ci`, no push) and merge → publish to repo-linked GitHub Packages.
- [x] `Directory.Build.props` sets package metadata (`RepositoryUrl`, MIT via `PackageLicenseExpression`, symbols) for repo linkage.
- [x] Root `LICENSE` is MIT (SPDX).
- [x] `.github/workflows/ci.yml` runs on `pull_request` to `main` only (pack, no push).
- [x] `.github/workflows/publish-nuget.yml` runs on `push` to `main` (and optional `workflow_dispatch`) and pushes Shipping packages.
- [x] README documents consumption feed, auth, Arcade versioning, and MIT license.

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
| Shipping package (migration worker) | `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/` |

NuGet API URL is owner-namespaced (`nuget.pkg.github.com/neox-technology`); packages are associated with this repository via Actions publish + `RepositoryUrl`.
