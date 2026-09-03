---
name: arcade-bootstrap
description: >-
  Bootstraps a .NET repository with Microsoft.DotNet.Arcade.Sdk (global.json,
  Directory.Build.*, eng/common, NuGet.config, Versions.props) and optional
  GitHub Actions NuGet publish to GitHub Packages. Use when the user asks to
  onboard Arcade, add eng/common, set up Arcade SDK, clone-and-build with Arcade,
  or wire CI pack / publish-nuget workflows.
disable-model-invocation: true
---

# Arcade SDK bootstrap

Bootstrap a **target repository** for local clone-and-build with the Arcade SDK,
plus optional GitHub Actions pack/publish to **GitHub Packages**.

Do **not** set up dnceng pipelines, darc/Maestro dependency flow, or Helix.

Official guides: [Onboarding.md](https://github.com/dotnet/arcade/blob/main/Documentation/Onboarding.md), [ArcadeSdk.md](https://github.com/dotnet/arcade/blob/main/Documentation/ArcadeSdk.md).

## Before starting

1. Confirm the target repo root (workspace root or path the user gives).
2. If Arcade files already exist (`eng/common`, `Microsoft.DotNet.Arcade.Sdk` in `global.json`), ask whether to refresh or abort.
3. Prefer adapting an existing solution layout over inventing a new one.
4. Resolve `RepositoryUrl` / GitHub owner from `git remote get-url origin` (HTTPS form `https://github.com/<OWNER>/<REPO>`). Do **not** hardcode another repo’s URL.
5. **License mode** — ask the user; **default for Neox private repos = proprietary**:
   - **Proprietary** (default): root `LICENSE.txt` + `PackageLicenseFile` (no SPDX MIT).
   - **OSS reusable**: `PackageLicenseExpression` (MIT or Apache-2.0) + SPDX `LICENSE` file.
6. If the repo will ship NuGet packages, plan GitHub Actions CI + publish (step 10). For non-trivial publish work in a Neox product repo, add a feature spec under `specs/features/` first.

## Workflow

Copy this checklist and track progress:

```
Arcade bootstrap:
- [ ] Resolve Arcade + .NET SDK versions
- [ ] NuGet.config feeds
- [ ] global.json
- [ ] License mode chosen (proprietary default / OSS)
- [ ] Directory.Build.props / Directory.Build.targets (metadata + authors)
- [ ] eng/common from dotnet/arcade
- [ ] eng/Versions.props (preview) + eng/Version.Details.xml
- [ ] Root Build.cmd / build.sh wrappers
- [ ] License file + src conventions
- [ ] GitHub Actions CI + publish-nuget (if packing)
- [ ] README consume snippet (if publishing)
- [ ] Local restore/build/pack smoke test
```

### 1. Resolve versions

Fetch current versions at execution time — **do not** invent or freeze stale Arcade versions in templates.

Preferred sources (in order):

1. [`dotnet/arcade` `global.json`](https://raw.githubusercontent.com/dotnet/arcade/main/global.json) — use matching `tools.dotnet` / `sdk.version` and `msbuild-sdks.Microsoft.DotNet.Arcade.Sdk`.
2. If the target already pins a .NET SDK, keep that SDK line and only take the Arcade SDK version from Arcade’s `global.json` when compatible; otherwise align both.

Record the chosen versions and use them in `global.json`.

### 2. NuGet.config

Ensure root `NuGet.config` includes at least:

- `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json` (Arcade SDK)
- `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json`
- `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json`
- `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnetN/nuget/v3/index.json` where **N** is the current major .NET version in use

Use `<clear />` on package sources (and disabled sources) so restores are deterministic. Keep any extra feeds the repo already needs.

Templates: [references/file-templates.md](references/file-templates.md).

### 3. global.json

Create or update root `global.json` with:

- `sdk.version` + `rollForward` (typically `latestFeature`) aligned with Arcade’s tooling
- `tools.dotnet` equal to that SDK version
- `msbuild-sdks.Microsoft.DotNet.Arcade.Sdk` set to the resolved Arcade version

Optional: `sdk.paths` / `errorMessage` pointing at `./eng/common/dotnet.cmd` / `dotnet.sh` when matching Arcade’s layout.

### 4. Directory.Build.props / Directory.Build.targets

**`Directory.Build.props`** — import `Sdk.props`, then:

- License: proprietary → `PackageLicenseFile` + `PackageLicenseFullPath`; OSS → `PackageLicenseExpression`
- `PackageProjectUrl` / `RepositoryUrl` from the git remote; `RepositoryType` = `git`; `PublishRepositoryUrl` = `true`
- `EmbedUntrackedSources`, `IncludeSymbols` (consider `PackageReadmeFile` when a package README exists)
- **`IsPackable`**: prefer default `false` and set `true` only on Shipping projects. Avoid repo-wide `IsPackable=true` when test projects exist (they would pack).

**`Directory.Build.targets`** — import `Sdk.targets`, then **override Arcade Microsoft defaults**:

- Set `Company`, `Authors`, `Copyright` to `Neox Technology` **after** the `Sdk.targets` import (Arcade overwrites earlier values).
- If proprietary: pack `LICENSE.txt` into the nupkg via a `None` item (`Pack="true"`).

Templates: [references/file-templates.md](references/file-templates.md).

### 5. Copy eng/common

Copy the **entire** `eng/common` tree from [dotnet/arcade `eng/common`](https://github.com/dotnet/arcade/tree/main/eng/common) into the target repo.

Practical approach:

```bash
# From a temp clone or sparse checkout of dotnet/arcade @ main
# Copy eng/common -> <repo>/eng/common
```

Then mark shell scripts executable for Git:

```bash
git add --chmod=+x $(git ls-files 'eng/common/**/*.sh')
```

Do not hand-edit files under `eng/common`; they must stay identical to Arcade and are updated via dependency flow later (out of this skill’s scope).

### 6. Versions.props and Version.Details.xml (preview by default)

Add:

- `eng/Versions.props` — `VersionPrefix`, `PreReleaseVersionLabel` = `preview`, `StabilizePackageVersion` = `false` (flip to `true` for repo-wide GA). Document per-project `SuppressFinalPackageVersion` to stay prerelease after stabilize.
- `eng/Version.Details.xml` — minimal dependency description file (can start nearly empty of ProductDependencies)

See templates in [references/file-templates.md](references/file-templates.md).

Optional: `eng/Build.props` if the repo must list `ProjectToBuild` (multiple solutions or non-root `.sln`).

### 7. Root build wrappers

Add thin wrappers that call Arcade scripts (no custom logic):

- `Build.cmd` → `eng\common\Build.ps1 -restore -build %*`
- `build.sh` → `eng/common/build.sh --restore --build "$@"` (executable)

Same pattern for `Restore.cmd` / `Test.cmd` if useful.

### 8. Conventions

- Build outputs live under `artifacts/` (Arcade default) — do not invent a parallel `bin/` at repo root.
- Source projects under `src/`, using `Sdk="Microsoft.NET.Sdk"`.
- Root license file matching the chosen mode (`LICENSE.txt` proprietary, or SPDX `LICENSE` for OSS).
- Test projects: `*.Tests` / `*.UnitTests` / `*.IntegrationTests` naming; keep `IsPackable=false`.

### 9. License file

- **Proprietary**: write Neox `LICENSE.txt` (template in [references/file-templates.md](references/file-templates.md)).
- **OSS**: write MIT or Apache-2.0 `LICENSE` and set `PackageLicenseExpression` accordingly.

### 10. GitHub Actions (recommended when packing NuGet)

If the repo publishes packages, add:

- `.github/workflows/ci.yml` — `pull_request` → `main`; pack only; upload Shipping artifact; **no push**
- `.github/workflows/publish-nuget.yml` — `push` → `main` + `workflow_dispatch`; `OfficialBuildId`; push Shipping `*.nupkg` to `https://nuget.pkg.github.com/<OWNER>/index.json` with `GITHUB_TOKEN`, `packages: write`, `--skip-duplicate`, `/p:Sign=false`

Substitute `<GITHUB_OWNER>` from the git remote. Triggers are **`main` only** (not `develop`) — intentional for release packages under git-flow.

Full templates: [references/github-workflows.md](references/github-workflows.md).

Document feed + PAT (`read:packages`) + a sample `PackageReference` in the repo README.

### 11. Smoke test

From the target root:

- Windows: `.\Build.cmd`
- Unix: `./build.sh`
- Pack: `./eng/common/build.sh --restore --build --pack --configuration Release` — Shipping under `artifacts/packages/Release/Shipping/`

Fix restore/feed/SDK issues until restore + build succeed (or until the only failures are pre-existing product code problems unrelated to Arcade wiring).

Validation checklist: [references/checklist.md](references/checklist.md).

## Pitfalls (do not skip)

| Risk | Guidance |
|------|----------|
| Authors still “Microsoft” | Set `Company` / `Authors` / `Copyright` **after** `Sdk.targets` |
| `IsPackable=true` global | Packs tests; default `false`, opt-in on Shipping projects |
| Symbols not on the feed | `IncludeSymbols` produces `.snupkg`; publish template pushes `*.nupkg` only — push symbols explicitly if needed |
| Missing `RepositoryType` / README in package | Set `RepositoryType=git`; consider `PackageReadmeFile` |
| CI on `develop` | Out of scope for this skill’s publish model (`main` only) |
| Signing | Keep `/p:Sign=false` in GHA; MicroBuild signing is out of scope |
| No consumer docs | README must document GitHub Packages feed + auth |
| No feature spec | Non-trivial NuGet publish in Neox repos needs `specs/features/<slug>.md` |

## Out of scope

- Azure DevOps pipelines (dnceng-public / dnceng internal)
- `darc` subscriptions, Maestro, BAR publishing
- Helix test queues
- Signing / official MicroBuild configuration beyond what Arcade imports by default
- Public NuGet.org publishing
- CI / publish triggers on `develop`

GitHub Actions pack + push to **GitHub Packages** is **in scope** (step 10).

## Additional resources

- [references/file-templates.md](references/file-templates.md) — minimal file templates
- [references/github-workflows.md](references/github-workflows.md) — CI + publish-nuget YAML
- [references/checklist.md](references/checklist.md) — post-bootstrap verification
