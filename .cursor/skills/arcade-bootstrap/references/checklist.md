# Arcade bootstrap — verification checklist

Run after wiring files. All items should pass before calling the bootstrap done.

## Files present

- [ ] `global.json` with matching `sdk.version` / `tools.dotnet` and `msbuild-sdks.Microsoft.DotNet.Arcade.Sdk`
- [ ] `NuGet.config` includes `dotnet-eng`, `dotnet-public`, `dotnet-tools`, and the current `dotnetN` feed
- [ ] `Directory.Build.props` imports `Sdk.props` / `Microsoft.DotNet.Arcade.Sdk`
- [ ] `Directory.Build.targets` imports `Sdk.targets` / `Microsoft.DotNet.Arcade.Sdk`
- [ ] `eng/common/` present and complete (not a partial copy)
- [ ] `eng/Versions.props` and `eng/Version.Details.xml` exist
- [ ] Root `Build.cmd` and/or `build.sh` dispatch to `eng/common`
- [ ] `.sh` under `eng/common` (and root `build.sh`) are executable in Git (`chmod +x` / `git add --chmod=+x`)

## License + package metadata

- [ ] License **mode** chosen (proprietary default for Neox private, or OSS)
- [ ] Proprietary: root `LICENSE.txt` + `PackageLicenseFile` / pack item in targets (no MIT SPDX)
- [ ] OSS: SPDX `LICENSE` + `PackageLicenseExpression` (MIT or Apache-2.0)
- [ ] `PackageProjectUrl` / `RepositoryUrl` match this repo’s git remote (not another repo)
- [ ] `RepositoryType` = `git`; `PublishRepositoryUrl` = `true`
- [ ] `Company` / `Authors` / `Copyright` = Neox Technology **after** `Sdk.targets`
- [ ] `IsPackable` not forced `true` repo-wide when test projects exist; Shipping projects opt in
- [ ] Optional: `PackageReadmeFile` if packaging a README

## Preview versioning

- [ ] `PreReleaseVersionLabel` = `preview` in `eng/Versions.props`
- [ ] `StabilizePackageVersion` = `false` (or documented plan for GA)
- [ ] Per-project `SuppressFinalPackageVersion` noted where a package must stay prerelease after stabilize

## GitHub Actions (when publishing NuGet)

- [ ] `.github/workflows/ci.yml` — PR → `main`, pack, artifact Shipping, no push
- [ ] `.github/workflows/publish-nuget.yml` — push → `main` (+ `workflow_dispatch`), `OfficialBuildId`, `packages: write`, push to `nuget.pkg.github.com/<OWNER>`
- [ ] Feed owner substituted from git remote
- [ ] `/p:Sign=false` on CI and publish
- [ ] README documents feed URL, `read:packages` auth, and a sample `PackageReference`
- [ ] Non-trivial publish: feature spec under `specs/features/` (Neox guideline)

## Layout

- [ ] Source projects under `src/` use `Sdk="Microsoft.NET.Sdk"`
- [ ] `tests/Directory.Build.props` imports the parent and sets `IsShipping=false` / `IsPackable=false` / `IsTestUtilityProject`
- [ ] xUnit runners that do not match `*.Tests` naming set `IsUnitTestProject=true` in the csproj
- [ ] No `.esproj` / `Microsoft.VisualStudio.JavaScript.Sdk`; nested `Directory.Build.*` import the parent
- [ ] No conflicting custom `Directory.Build.*` that skip Arcade imports
- [ ] Optional `eng/Build.props` lists the correct solution/projects if needed

## Build / pack

- [ ] `.\Build.cmd` or `./build.sh` restores the Arcade SDK from `dotnet-eng`
- [ ] Toolset restore completes under `artifacts/`
- [ ] Solution/projects build (failures limited to unrelated product issues)
- [ ] Outputs land under `artifacts/` (bin, obj, log, etc.)
- [ ] Pack produces Shipping nupkgs under `artifacts/packages/Release/Shipping/` when packing is in scope

## Pitfalls reviewed

- [ ] Authors not left as Microsoft (set after `Sdk.targets`)
- [ ] Symbol `.snupkg` push decided intentionally (default workflow pushes `*.nupkg` only)
- [ ] No CI/publish on `develop` unless the user explicitly expands scope
- [ ] Signing / MicroBuild / NuGet.org / dnceng left out of scope

## Do not require for this skill

- Azure Pipelines YAML on dnceng
- `darc` auth / subscriptions
- Helix or official signing setup
- Publish to nuget.org
