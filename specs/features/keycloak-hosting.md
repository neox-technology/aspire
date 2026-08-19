# Keycloak hosting

| Field | Value |
|-------|-------|
| Slug | `keycloak-hosting` |
| Status | implemented |
| Last code review | 2026-08-19 |

## Summary

Shipping hosting package `Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/) with **`AddRealm`** on the Keycloak container resource: optional `RealmRepresentation` callback, JSON file generation under `.aspire/keycloak-realms/{keycloakName}/`, and a single upstream `WithRealmImport` mount per container. Post-`AddRealm` builders **`WithOrganizations`** and **`WithOrganization(name, domain)`** mutate the realm JSON idempotently (organizations feature flag + org/domain entries).

`KeycloakRealmResource` is a child resource of `KeycloakResource` (realm name + import directory). **`AddJwtClient`** on a realm registers a confidential JWT client in realm JSON (including an **`oidc-audience-mapper`** for JWT validation) and creates a child **`KeycloakJwtClientResource`**. **`WithRedirectUrl`** on a JWT client accumulates redirect URIs and web origins for OAuth2 authorization code flow (Swagger). **`WithKeycloakJwtBearer`** projects `Keycloak__*` environment variables (AuthServerUrl, Realm, Authority, ClientId, Audience, ClientSecret, ServiceName) from the JWT client and Keycloak HTTP endpoint, and `WaitFor`s Keycloak and the realm. **`WithKeycloakSpa`** remains a stub. The sample harness under `tests/keycloak/` uses upstream `AddKeycloak` for the server, Neox `AddRealm` / `AddJwtClient` / `WithRedirectUrl` for realm import, and `Aspire.Keycloak.Authentication` in the sample API.

## User scenarios

1. **AppHost author declares a realm on Keycloak** — `AddKeycloak(...).AddRealm(configure, realm)` writes `{realm}-realm.json` and registers a child `KeycloakRealmResource`.
2. **AppHost author configures realm JSON** — optional callback mutates `RealmRepresentation` from `Neox.Keycloak.Provisioning.Realm` before serialization.
3. **Multiple realms on one container** — repeated `AddRealm` calls add multiple JSON files; `WithRealmImport` is applied once via internal annotation.
4. **Contributor validates behavior locally** — xUnit tests assert JSON output, child resource wiring, organization builders, JWT client builders, and env projection; the harness AppHost runs Keycloak with a sample realm and JWT-protected API.
5. **AppHost author declares organizations** — `AddRealm(...).WithOrganizations()` enables organizations; `WithOrganization(name, domain)` adds an org and domain idempotently via read-modify-write of `{realm}-realm.json`.
6. **AppHost author wires API JWT auth** — `AddRealm(...).AddJwtClient(name, clientId, clientSecret).WithRedirectUrl(uri)` then `AddProject(...).WithReference(keycloak).WithKeycloakJwtBearer(jwtClient)`; API consumes `Keycloak__*` via `Aspire.Keycloak.Authentication`.
7. **AppHost author registers Swagger OAuth redirect** — `WithRedirectUrl(Uri)` on a JWT client writes `redirectUris` / `webOrigins` and enables standard flow in realm JSON.

## Business rules

1. **One Shipping package** — `Neox.Aspire.Hosting.Keycloak` is packable and targets `net10.0`.
2. **Child resources** — `KeycloakRealmResource` implements `IResourceWithParent<KeycloakResource>`, is excluded from manifest, and is named `{keycloakName}-{realm}`. `KeycloakJwtClientResource` implements `IResourceWithParent<KeycloakRealmResource>` and is excluded from manifest.
3. **Import directory** — `.aspire/keycloak-realms/{keycloakName}/` resolved relative to AppHost directory; gitignored locally.
4. **Realm parameter wins** — callback may set `Realm`; the `realm` parameter is forced after the callback.
5. **Single import mount** — `KeycloakRealmImportAnnotation` ensures `WithRealmImport` runs once per `KeycloakResource`.
6. **JWT env projection** — `WithKeycloakJwtBearer` resolves AuthServerUrl from the Keycloak container HTTP endpoint; Authority is `{AuthServerUrl}/realms/{Realm}`; Audience defaults to ClientId when not specified on `AddJwtClient`.
7. **JWT client auth defaults** — `AddJwtClient` / `WithRedirectUrl` ensure `protocol: openid-connect` and an `oidc-audience-mapper` (`included.client.audience` = audience) so access tokens validate against `Keycloak__Audience`.
8. **Redirect URLs** — `WithRedirectUrl` accumulates redirect URIs idempotently, derives matching `webOrigins`, and sets `standardFlowEnabled` when at least one redirect URL is present.
9. **SPA stub** — `WithKeycloakSpa` performs null-checks only (no env vars yet).
10. **Harness isolation** — sample AppHost projects under `tests/keycloak/` are harness-only and not product runtimes.
11. **Upstream vs Neox** — container lifecycle and `WithRealmImport` delegation use upstream `Aspire.Hosting.Keycloak`; Neox owns realm JSON generation and the child resource model.
12. **Post-AddRealm mutation** — `WithOrganizations` / `WithOrganization` / `AddJwtClient` / `WithRedirectUrl` re-read, mutate, and rewrite `{realm}-realm.json`; `ConcurrentBagExtensions.GetOrAdd` prevents duplicate org/domain/client/mapper entries.

## Dependencies

- [`domain-glossary`](domain-glossary.md)
- [`aspire-bootstrap`](aspire-bootstrap.md)
- [`keycloak-provisioning-realm`](keycloak-provisioning-realm.md) — `RealmRepresentation` and `KeycloakRealmJsonOptions` for JSON serialization
- Upstream integration: [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/) — packages `Aspire.Hosting.Keycloak` (hosting) and `Aspire.Keycloak.Authentication` (client JWT/OIDC handlers; sample API uses client package directly)

## Out of scope

- Token validation policy implementation details in consumers (beyond sample harness)
- `WithKeycloakSpa` env projection
- Realm import in production / `aspire publish` (upstream limitation)
- Advanced client flows (service accounts, UMA, federation)
- MSAL/Entra-specific behavior and contracts

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Keycloak` exists under `src/hosting/Neox.Aspire.Hosting.Keycloak/`.
- [x] Public API exposes `AddRealm` on `IResourceBuilder<KeycloakResource>`, `WithOrganizations` / `WithOrganization` / `AddJwtClient` / `WithRedirectUrl` on JWT client resources, `WithKeycloakJwtBearer` on project/container resources, plus stub `WithKeycloakSpa`.
- [x] `AddRealm` writes `{realm}-realm.json`, registers child `KeycloakRealmResource`, and calls upstream `WithRealmImport` once per container.
- [x] `AddJwtClient` writes client entry to realm JSON and registers child `KeycloakJwtClientResource`.
- [x] `WithKeycloakJwtBearer` projects `Keycloak__*` env vars and `WaitFor`s Keycloak and realm.
- [x] Tests exist under `tests/keycloak/` with xUnit assertions for JSON, child resources, organization builders, JWT client, and env projection.
- [x] Sample AppHost/API/SPA projects are registered in `Neox.Aspire.slnx`; sample API uses `Aspire.Keycloak.Authentication`.
- [x] Upstream Aspire Keycloak documentation is indexed in this spec (Dependencies + Summary).

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/hosting/Neox.Aspire.Hosting.Keycloak/` |
| Package id | `Neox.Aspire.Hosting.Keycloak` |
| Namespace | `Neox.Aspire.Hosting.Keycloak` |
| Core types | `KeycloakRealmResource`, `KeycloakJwtClientResource`, `KeycloakRealmImportAnnotation`, `KeycloakHostingExtensions` |
| Provisioning dependency | `Neox.Keycloak.Provisioning.Realm` (ProjectReference) |
| Upstream package | `Aspire.Hosting.Keycloak` $(AspireHostingKeycloakVersion) (preview; no stable $(AspireVersion) on nuget.org) |
| Client package (harness) | `Aspire.Keycloak.Authentication` $(AspireKeycloakAuthenticationVersion) |
| Upstream doc | https://aspire.dev/fr/integrations/security/keycloak/ |
| Unit tests | `tests/keycloak/Neox.Aspire.Hosting.Keycloak.Tests.csproj` |
| Sample AppHost | `tests/keycloak/aspire/apphost/` |
| Sample API | `tests/keycloak/src/apis/` |
| Sample SPA | `tests/keycloak/src/spa/` |
