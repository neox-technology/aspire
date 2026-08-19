# Arcade bootstrap

| Field | Value |
|-------|-------|
| Slug | `arcade-bootstrap` |
| Status | implemented |
| Last code review | 2026-08-16 |

## Summary

This repository already uses **Microsoft.DotNet.Arcade.Sdk** on **.NET 10** for clone-and-build and local pack: `global.json`, `NuGet.config`, `Directory.Build.*`, `eng/common`, preview versioning, MIT `LICENSE`, root `Build.cmd` / `build.sh`, and **`Neox.Aspire.slnx`**. GitHub Actions pack/publish is **not** in this tree. Do not refresh `eng/common` as part of the agent-kit bootstrap.

## User scenarios

1. **Contributor clones this repository** — `.\Build.cmd` or `./build.sh` restores the Arcade SDK and builds `Neox.Aspire.slnx`.
2. **Contributor packs locally** — `./eng/common/build.sh --restore --build --pack --configuration Release` (or `Build.cmd`) writes Shipping nupkgs under `artifacts/packages/`.
3. **Agent refreshes Arcade later** — follows `.cursor/skills/arcade-bootstrap` only when explicitly asked; this spec records the current pin, it does not authorize a silent refresh.

## Business rules

1. **Arcade toolset** — `sdk.version` / `tools.dotnet` are **.NET 10** (`10.0.110` in `global.json`). `msbuild-sdks.Microsoft.DotNet.Arcade.Sdk` is `10.0.0-beta.26324.4`.
2. **Feeds** — `NuGet.config` uses `<clear />` and includes `dotnet-eng`, `dotnet-public`, `dotnet-tools`, **`dotnet10`**, and `nuget.org`.
3. **MIT license** — root `LICENSE` + `PackageLicenseExpression=MIT`; `Company` / `Authors` / `Copyright` are Neox Technology.
4. **Pack default** — `IsPackable` is `false` repo-wide; Shipping projects opt in.
5. **Preview** — `PreReleaseVersionLabel` is `preview`; `StabilizePackageVersion` is `false` until GA is decided (`eng/Versions.props`).
6. **No GitHub Actions** — CI pack and publish-nuget workflows are not in this repository ([`nuget-org`](nuget-org.md)).
7. **Solution format** — the repo solution is `Neox.Aspire.slnx` (not `.sln`).

## Dependencies

- [`aspire-bootstrap`](aspire-bootstrap.md) — repository identity
- [`domain-glossary`](domain-glossary.md) — **Neox Aspire**, **Shipping**
- [`nuget-org`](nuget-org.md) — local pack vs absent publish workflows

## Out of scope

- Refreshing `eng/common` or changing Arcade / SDK pins in this bootstrap
- GitHub Actions CI / publish to GitHub Packages or nuget.org
- Azure DevOps dnceng pipelines
- `darc` / Maestro / BAR
- Helix test queues
- MicroBuild signing

## Acceptance criteria

- [x] `global.json`, `NuGet.config`, `Directory.Build.props`, `Directory.Build.targets` exist
- [x] `eng/common/` is present; root `Build.cmd` / `build.sh` exist
- [x] `eng/Versions.props` exists (preview)
- [x] Proprietary-vs-MIT: root `LICENSE` is MIT; packages use `PackageLicenseExpression`
- [x] `Neox.Aspire.slnx` is the repo solution; SDK / TFM are **.NET 10** / `net10.0`; feed includes `dotnet10`
- [x] No `.github/workflows/` in this tree
- [x] Skill `.cursor/skills/arcade-bootstrap` is present for a later explicit refresh

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Skill | `.cursor/skills/arcade-bootstrap` |
| SDK pin | `global.json` — `10.0.110` / Arcade `10.0.0-beta.26324.4` |
| Solution | `Neox.Aspire.slnx` (libraries under `src/`; harnesses under `tests/`) |
| License | `LICENSE` (MIT) |
| Build | `Build.cmd` → `eng/common/Build.ps1` |
