# Keycloak hosting

| Field | Value |
|-------|-------|
| Slug | `keycloak-hosting` |
| Status | implemented |
| Last code review | 2026-08-19 |

## Summary

Shipping hosting package `Neox.Aspire.Hosting.Keycloak` provides a lightweight Aspire model resource and extensions to inject Keycloak configuration into API and SPA resources from AppHost composition.

The official Aspire integration ([Keycloak integration](https://aspire.dev/fr/integrations/security/keycloak/)) owns the Keycloak **container** (`AddKeycloak`), connection strings, realm import, and client auth handlers. Neox owns **env projection** from a `KeycloakRealmResource` model (`AddKeycloakRealm`, `WithKeycloakWebApi`, `WithKeycloakSpa`). The sample harness under `tests/keycloak/` uses upstream `Aspire.Hosting.Keycloak` for the server and Neox helpers for consumer wiring (realm wiring is phased in separately).

## User scenarios

1. **AppHost author models Keycloak realm settings** — `AddKeycloakRealm(name, realm, authServerUrl, clientId, clientSecret)` creates a reusable resource.
2. **AppHost author wires API auth config** — `.WithKeycloakWebApi(realm)` injects `Keycloak__*` values for web API authentication setup.
3. **AppHost author wires SPA auth config** — `.WithKeycloakSpa(realm)` injects `KEYCLOAK_*` values for front-end runtime auth configuration.
4. **Contributor validates behavior locally** — xUnit tests and sample AppHost/API/SPA under `tests/keycloak/` verify env wiring and project integration; the harness AppHost runs a Keycloak container via upstream `AddKeycloak`.

## Business rules

1. **One Shipping package** — `Neox.Aspire.Hosting.Keycloak` is packable and targets `net10.0`.
2. **Model-only resource** — `KeycloakRealmResource` is excluded from manifest and used for composition/env projection.
3. **API env convention** — `WithKeycloakWebApi` maps `AuthServerUrl`, `Realm`, `Authority`, `ClientId`, `Audience`, and optional `ClientSecret` under `Keycloak__`.
4. **SPA env convention** — `WithKeycloakSpa` maps `URL`, `REALM`, and `CLIENT_ID` under `KEYCLOAK_`.
5. **Harness isolation** — sample AppHost projects under `tests/keycloak/` are harness-only and not product runtimes.
6. **Upstream vs Neox** — running a Keycloak container is delegated to upstream `Aspire.Hosting.Keycloak` (`AddKeycloak`); Neox does not reimplement container lifecycle.

## Dependencies

- [`domain-glossary`](domain-glossary.md)
- [`aspire-bootstrap`](aspire-bootstrap.md)
- [`azure-entra-id`](azure-entra-id.md)
- Upstream integration: [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/) — packages `Aspire.Hosting.Keycloak` (hosting) and `Aspire.Keycloak.Authentication` (client JWT/OIDC handlers; not consumed by Neox hosting yet)

## Out of scope

- Realm import (`WithRealmImport`) and production realm seeding in the Neox package
- Token validation policy implementation details in consumers
- Advanced client flows (service accounts, UMA, federation)
- MSAL/Entra-specific behavior and contracts
- Client package `Aspire.Keycloak.Authentication` wiring in Neox hosting (consumers may add it directly)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Keycloak` exists under `src/hosting/Neox.Aspire.Hosting.Keycloak/`.
- [x] Public API exposes `AddKeycloakRealm`, `WithKeycloakWebApi`, and `WithKeycloakSpa`.
- [x] `WithKeycloakWebApi` and `WithKeycloakSpa` inject expected environment keys and `WaitFor` the Keycloak resource.
- [x] Tests exist under `tests/keycloak/` with xUnit assertions for env mapping.
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
| Core types | `KeycloakRealmResource`, `KeycloakHostingExtensions` |
| Upstream package | `Aspire.Hosting.Keycloak` $(AspireHostingKeycloakVersion) (preview; no stable $(AspireVersion) on nuget.org) |
| Upstream doc | https://aspire.dev/fr/integrations/security/keycloak/ |
| Unit tests | `tests/keycloak/Neox.Aspire.Hosting.Keycloak.Tests.csproj` |
| Sample AppHost | `tests/keycloak/aspire/apphost/` |
| Sample API | `tests/keycloak/src/apis/` |
| Sample SPA | `tests/keycloak/src/spa/` |
