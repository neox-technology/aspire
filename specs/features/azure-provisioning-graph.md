# Azure.Provisioning Graph constructs

| Field | Value |
|-------|-------|
| Slug | `azure-provisioning-graph` |
| Status | implemented |
| Last code review | 2026-08-16 |

## Summary

Shipping CDK package **`Neox.Azure.Provisioning.Graph`** under `src/provisioning/Neox.Azure.Provisioning.Graph/` exposes Microsoft Graph Bicep resources as `Azure.Provisioning` constructs (`ProvisionableResource` / `ProvisionableConstruct`). Types are **source-generated** at build time from a checked-in snapshot of the official [msgraph-bicep-types](https://github.com/microsoftgraph/msgraph-bicep-types) `types.json` (Graph **v1.0**, types version **1.0.0** — the same pin as Entra hosting `bicepconfig.json`). The generator project is not packable. Builds do not fetch GitHub; refreshing the snapshot is a manual tool run.

This package is not an Aspire hosting library. [`azure-entra-id`](azure-entra-id.md) consumes it from `AddAzureAppRegistration` / `ConfigureInfrastructure`.

## User scenarios

1. **Consumer packs locally** — Arcade pack emits `Neox.Azure.Provisioning.Graph` under Shipping; the generator project is not packed.
2. **AppHost / CDK author uses Graph constructs** — `new GraphApplication("app")` / `GraphServicePrincipal` / nested `GraphWebApplication` compile to `Microsoft.Graph/*@v1.0` Bicep (no ARM `name` unless the JSON defines `name`, e.g. federated identity credentials).
3. **AppHost author references an existing Graph resource** — `GraphApplication.FromExisting("app", uniqueName)` sets the JSON **Identifier** property before `IsExistingResource` so `Compile()` emits `existing` + `uniqueName:` (not ARM `name:`).
4. **Maintainer refreshes types** — `tools/msgraph-bicep-types-catalog` rewrites the checked-in `types.json` / `index.json` from the official repo; CI stays offline-deterministic.
5. **Contributor runs unit tests** — in-process compile of generated types; no live Azure or Graph.

## Business rules

1. **One Shipping Graph CDK package** — packable id `Neox.Azure.Provisioning.Graph` (`net10.0`, `RootNamespace=Neox.Azure.Provisioning.Graph`). Generator `Neox.Azure.Provisioning.Graph.Generators.Internal` is `IsPackable=false`, `netstandard2.0`, referenced as an analyzer.
2. **Official JSON is the source of truth** — snapshot `generated/types.json` (+ `index.json` for the pin) from `microsoftgraph/msgraph-bicep-types` path `generated/microsoftgraph/microsoft.graph/v1.0/1.0.0/`. Full v1.0 surface (applications, servicePrincipals, groups, users, federatedIdentityCredentials, oauth2PermissionGrants, appRoleAssignedTo, and nested ObjectTypes).
3. **C# naming** — `MicrosoftGraph*` → strip prefix then `Graph` prefix (`MicrosoftGraphWebApplication` → `GraphWebApplication`). Resources: `Microsoft.Graph/applications` → `GraphApplication`, `servicePrincipals` → `GraphServicePrincipal`, `groups` → `GraphGroup`, `users` → `GraphUser`, `oauth2PermissionGrants` → `GraphOauth2PermissionGrant`, `appRoleAssignedTo` → `GraphAppRoleAssignedTo`, `applications/federatedIdentityCredentials` → `GraphFederatedIdentityCredential`.
4. **Identifier, not ARM name** — JSON flag `Identifier` drives `FromExisting(bicepIdentifier, BicepValue<string> identifier)` assigned **before** `IsExistingResource`. Applications/groups → `UniqueName`; service principals → `AppId`; users (read-only) → `UserPrincipalName`. Do not emit ARM `name` unless the JSON property is actually `name`.
5. **Skipped JSON members** — read-only `type` and `apiVersion` (DeployTimeConstant **resource** metadata); nested ObjectType fields named `type` (for example `GraphPermissionScope.Type` = Admin/User) are generated. `any` (e.g. `customSecurityAttributes`) is skipped. String unions → `BicepValue<string>`. Unassigned properties are omitted by `Compile()`.
6. **Offline builds** — AdditionalFiles catalogue; no network during `dotnet build`. Refresh tool may download from GitHub when a maintainer runs it.
7. **Tests** — xUnit under `tests/azure-provisioning-graph/`; no live ARM / Graph.

## Dependencies

- Arcade pack ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md)), repo identity ([`aspire-bootstrap`](aspire-bootstrap.md))
- Consumer hosting ([`azure-entra-id`](azure-entra-id.md))
- `Azure.Provisioning` (`ProvisionableResource`, `Compile()`)
- Official types: [microsoftgraph/msgraph-bicep-types](https://github.com/microsoftgraph/msgraph-bicep-types) v1.0 / 1.0.0

## Out of scope

- Microsoft Graph **beta** types
- Azure SDK TypeSpec provisioning generator
- Aspire hosting APIs (`AddAzureAppRegistration` — owned by [`azure-entra-id`](azure-entra-id.md))
- Graph SDK hosting / `microsoft-graph-permissions.json` catalogue (not this CDK)
- Git submodule of msgraph-bicep-types (snapshot + refresh tool only)
- Automatic catalogue refresh in CI

## Acceptance criteria

- [x] Package `Neox.Azure.Provisioning.Graph` under `src/provisioning/Neox.Azure.Provisioning.Graph/` (`IsPackable=true`, `net10.0`).
- [x] Generator `Neox.Azure.Provisioning.Graph.Generators.Internal` (`IsPackable=false`) emits constructs from AdditionalFiles `types.json`.
- [x] Projects listed in `Neox.Aspire.slnx` only.
- [x] `{paths.azureProvisioningGraph}` / `{aspire.packages.azureProvisioningGraph}` in `.cursor/rules/neox-rules.json`.
- [x] Checked-in `types.json` / `index.json` pin Graph v1.0 types **1.0.0**.
- [x] Public types include `GraphApplication`, `GraphServicePrincipal`, `GraphWebApplication`, `GraphGroup`, `GraphUser` (Key Vault–style `FromExisting`; identifier not ARM `name` except FIC `name`).
- [x] `GraphApplication.FromExisting` with uniqueName compiles Bicep `existing` + `uniqueName:` and does not emit `  name:`.
- [x] Refresh tool `tools/msgraph-bicep-types-catalog` can rewrite the snapshot from the official repo.
- [x] xUnit tests under `tests/azure-provisioning-graph/` (no live Azure / Graph).

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/provisioning/Neox.Azure.Provisioning.Graph/` |
| Package id | `Neox.Azure.Provisioning.Graph` |
| Namespace | `Neox.Azure.Provisioning.Graph` |
| Generator | `src/provisioning/Neox.Azure.Provisioning.Graph.Generators.Internal/` |
| SG class | `GraphProvisioningGenerator` |
| Catalogue | `src/provisioning/Neox.Azure.Provisioning.Graph/generated/types.json` |
| Catalogue refresh | `tools/msgraph-bicep-types-catalog/` |
| Types pin | `microsoftgraph/msgraph-bicep-types` `v1.0/1.0.0` |
| Unit tests | `tests/azure-provisioning-graph/` |
