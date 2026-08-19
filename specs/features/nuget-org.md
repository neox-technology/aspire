# NuGet pack (nuget.org identity)

| Field | Value |
|-------|-------|
| Slug | `nuget-org` |
| Status | defined |
| Last code review | 2026-08-16 |

## Summary

Arcade **local** pack for Neox Aspire packages. Public package ids historically ship on **nuget.org** (MIT, `PackageLicenseExpression`). **GitHub Actions publish is not in this tree**: no Trusted Publishing workflow, no daily GitHub Packages push. Follows microsoft/aspire versioning locally (`-restore -build -pack`, `StabilizePackageVersion` → `DotNetFinalVersionKind`). Release identity lives in `eng/Versions.props`. Coexists with the GitFlow **branch convention** ([`gitflow-ci`](gitflow-ci.md)).

## User scenarios

1. **Contributor packs locally** — Arcade restore/build/pack writes Shipping nupkgs (and snupkgs when present) under `artifacts/packages/`.
2. **Consumer installs from nuget.org** — existing published packages remain installable; this tree does not push new versions until a publish workflow exists again.
3. **Contributor sets preview vs stabilize** — edits `eng/Versions.props` (`VersionPrefix`, `PreReleaseVersionLabel`, `StabilizePackageVersion`).

## Business rules

1. **Local pack** — `Build.cmd` / `eng/common/build.sh --pack` is the delivery path in this tree.
2. **No publish workflows** — `.github/workflows/ci.yml` and `publish-nuget.yml` are absent. Do not treat nuget.org Trusted Publishing or GitHub Packages daily push as implemented here.
3. **MIT** — root `LICENSE` + `PackageLicenseExpression=MIT`.
4. **Shipping opt-in** — repo `IsPackable` default is false; Shipping projects opt in.
5. **Daily label** — the Arcade `daily` prerelease label remains a **term** for a possible future `release/**` private feed; it is not produced by an in-repo workflow today.

## Dependencies

- [`aspire-bootstrap`](aspire-bootstrap.md) — repository identity and no-GHA rule
- [`arcade-bootstrap`](arcade-bootstrap.md) — Arcade clone-and-build
- [`gitflow-ci`](gitflow-ci.md) — branch convention
- [`domain-glossary`](domain-glossary.md) — **Shipping**, **daily**

## Out of scope

- Restoring GitHub Actions publish in this bootstrap
- Long-lived nuget.org API keys stored as repo secrets
- Azure Artifacts / dnceng / 1ES / BAR / MicroBuild signing
- WinGet / npm / CLI installer channels

## Acceptance criteria

- [x] Spec states local Arcade pack and that GHA publish is **not** in this tree
- [x] `Directory.Build.props` sets package metadata (`RepositoryUrl`, MIT via `PackageLicenseExpression`, symbols)
- [x] Root `LICENSE` is MIT (SPDX)
- [x] `eng/Versions.props` maps `StabilizePackageVersion=true` → `DotNetFinalVersionKind=release`
- [x] README documents local pack, MIT license, and nuget.org consumption of **already published** packages
- [x] No `.github/workflows/` for CI or publish
- [ ] GitHub Actions restore: PR CI pack (`*-ci`, no push), `release/**` daily GitHub Packages, `main` nuget.org Trusted Publishing

## Terminology

See [`domain-glossary`](domain-glossary.md). **Daily** = Arcade prerelease label `daily` producing `X.Y.Z-daily.{OfficialBuildId}` when that pipeline exists (not wired here).

## Implementation notes

| Item | Path |
|------|------|
| CI / publish workflows | **Absent** |
| Package metadata | `Directory.Build.props` / `Directory.Build.targets` |
| License | Root `LICENSE` (MIT); NuGet `PackageLicenseExpression=MIT` |
| Versions | `eng/Versions.props` |
| Shipping packages | `{paths}` in `.cursor/rules/neox-rules.json` |
| GitFlow | [`gitflow-ci`](gitflow-ci.md) |
