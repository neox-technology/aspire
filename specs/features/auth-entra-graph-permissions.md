# AuthOps Entra — Microsoft Graph well-known permissions

| Field | Value |
|-------|-------|
| Slug | `auth-entra-graph-permissions` |
| Status | implemented |
| Last code review | 2026-08-02 |

## Summary

Extend `Neox.Aspire.Hosting.Auth.EntraId` so AppHosts can bind **first-party Microsoft Graph** delegated scopes and application roles with the same `WithApiPermission` ergonomics used for in-model API expositions:

```csharp
webAuth
  .WithApiPermission(MicrosoftGraph.Delegated.UserRead)
  .WithApiPermission(MicrosoftGraph.Application.UserReadAll);
```

A Roslyn source generator (`Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal`) emits `MicrosoftGraph.Delegated` / `MicrosoftGraph.Application` statics from a checked-in JSON catalogue (`microsoft-graph-permissions.json`). Builds do not call Graph; refreshing the catalogue is a manual tool run (`tools/microsoft-graph-permissions-catalog`).

## User scenarios

- AppHost author wants Graph `User.Read` (delegated) and/or `User.Read.All` (application) on a consumer Auth app without hand-coding permission GUIDs.
- `plan-{app}-auth` / `provision-{app}-auth` include those entries in Graph `requiredResourceAccess` (`ResourceAppId` = Microsoft Graph app id `00000003-0000-0000-c000-000000000000`, type `Scope` or `Role`).
- Well-known Graph permissions create an `AuthApiPermission` dashboard child under the consumer (same as in-model `WithApiPermission`) but do **not** add a `DependsOn` on another Auth app’s provision step (unlike consuming an in-model `ApiExposition`).
- Maintainers refresh the catalogue when Microsoft adds/changes Graph permissions; CI builds remain offline-deterministic from the checked-in JSON.

## Routes (if UI)

None.

## Dependencies

- [`auth-providers`](auth-providers.md) (Entra AuthOps, existing `WithApiPermission` for in-model expositions)
- Terminology ([`domain-glossary`](domain-glossary.md))
- Pattern: DomainOps OctoDNS catalogue + generator ([`azure-custom-domains`](azure-custom-domains.md))

## Out of scope

- Other first-party Microsoft APIs (SharePoint, Exchange, legacy Azure AD Graph)
- Automatic admin consent / consent URL generation
- Resource-specific application permissions (RSAP)
- Modeling Microsoft Graph itself as an Aspire Auth app registration resource
- Automatic catalogue refresh in CI

## Acceptance criteria

- [x] Spec status is `defined` before implementation; moves to `implemented` when ACs below are met in code.
- [x] Package ships generator project `Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal` (`IsPackable=false`, analyzer reference from EntraId).
- [x] Checked-in catalogue `Graph/microsoft-graph-permissions.json` lists Graph delegated scopes and application roles (`id`, `value`, display/description, `isEnabled`); disabled entries are not emitted.
- [x] Generator emits `MicrosoftGraph` with nested `Delegated` and `Application` static `WellKnownApiPermission` properties (C# identifiers sanitized from permission `value`, e.g. `User.Read` → `UserRead`).
- [x] `WellKnownApiPermission` carries `ResourceAppId`, `PermissionId`, `Type` (`Scope`|`Role`), and `Value`.
- [x] `WithApiPermission(WellKnownApiPermission)` records a well-known permission annotation on the consumer Auth app and creates child `{consumer}-apiperm-{value}` (`AuthApiPermission`); existing `WithApiPermission(IResourceBuilder<TApiExposition>)` remains fluent on the consumer app builder.
- [x] `EntraApiPermissionApplicator` merges well-known permissions into desired `requiredResourceAccess` without resolving an exposer ClientId; provision `DependsOn` for exposers remains only for in-model expositions.
- [x] Refresh tool `tools/microsoft-graph-permissions-catalog` can rewrite the JSON from the Microsoft Graph service principal (and/or an offline docs bootstrap).
- [x] Unit tests cover catalogue parse/codegen and applicator collection for well-known permissions; sample AppHost binds at least one Graph permission. No live Graph in CI.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Shipping package | `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` |
| Generator | `src/hosting/Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal/` |
| Catalogue | `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/Graph/microsoft-graph-permissions.json` |
| Catalogue refresh | `tools/microsoft-graph-permissions-catalog/` |
| Graph app id | `00000003-0000-0000-c000-000000000000` |
| Unit tests | `tests/auth-providers/tests/` |
| Sample AppHost | `tests/auth-providers/sample-apphost/` |
