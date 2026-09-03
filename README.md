# Neox Aspire

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

| Package | Downloads | README |
|---------|-----------|--------|
| [Neox.Aspire.EntityFrameworkCore.MigrationWorker](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | [README](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) |
| [Neox.Aspire.Hosting.Azure.CustomDomains](https://www.nuget.org/packages/Neox.Aspire.Hosting.Azure.CustomDomains) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Neox.Aspire.Hosting.Azure.CustomDomains.svg)](https://www.nuget.org/packages/Neox.Aspire.Hosting.Azure.CustomDomains) | [README](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/README.md) |
| [Neox.Azure.Provisioning.Graph](src/provisioning/Neox.Azure.Provisioning.Graph/README.md) | — | [README](src/provisioning/Neox.Azure.Provisioning.Graph/README.md) |
| [Neox.Keycloak.Provisioning.Realm](src/provisioning/Neox.Keycloak.Provisioning.Realm/README.md) | — | [README](src/provisioning/Neox.Keycloak.Provisioning.Realm/README.md) |
| [Neox.Aspire.Hosting.Azure.EntraId](src/hosting/Neox.Aspire.Hosting.Azure.EntraId/README.md) | — | [README](src/hosting/Neox.Aspire.Hosting.Azure.EntraId/README.md) |
| [Neox.Aspire.Hosting.Keycloak](src/hosting/Neox.Aspire.Hosting.Keycloak/README.md) | — | [README](src/hosting/Neox.Aspire.Hosting.Keycloak/README.md) |
| [Neox.Aspire.Hosting.Keycloak.EntraId](src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/README.md) | — | [README](src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/README.md) |

## What is Neox Aspire?

Neox Aspire is a set of shared [Aspire](https://aspire.dev/) NuGet packages under the `Neox.Aspire.*` namespace for Neox projects — hosting helpers and other reusable libraries (`neox-technology/aspire`).

This repository is **public** (MIT). It is not an AppHost and does not run Aspire orchestration itself. **Neox SaaS** may mount this tree as a Git submodule (`aspire/`) so contributors can develop here. That host is a **development workspace only** — not a product owner of these packages.

Prefer **submodule contribution**: commit in this repository first, then update the saas gitlink.

Already published packages remain on [nuget.org](https://www.nuget.org/profiles/neox-technology). Pack locally with Arcade, or use the manual release GitHub Actions below.

## Getting started

Pick a package and install from nuget.org (see the package README for usage):

```bash
dotnet add package Neox.Aspire.EntityFrameworkCore.MigrationWorker
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
dotnet add package Neox.Azure.Provisioning.Graph
dotnet add package Neox.Keycloak.Provisioning.Realm
dotnet add package Neox.Aspire.Hosting.Azure.EntraId
dotnet add package Neox.Aspire.Hosting.Keycloak
dotnet add package Neox.Aspire.Hosting.Keycloak.EntraId
```

| Package | Usage |
|---------|-------|
| [MigrationWorker](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/README.md) | `AddEfCoreMigrationService<TDbContext>()` |
| [CustomDomains](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/README.md) | `AddDomainOpsProvider` + `WithAzureCustomDomainOps` |
| [Azure.Provisioning.Graph](src/provisioning/Neox.Azure.Provisioning.Graph/README.md) | `GraphApplication` / `GraphServicePrincipal` (`Azure.Provisioning` constructs from msgraph-bicep-types) |
| [Keycloak.Provisioning.Realm](src/provisioning/Neox.Keycloak.Provisioning.Realm/README.md) | `RealmRepresentation` / `ClientRepresentation` (Keycloak realm import JSON POCOs from Admin REST OpenAPI) |
| [Azure.EntraId](src/hosting/Neox.Aspire.Hosting.Azure.EntraId/README.md) | `AddAzureAppRegistration` + `WithSupportedAccountType` / `AddScope` / `AddAppRole` / `AddWebApplication` / `AddSpaApplication` / `WithPermission` / `AddCertificate` / `WithKeyCredential` / `WithSecret` / `EntraIdInstance` / `WithMicrosoftIdentityWebApplication` / `WithEntraIdSpaApplication` (`AzureProvisioningResource` + Graph CDK) |
| [Keycloak](src/hosting/Neox.Aspire.Hosting.Keycloak/README.md) | `AddRealm` + `AddJwtClient` / `AddOidcClient` / `AddIdentityProvider` + `WithLocalRedirectUri` / `WithRedirectUrl` + `WithKeycloakJwtBearer` / `WithKeycloakSpa` on upstream `AddKeycloak` |
| [Keycloak.EntraId](src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/README.md) | `AddEntraIdIdentityProvider` — Entra app registration as Keycloak OIDC identity provider |

> [!NOTE]
> .NET SDK **10.0.110** is pinned in [`global.json`](global.json). Arcade installs a local copy via `eng/common` when needed.

## Specs

See [`specs/README.md`](specs/README.md). Terminology: [`domain-glossary`](specs/features/domain-glossary.md). Repository identity: [`aspire-bootstrap`](specs/features/aspire-bootstrap.md). Build: [`arcade-bootstrap`](specs/features/arcade-bootstrap.md).

## Agent-kit status

| Area | Status |
|------|--------|
| `neox-rules.json` specs paths | Present ([`.cursor/rules/neox-rules.json`](.cursor/rules/neox-rules.json)) |
| Domain glossary | Defined ([`domain-glossary`](specs/features/domain-glossary.md)) |
| Aspire bootstrap | Defined ([`aspire-bootstrap`](specs/features/aspire-bootstrap.md)) |
| Arcade clone-and-build | Implemented ([`arcade-bootstrap`](specs/features/arcade-bootstrap.md)) — .NET 10; `Neox.Aspire.slnx`; MIT |
| Sample AppHosts | Harnesses under `tests/` ([`.cursor/rules/aspire-apphost.mdc`](.cursor/rules/aspire-apphost.mdc)) |
| GitHub Actions | Manual release workflows ([`gitflow-ci`](specs/features/gitflow-ci.md), [`nuget-org`](specs/features/nuget-org.md)) |
| NuGet packages | Shipping libraries in `src/`; local Arcade pack + GHA publish |
| Azure Entra ID hosting | Implemented ([`azure-entra-id`](specs/features/azure-entra-id.md)) — `AddAzureAppRegistration` + `AddScope` + `AddAppRole` + `AddWebApplication` + `AddSpaApplication` + `WithPermission` + `AddCertificate` / `WithKeyCredential` + `WithSecret` + `EntraIdInstance` + `WithMicrosoftIdentityWebApplication` + `WithEntraIdSpaApplication` |
| Keycloak hosting | Implemented ([`keycloak-hosting`](specs/features/keycloak-hosting.md)) — `AddRealm` + `WithOrganizations` / `WithOrganization` / `AddJwtClient` / `AddOidcClient` / `AddIdentityProvider` + `WithLocalRedirectUri` / `WithRedirectUrl` + `WithKeycloakJwtBearer` / `WithKeycloakSpa` on upstream `AddKeycloak`; harness runs Keycloak with realm import, JWT API, and SPA stub |
| Keycloak Entra ID hosting | Implemented ([`keycloak-entraid`](specs/features/keycloak-entraid.md)) — bridge `AddEntraIdIdentityProvider` (Entra app registration → Keycloak OIDC IdP) |
| Azure.Provisioning Graph | Implemented ([`azure-provisioning-graph`](specs/features/azure-provisioning-graph.md)) — source-generated Graph Bicep constructs |
| Keycloak realm provisioning | Implemented ([`keycloak-provisioning-realm`](specs/features/keycloak-provisioning-realm.md)) — source-generated realm POCOs from Keycloak OpenAPI 26.2.5 |

## Useful links

- [Aspire documentation](https://aspire.dev/docs/)
- [microsoft/aspire](https://github.com/microsoft/aspire)
- [Feature specs](specs/README.md)
- [Domain glossary](specs/features/domain-glossary.md)

## What is in this repo?

Packable libraries live under `src/`. Hosting packages use the `Neox.Aspire.Hosting.*` namespace under `src/hosting/`.

| Package | Role |
|---------|------|
| [`Neox.Aspire.EntityFrameworkCore.MigrationWorker`](src/Neox.Aspire.EntityFrameworkCore.MigrationWorker) | One-shot EF Core migration `BackgroundService` via `AddEfCoreMigrationService<TDbContext>()` |
| [`Neox.Aspire.Hosting.Azure.CustomDomains`](src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains) | ACA custom domain ops (`WithAzureCustomDomainOps`, OctoDNS / managed certs via `aspire do`) |
| [`Neox.Azure.Provisioning.Graph`](src/provisioning/Neox.Azure.Provisioning.Graph) | Graph Bicep `Azure.Provisioning` constructs (Shipping; source-generated from msgraph-bicep-types) |
| [`Neox.Keycloak.Provisioning.Realm`](src/provisioning/Neox.Keycloak.Provisioning.Realm) | Keycloak realm import JSON POCOs (Shipping; source-generated from Keycloak Admin REST OpenAPI) |
| [`Neox.Aspire.Hosting.Azure.EntraId`](src/hosting/Neox.Aspire.Hosting.Azure.EntraId) | Entra ID `AddAzureAppRegistration` + `AddWebApplication` + `AddSpaApplication` + `EntraIdInstance` + `WithMicrosoftIdentityWebApplication` + `WithEntraIdSpaApplication` + `WithPermission` + `AddCertificate` / `WithKeyCredential` + `WithSecret` (`AzureProvisioningResource`; Shipping) |
| [`Neox.Aspire.Hosting.Keycloak`](src/hosting/Neox.Aspire.Hosting.Keycloak) | Keycloak `AddRealm` + `AddJwtClient` / `AddOidcClient` / `AddIdentityProvider` + `WithKeycloakJwtBearer` / `WithKeycloakSpa` (realm import + API/SPA env projection; Shipping) |
| [`Neox.Aspire.Hosting.Keycloak.EntraId`](src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId) | Bridge `AddEntraIdIdentityProvider` (Entra app registration → Keycloak OIDC IdP; Shipping) |

Tests: [`tests/efcore-migration-worker/`](tests/efcore-migration-worker/) (Aspire harnesses, Docker), [`tests/azure-custom-domains/`](tests/azure-custom-domains/) (unit + sample AppHost), [`tests/azure-provisioning-graph/`](tests/azure-provisioning-graph/) (Graph CDK unit tests), [`tests/keycloak-provisioning/`](tests/keycloak-provisioning/) (Keycloak realm CDK unit tests), [`tests/azure-entraid/`](tests/azure-entraid/) (unit tests + sample AppHost Auth group + stub API/SPA), [`tests/keycloak/`](tests/keycloak/) (unit tests + sample AppHost with upstream Keycloak container + JWT API/SPA), and [`tests/keycloak-entraid/`](tests/keycloak-entraid/) (Keycloak Entra IdP bridge unit tests). Specs: [`specs/`](specs/README.md).

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

### Git flow / release Actions

Branch convention: `feature/*` → `develop`; `release/*` / `hotfix/*` → `main`; `main` holds shipped releases. Spec: [`gitflow-ci`](specs/features/gitflow-ci.md).

| Trigger | Workflow | Effect |
|---------|----------|--------|
| `workflow_dispatch` on `develop` | [`.github/workflows/release-start.yml`](.github/workflows/release-start.yml) | Bump from latest `v*` (`major` / `minor` / `patch` / `preview`); update [`eng/Versions.props`](eng/Versions.props); push `release/<version>` + PR → `main` |
| `workflow_dispatch` on `release/*` | [`.github/workflows/release-private-publish.yml`](.github/workflows/release-private-publish.yml) | Arcade build/test/pack (`daily` + `OfficialBuildId`) → private GitHub Packages |
| `workflow_dispatch` on `release/*` | [`.github/workflows/release-finalize.yml`](.github/workflows/release-finalize.yml) | Squash-merge PR → `main`, tag `v*`, GitHub Release, Arcade build/test/pack → nuget.org (Trusted Publishing), sync PR `main` → `develop` |

Git automation uses org App **`neox-gitflow`** (`NEOX_GITFLOW_APP_ID` / `NEOX_GITFLOW_APP_PRIVATE_KEY`). nuget.org Trusted Publishing needs repo secret `NUGET_USER` (profile name) and a nuget.org policy for workflow file `release-finalize.yml`. Prefer squash-merge (linear history).

Versioning follows Arcade (`VersionPrefix` / `StabilizePackageVersion` in [`eng/Versions.props`](eng/Versions.props)). Spec: [`nuget-org`](specs/features/nuget-org.md).

## License

The code in this repo is licensed under the [MIT](LICENSE) license.
