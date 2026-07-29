# Neox Aspire

[![CI](https://github.com/neox-technology/aspire/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/neox-technology/aspire/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg?label=NuGet)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## What is Neox Aspire?

Neox Aspire is a set of shared [Aspire](https://aspire.dev/) NuGet packages under the `Neox.Aspire.*` namespace for Neox projects — hosting helpers and other reusable libraries.

This repository is **public**. It is not an AppHost and does not run Aspire orchestration itself; packages are published to [nuget.org](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker).

## Getting started

Install a package from nuget.org:

```bash
dotnet add package Neox.Aspire.EntityFrameworkCore.MigrationWorker
```

Or add a `PackageReference`:

```xml
<PackageReference Include="Neox.Aspire.EntityFrameworkCore.MigrationWorker" Version="1.0.0-preview.*" />
```

See the [package README](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) for usage.

> [!NOTE]
> .NET SDK **10.0.110** is pinned in [`global.json`](global.json). Arcade installs a local copy via `eng/common` when needed.

## Useful links

- [NuGet: Neox.Aspire.EntityFrameworkCore.MigrationWorker](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)
- [Aspire documentation](https://aspire.dev/docs/)
- [microsoft/aspire](https://github.com/microsoft/aspire)
- [Feature specs](specs/README.md)
- [CI build status](https://github.com/neox-technology/aspire/actions/workflows/ci.yml)
- [Publish workflow](.github/workflows/publish-nuget.yml)

## What is in this repo?

Packable libraries live under `src/`. Hosting packages (when present) use the `Neox.Aspire.Hosting.*` namespace under `src/hosting/`.

| Package | Role |
|---------|------|
| [`Neox.Aspire.EntityFrameworkCore.MigrationWorker`](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | One-shot EF Core migration `BackgroundService` via `AddEfCoreMigrationService<TDbContext>()` |
| [`Neox.Aspire.Hosting.Azure.CustomDomains`](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains) | ACA custom domain ops (`WithAzureCustomDomainOps`, OctoDNS / managed cert / GitHub vars via `aspire do`) |

Integration tests (Aspire xUnit harnesses, Docker required) live under [`tests/efcore-migration-worker/`](tests/efcore-migration-worker/). Specs: [`specs/`](specs/README.md).

### Build

```cmd
Build.cmd -configuration Release
```

Unix: `./build.sh --configuration Release`. Pack:

```bash
./eng/common/build.sh --restore --build --pack --configuration Release
```

Shipping packages land under `artifacts/packages/Release/Shipping/`.

Run tests (Docker required):

```cmd
Build.cmd -configuration Release -test
```

### Git flow

- `main` — production releases
- `develop` — integration / development

Feature work lands on `feature/*` branches from `develop`.

PRs to `main` pack and test; merges to `main` publish to nuget.org via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). Versioning follows the Arcade / Aspire model (`PreReleaseVersionLabel` / `StabilizePackageVersion` in [`eng/Versions.props`](eng/Versions.props)). Spec: [`nuget-org`](specs/features/nuget-org.md).

## License

The code in this repo is licensed under the [MIT](LICENSE) license.
