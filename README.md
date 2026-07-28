# Neox Aspire

[![CI](https://github.com/neox-technology/aspire/actions/workflows/ci.yml/badge.svg)](https://github.com/neox-technology/aspire/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg?label=NuGet)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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

Packages publish to **[nuget.org](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)**. License: MIT ([`LICENSE`](LICENSE)). Spec: [`nuget-org`](specs/features/nuget-org.md).

## Prerequisites

- .NET SDK **10.0.110** (pinned in `global.json`; Arcade installs a local copy via `eng/common` if needed)

## Consume from NuGet

```bash
dotnet add package Neox.Aspire.EntityFrameworkCore.MigrationWorker
```

```xml
<PackageReference Include="Neox.Aspire.EntityFrameworkCore.MigrationWorker" Version="1.0.0-preview.*" />
```

See the [package README](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) for usage.

## CI / publish

| Event | Workflow | Behavior |
|-------|----------|----------|
| PR → `main` | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Pack (`*-ci` versions) + Arcade `-test` (Docker); no NuGet push |
| Merge to `main` | [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) | Pack with `OfficialBuildId` + Trusted Publishing push to nuget.org |

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

```cmd
Build.cmd -configuration Release
```

Unix: `./build.sh --configuration Release`. Outputs land under `artifacts/`. Pack locally:

```bash
./eng/common/build.sh --restore --build --pack --configuration Release
```

Shipping packages: `artifacts/packages/Release/Shipping/`.

## Integration tests

Per-provider Aspire xUnit harnesses live under [`tests/efcore-migration-worker/`](tests/efcore-migration-worker/) (`sqlserver`, `postgresql`, `mysql`, `oracle`). **Docker is required**.

Same convention as other Neox Arcade repos:

```cmd
Build.cmd -configuration Release -test
```

Unix: `./build.sh --configuration Release --test`.

## Specs

Index: [`specs/README.md`](specs/README.md).
