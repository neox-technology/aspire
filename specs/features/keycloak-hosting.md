# Keycloak hosting

| Field | Value |
|-------|-------|
| Slug | `keycloak-hosting` |
| Status | implemented |
| Last code review | 2026-08-19 |

## Summary

Shipping hosting package `Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/) with **`AddRealm`** on the Keycloak container resource: optional `RealmRepresentation` callback, JSON file generation under `.aspire/keycloak-realms/{keycloakName}/`, and a single upstream `WithRealmImport` mount per container. Post-`AddRealm` builders **`WithOrganizations`** and **`WithOrganization(name, domain)`** mutate the realm JSON idempotently (organizations feature flag + org/domain entries).

`KeycloakRealmResource` is a child resource of `KeycloakResource` (realm name + import directory). `WithKeycloakWebApi` and `WithKeycloakSpa` retain their signatures but are **stubs** until env projection is implemented. The sample harness under `tests/keycloak/` uses upstream `AddKeycloak` for the server and Neox `AddRealm` for realm import.

## User scenarios

1. **AppHost author declares a realm on Keycloak** — `AddKeycloak(...).AddRealm(configure, realm)` writes `{realm}-realm.json` and registers a child `KeycloakRealmResource`.
2. **AppHost author configures realm JSON** — optional callback mutates `RealmRepresentation` from `Neox.Keycloak.Provisioning.Realm` before serialization.
3. **Multiple realms on one container** — repeated `AddRealm` calls add multiple JSON files; `WithRealmImport` is applied once via internal annotation.
4. **Contributor validates behavior locally** — xUnit tests assert JSON output, child resource wiring, organization builders, and stub helpers; the harness AppHost runs Keycloak with a sample `neox` realm.
5. **AppHost author declares organizations** — `AddRealm(...).WithOrganizations()` enables organizations; `WithOrganization(name, domain)` adds an org and domain idempotently via read-modify-write of `{realm}-realm.json`.

## Business rules

1. **One Shipping package** — `Neox.Aspire.Hosting.Keycloak` is packable and targets `net10.0`.
2. **Child resource** — `KeycloakRealmResource` implements `IResourceWithParent<KeycloakResource>`, is excluded from manifest, and is named `{keycloakName}-{realm}`.
3. **Import directory** — `.aspire/keycloak-realms/{keycloakName}/` resolved relative to AppHost directory; gitignored locally.
4. **Realm parameter wins** — callback may set `Realm`; the `realm` parameter is forced after the callback.
5. **Single import mount** — `KeycloakRealmImportAnnotation` ensures `WithRealmImport` runs once per `KeycloakResource`.
6. **Env stubs** — `WithKeycloakWebApi` / `WithKeycloakSpa` perform null-checks only (no env vars yet).
7. **Harness isolation** — sample AppHost projects under `tests/keycloak/` are harness-only and not product runtimes.
8. **Upstream vs Neox** — container lifecycle and `WithRealmImport` delegation use upstream `Aspire.Hosting.Keycloak`; Neox owns realm JSON generation and the child resource model.
9. **Post-AddRealm mutation** — `WithOrganizations` / `WithOrganization` re-read, mutate, and rewrite `{realm}-realm.json`; `ConcurrentBagExtensions.GetOrAdd` prevents duplicate org/domain entries.

## Dependencies

- [`domain-glossary`](domain-glossary.md)
- [`aspire-bootstrap`](aspire-bootstrap.md)
- [`keycloak-provisioning-realm`](keycloak-provisioning-realm.md) — `RealmRepresentation` and `KeycloakRealmJsonOptions` for JSON serialization
- Upstream integration: [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/) — packages `Aspire.Hosting.Keycloak` (hosting) and `Aspire.Keycloak.Authentication` (client JWT/OIDC handlers; not consumed by Neox hosting yet)

## Out of scope

- Token validation policy implementation details in consumers
- `WithKeycloakWebApi` / `WithKeycloakSpa` env projection (stubs only in this delivery)
- Dynamic `AuthServerUrl` resolution from Keycloak endpoints
- Realm import in production / `aspire publish` (upstream limitation)
- Advanced client flows (service accounts, UMA, federation)
- MSAL/Entra-specific behavior and contracts
- Client package `Aspire.Keycloak.Authentication` wiring in Neox hosting (consumers may add it directly)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Keycloak` exists under `src/hosting/Neox.Aspire.Hosting.Keycloak/`.
- [x] Public API exposes `AddRealm` on `IResourceBuilder<KeycloakResource>`, `WithOrganizations` / `WithOrganization` on `IResourceBuilder<KeycloakRealmResource>`, plus stub `WithKeycloakWebApi` and `WithKeycloakSpa`.
- [x] `AddRealm` writes `{realm}-realm.json`, registers child `KeycloakRealmResource`, and calls upstream `WithRealmImport` once per container.
- [x] Tests exist under `tests/keycloak/` with xUnit assertions for JSON, child resource, callback, default realm, organization builders, and WebApi stub.
- [x] Sample AppHost/API/SPA projects are registered in `Neox.Aspire.slnx`.
- [x] Upstream Aspire Keycloak documentation is indexed in this spec (Dependencies + Summary).

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/hosting/Neox.Aspire.Hosting.Keycloak/` |
| Package id | `Neox.Aspire.Hosting.Keycloak` |
| Namespace | `Neox.Aspire.Hosting.Keycloak` |
| Core types | `KeycloakRealmResource`, `KeycloakRealmImportAnnotation`, `KeycloakHostingExtensions` |
| Provisioning dependency | `Neox.Keycloak.Provisioning.Realm` (ProjectReference) |
| Upstream package | `Aspire.Hosting.Keycloak` $(AspireHostingKeycloakVersion) (preview; no stable $(AspireVersion) on nuget.org) |
| Upstream doc | https://aspire.dev/fr/integrations/security/keycloak/ |
| Unit tests | `tests/keycloak/Neox.Aspire.Hosting.Keycloak.Tests.csproj` |
| Sample AppHost | `tests/keycloak/aspire/apphost/` |
| Sample API | `tests/keycloak/src/apis/` |
| Sample SPA | `tests/keycloak/src/spa/` |
