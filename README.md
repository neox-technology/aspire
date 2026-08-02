# Neox Aspire

[![CI](https://github.com/neox-technology/aspire/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/neox-technology/aspire/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

| Package | Downloads | README |
|---------|-----------|--------|
| [Neox.Aspire.EntityFrameworkCore.MigrationWorker](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | [README](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) |
| [Neox.Aspire.Hosting.Azure.CustomDomains](https://www.nuget.org/packages/Neox.Aspire.Hosting.Azure.CustomDomains) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.Hosting.Azure.CustomDomains.svg)](https://www.nuget.org/packages/Neox.Aspire.Hosting.Azure.CustomDomains) | [README](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/README.md) |
| [Neox.Aspire.Hosting.Auth.Abstractions](https://www.nuget.org/packages/Neox.Aspire.Hosting.Auth.Abstractions) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.Hosting.Auth.Abstractions.svg)](https://www.nuget.org/packages/Neox.Aspire.Hosting.Auth.Abstractions) | [README](src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/README.md) |
| [Neox.Aspire.Hosting.Auth.EntraId](https://www.nuget.org/packages/Neox.Aspire.Hosting.Auth.EntraId) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.Hosting.Auth.EntraId.svg)](https://www.nuget.org/packages/Neox.Aspire.Hosting.Auth.EntraId) | [README](src/hosting/Neox.Aspire.Hosting.Auth.EntraId/README.md) |

## What is Neox Aspire?

Neox Aspire is a set of shared [Aspire](https://aspire.dev/) NuGet packages under the `Neox.Aspire.*` namespace for Neox projects — hosting helpers and other reusable libraries.

This repository is **public**. It is not an AppHost and does not run Aspire orchestration itself; packages are published to [nuget.org](https://www.nuget.org/profiles/neox-technology).

## Getting started

Pick a package and install from nuget.org (see the package README for usage):

```bash
dotnet add package Neox.Aspire.EntityFrameworkCore.MigrationWorker
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
dotnet add package Neox.Aspire.Hosting.Auth.EntraId
```

| Package | Usage |
|---------|-------|
| [MigrationWorker](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) | `AddEfCoreMigrationService<TDbContext>()` |
| [CustomDomains](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/README.md) | `AddDomainOpsProvider` + `WithAzureCustomDomainOps` |
| [Auth.EntraId](src/hosting/Neox.Aspire.Hosting.Auth.EntraId/README.md) | `AddAuthProvider` + `.Entra` + `WithAuth` |

> [!NOTE]
> .NET SDK **10.0.110** is pinned in [`global.json`](global.json). Arcade installs a local copy via `eng/common` when needed.

## Useful links

- [Aspire documentation](https://aspire.dev/docs/)
- [microsoft/aspire](https://github.com/microsoft/aspire)
- [Feature specs](specs/README.md)
- [Domain glossary](specs/features/domain-glossary.md)
- [CI build status](https://github.com/neox-technology/aspire/actions/workflows/ci.yml)
- [Publish workflow](.github/workflows/publish-nuget.yml)

## What is in this repo?

Packable libraries live under `src/`. Hosting packages use the `Neox.Aspire.Hosting.*` namespace under `src/hosting/`.

| Package | Role |
|---------|------|
| [`Neox.Aspire.EntityFrameworkCore.MigrationWorker`](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | One-shot EF Core migration `BackgroundService` via `AddEfCoreMigrationService<TDbContext>()` |
| [`Neox.Aspire.Hosting.Azure.CustomDomains`](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains) | ACA custom domain ops (`WithAzureCustomDomainOps`, OctoDNS / managed certs via `aspire do`) |
| [`Neox.Aspire.Hosting.Auth.Abstractions`](src/hosting/Neox.Aspire.Hosting.Auth.Abstractions) | AuthOps core — shared gates + generic `AUTH_*` via `WithAuth` |
| [`Neox.Aspire.Hosting.Auth.EntraId`](src/hosting/Neox.Aspire.Hosting.Auth.EntraId) | AuthOps Entra — Graph app registrations + `.Entra(...)` |

Tests: [`tests/efcore-migration-worker/`](tests/efcore-migration-worker/) (Aspire harnesses, Docker), [`tests/azure-custom-domains/`](tests/azure-custom-domains/) (unit + sample AppHost), and [`tests/auth-providers/`](tests/auth-providers/) (AuthOps unit tests + sample AppHost Blazor/ops). Specs: [`specs/`](specs/README.md).

### Build

```cmd
Build.cmd -configuration Release
```

Unix: `./build.sh --configuration Release`. Pack:

```bash
./eng/common/build.sh --restore --build --pack --configuration Release
```

Shipping packages land under `artifacts/packages/Release/Shipping/`.

Run tests (Docker required for EF Core harnesses):

```cmd
Build.cmd -configuration Release -test
```

### Git flow / CI / publish

Branch model: `feature/*` → `develop`; `release/*` / `hotfix/*` → `main`; `main` holds shipped releases. Spec: [`gitflow-ci`](specs/features/gitflow-ci.md).

| Event | Workflow | Behavior |
|-------|----------|----------|
| Push `feature/**` | [`.github/workflows/gitflow-auto-pr.yml`](.github/workflows/gitflow-auto-pr.yml) | Auto-PR → `develop` |
| Push `release/**` / `hotfix/**` | same | Auto-PR → `main` |
| Merge `feature/**` → `develop` | [`.github/workflows/gitflow-cleanup-feature.yml`](.github/workflows/gitflow-cleanup-feature.yml) | Delete feature branch |
| `workflow_dispatch` on `develop` | [`.github/workflows/gitflow-start-release.yml`](.github/workflows/gitflow-start-release.yml) | Cut `release/x.y.z` or `release/x.y.z-preview.N` from [`eng/Versions.props`](eng/Versions.props) + PR → `main` |
| Merge `release/**` / `hotfix/**` → `main` | [`.github/workflows/gitflow-finish.yml`](.github/workflows/gitflow-finish.yml) | Tag `v` + branch version (prerelease if suffix), GitHub Release, sync PR `main` → `develop`, delete branch |
| PR → `develop` or `main` | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Build, test, pack (`*-ci` versions); no NuGet push |
| Push / merge to `main` | [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) | Pack with `OfficialBuildId` + push to nuget.org |

Optional: `workflow_dispatch` on the publish workflow to re-run from `main`. Git automation uses org App **`neox-gitflow`** (not a PAT). Squash-merge only (linear history).

Publishing uses [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). Versioning follows the Arcade / Aspire model (`PreReleaseVersionLabel` / `StabilizePackageVersion` in [`eng/Versions.props`](eng/Versions.props)). Spec: [`nuget-org`](specs/features/nuget-org.md).

## License

The code in this repo is licensed under the [MIT](LICENSE) license.
