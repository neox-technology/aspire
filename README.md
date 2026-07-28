# Neox Aspire

Shared [.NET Aspire](https://aspire.dev/) hosting packages for Neox projects.

## Git flow

This repository uses git-flow:

- `main` — production releases
- `develop` — integration / development

Feature work lands on `feature/*` branches from `develop`.

## Stack

| Item | Choice |
|------|--------|
| Runtime | .NET **10** |
| Build | Arcade SDK (local `eng/common`) |
| Solution | `Neox.Aspire.slnx` |

## Packages

| Project | Role |
|---------|------|
| [`Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker`](src/hosting/Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker) | Reusable EF Core migration worker hosting helpers (skeleton; feature API TBD) |

Packages publish to **GitHub Packages linked to this repository** (not NuGet.org). License: proprietary Neox Technology ([`LICENSE.txt`](LICENSE.txt)). Spec: [`nuget-github-packages`](specs/features/nuget-github-packages.md).

## Prerequisites

- .NET SDK **10.0.110** (pinned in `global.json`; Arcade installs a local copy via `eng/common` if needed)

## Consume from NuGet (GitHub Packages)

Feed URL (owner namespace — packages are associated with this repo):

`https://nuget.pkg.github.com/neox-technology/index.json`

```xml
<!-- NuGet.config in the consuming AppHost repo -->
<configuration>
  <packageSources>
    <add key="neox-aspire" value="https://nuget.pkg.github.com/neox-technology/index.json" />
  </packageSources>
</configuration>
```

Authenticate with a PAT that has `read:packages` (and access to this private repo), or `GITHUB_TOKEN` in Actions when the consumer workflow can read this repo’s packages:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/neox-technology/index.json" \
  --name neox-aspire \
  --username USERNAME \
  --password YOUR_TOKEN \
  --store-password-in-clear-text
```

```xml
<PackageReference Include="Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker" Version="1.0.0-preview.*" />
```

### Local ProjectReference (dev)

```xml
<ProjectReference Include="..\..\path\to\aspire\src\hosting\Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker\Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker.csproj" IsAspireProjectResource="false" />
```

## CI / publish

| Event | Workflow | Behavior |
|-------|----------|----------|
| PR → `main` | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Pack only (`*-ci` versions); no NuGet push |
| Merge to `main` | [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) | Pack with `OfficialBuildId` + push to GitHub Packages |

Optional: `workflow_dispatch` on the publish workflow to re-run from `main`.

### Versioning (Arcade / Aspire model)

| Context | Behavior |
|---------|----------|
| PR | `*-ci` versions (no `OfficialBuildId`) |
| Official build (merge to `main`) | `*-preview.…` via `PreReleaseVersionLabel` in [`eng/Versions.props`](eng/Versions.props) |
| Repo GA | Set `StabilizePackageVersion=true` in `eng/Versions.props` |

### License

Proprietary — [`LICENSE.txt`](LICENSE.txt). Packages use `PackageLicenseFile` (not an open-source SPDX expression such as MIT).

## Build

```powershell
.\Build.cmd
```

Unix: `./build.sh`. Outputs land under `artifacts/`. Pack locally:

```bash
./eng/common/build.sh --restore --build --pack --configuration Release
```

Shipping packages: `artifacts/packages/Release/Shipping/`.

## Specs

Index: [`specs/README.md`](specs/README.md).
