# NuGet pack and publish

| Field | Value |
|-------|-------|
| Slug | `nuget-org` |
| Status | implemented |
| Last code review | 2026-10-15 |

## Summary

Arcade pack for Neox Aspire packages. Public package ids ship on **nuget.org** (MIT, `PackageLicenseExpression`) via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) from **publish-final-version** (`release/x.x.x`, `environment: production`). Preview validation uses **daily** packages on the private GitHub Packages feed from **publish-preview-version** (`release/x.x.x-preview.x`). Local pack remains available. Release identity lives in `eng/Versions.props`. Coexists with GitFlow Actions ([`gitflow-ci`](gitflow-ci.md)).

## User scenarios

1. **Contributor packs locally** — Arcade restore/build/pack writes Shipping nupkgs (and snupkgs when present) under `artifacts/packages/`.
2. **Operator validates a preview** — **publish-preview-version** on `release/x.x.x-preview.x` packs with `PreReleaseVersionLabel=daily`, `StabilizePackageVersion=false`, and `OfficialBuildId`, then pushes to GitHub Packages.
3. **Operator ships** — **publish-final-version** on `release/x.x.x` packs with `OfficialBuildId` from `Versions.props` (`StabilizePackageVersion=true`) and pushes to nuget.org via `NuGet/login@v1` OIDC.
4. **Consumer installs from nuget.org** — packages remain installable; no private feed required for public consumption.

## Business rules

1. **Local pack** — `Build.cmd` / `eng/common/build.sh --pack` remains a supported delivery path.
2. **Private feed** — only from `release/x.x.x-preview.x` via **publish-preview-version** (daily identity, not the public nuget.org version). Push uses the job `GITHUB_TOKEN` (`packages: write`).
3. **nuget.org** — only from **publish-final-version** (Trusted Publishing; workflow file name and environment must match the nuget.org policy).
4. **MIT** — root `LICENSE` + `PackageLicenseExpression=MIT`.
5. **Shipping opt-in** — repo `IsPackable` default is false; Shipping projects opt in.
6. **No publish from develop** — develop never pushes NuGet. Preview git finalize squash-merges into `develop` after the package push; it does not publish from `develop`.

## Dependencies

- [`aspire-bootstrap`](aspire-bootstrap.md) — repository identity
- [`arcade-bootstrap`](arcade-bootstrap.md) — Arcade clone-and-build
- [`gitflow-ci`](gitflow-ci.md) — release branch workflows
- [`domain-glossary`](domain-glossary.md) — **Shipping**, **daily**

## Out of scope

- Long-lived nuget.org API keys stored as repo secrets
- Azure Artifacts / dnceng / 1ES / BAR / MicroBuild signing
- WinGet / npm / CLI installer channels
- Automatic publish on every push to `main` (final publish is manual)
- Authenticating restore against a private NuGet source (`NuGet.config` public feeds only)

## Acceptance criteria

- [x] Spec states local Arcade pack plus manual GHA publish paths
- [x] `Directory.Build.props` sets package metadata (`RepositoryUrl`, MIT via `PackageLicenseExpression`, symbols)
- [x] Root `LICENSE` is MIT (SPDX)
- [x] `eng/Versions.props` maps `StabilizePackageVersion=true` → `DotNetFinalVersionKind=release`
- [x] README documents local pack, MIT license, GitHub Packages daily path, and nuget.org Trusted Publishing
- [x] `.github/workflows/publish-preview-version.yml` pushes Shipping packages to GitHub Packages from `release/x.x.x-preview.x`
- [x] `.github/workflows/publish-final-version.yml` uses `NuGet/login@v1` + `id-token: write` and pushes Shipping packages to nuget.org from `release/x.x.x`

## Terminology

See [`domain-glossary`](domain-glossary.md). **Daily** = Arcade prerelease label `daily` producing `X.Y.Z-daily.{OfficialBuildId}` on **publish-preview-version**.

## Implementation notes

| Item | Path |
|------|------|
| Preview publish workflow | `.github/workflows/publish-preview-version.yml` |
| nuget.org final workflow | `.github/workflows/publish-final-version.yml` |
| Package metadata | `Directory.Build.props` / `Directory.Build.targets` |
| License | Root `LICENSE` (MIT); NuGet `PackageLicenseExpression=MIT` |
| Versions | `eng/Versions.props` |
| Shipping packages | `{paths}` in `.cursor/rules/neox-rules.json` |
| GitFlow | [`gitflow-ci`](gitflow-ci.md) |
| Secret | `NUGET_USER` — nuget.org profile name (not email), projet scope |

### Trusted Publishing policy (manual on nuget.org)

| Field | Value |
|-------|-------|
| Repository Owner | `neox-technology` |
| Repository | `aspire` |
| Workflow File | `publish-final-version.yml` |
| Environment | `production` |
| Owner | nuget.org account/org that owns the packages |
