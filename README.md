# Neox Aspire

Shared [.NET Aspire](https://aspire.dev/) NuGet packages under the `Neox.Aspire.*` namespace for Neox projects — hosting packages and other reusable libraries. This repository is **public**; it is not an AppHost and does not run Aspire orchestration itself.

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

Packable libraries live under `src/`. Hosting packages (when present) use the `Neox.Aspire.Hosting.*` namespace under `src/hosting/`.

| Project | Role |
|---------|------|
| [`Neox.Aspire.EntityFrameworkCore.MigrationWorker`](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | One-shot EF Core migration `BackgroundService` via `AddEfCoreMigrationService<TDbContext>()` |

Packages publish to **GitHub Packages linked to this repository** (not NuGet.org). License: MIT ([`LICENSE`](LICENSE)). Spec: [`nuget-github-packages`](specs/features/nuget-github-packages.md).

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

Authenticate with a PAT that has `read:packages`, or `GITHUB_TOKEN` in Actions when the consumer workflow can read this repository’s packages:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/neox-technology/index.json" \
  --name neox-aspire \
  --username USERNAME \
  --password YOUR_TOKEN \
  --store-password-in-clear-text
```

```xml
<PackageReference Include="Neox.Aspire.EntityFrameworkCore.MigrationWorker" Version="1.0.0-preview.*" />
```

### Local ProjectReference (dev)

```xml
<ProjectReference Include="..\..\path\to\aspire\src\Neox.Aspire.EntityFrameworkCore.MigrationWorker\Neox.Aspire.EntityFrameworkCore.MigrationWorker.csproj" IsAspireProjectResource="false" />
```

```csharp
builder.Services.AddEfCoreMigrationService<ApplicationDbContext>();
// Register the DbContext separately (provider-specific).
```

## CI / publish

| Event | Workflow | Behavior |
|-------|----------|----------|
| PR → `main` | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Pack (`*-ci` versions) + Aspire migration MSTest (Docker); no NuGet push |
| Merge to `main` | [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) | Pack with `OfficialBuildId` + push to GitHub Packages |

Optional: `workflow_dispatch` on the publish workflow to re-run from `main`.

### Versioning (Arcade / Aspire model)

| Context | Behavior |
|---------|----------|
| PR | `*-ci` versions (no `OfficialBuildId`) |
| Official build (merge to `main`) | `*-preview.…` via `PreReleaseVersionLabel` in [`eng/Versions.props`](eng/Versions.props) |
| Repo GA | Set `StabilizePackageVersion=true` in `eng/Versions.props` |

### License

MIT — [`LICENSE`](LICENSE). Packages use `PackageLicenseExpression=MIT`.

## Build

```powershell
.\Build.cmd
```

Unix: `./build.sh`. Outputs land under `artifacts/`. Pack locally:

```bash
./eng/common/build.sh --restore --build --pack --configuration Release
```

Shipping packages: `artifacts/packages/Release/Shipping/`.

## Integration tests

Per-provider Aspire MSTest harnesses live under [`tests/efcore-migration-worker/`](tests/efcore-migration-worker/) (`sqlserver`, `postgresql`, `mysql`). **Docker is required** (containers for the database engines).

```powershell
dotnet test tests/efcore-migration-worker/sqlserver/tests/Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.csproj -c Release
dotnet test tests/efcore-migration-worker/postgresql/tests/Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.csproj -c Release
dotnet test tests/efcore-migration-worker/mysql/tests/Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.csproj -c Release
```

## Specs

Index: [`specs/README.md`](specs/README.md).
