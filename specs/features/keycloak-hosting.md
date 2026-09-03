# Keycloak hosting

| Field | Value |
|-------|-------|
| Slug | `keycloak-hosting` |
| Status | implemented |
| Last code review | 2026-08-20 |

## Summary

Shipping hosting package `Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/) with **`AddRealm`** on the Keycloak container resource: optional `RealmRepresentation` callback, JSON file generation under `.aspire/keycloak-realms/{keycloakName}/`, and a single upstream `WithRealmImport` mount per container. Post-`AddRealm` builders **`WithOrganizations`** and **`WithOrganization(name, domain)`** mutate the realm JSON idempotently (organizations feature flag + org/domain entries). **`AddIdentityProvider(alias, providerId, configure?)`** registers a **Keycloak identity provider** in realm JSON (`IdentityProviderRepresentation`) and a child **`KeycloakIdentityProviderResource`**.

`KeycloakRealmResource` is a child resource of `KeycloakResource` (realm name + import directory). **`AddJwtClient`** on a realm registers a confidential JWT client in realm JSON (including an **`oidc-audience-mapper`** for JWT validation) and creates a child **`KeycloakJwtClientResource`**. **`AddOidcClient`** registers a public OIDC/SPA client in realm JSON and creates a child **`KeycloakOidcClientResource`**. **`WithRedirectUrl`** and **`WithLocalRedirectUri`** on JWT or OIDC clients accumulate redirect URIs and web origins. **`WithLocalRedirectUri`** (Development only) registers HTTP loopback redirect URIs without port and `webOrigins: *` for Aspire ephemeral ports. **`WithKeycloakJwtBearer`** projects `Keycloak__*` environment variables from the Keycloak HTTPS endpoint (fallback HTTP) and `WaitFor`s Keycloak and the realm. **`WithKeycloakSpa`** projects `KEYCLOAK_URL`, `KEYCLOAK_REALM`, and `KEYCLOAK_CLIENT_ID` for Vite/SPA consumers and `WaitFor`s Keycloak and the realm. The sample harness under `tests/keycloak/` uses upstream `AddKeycloak` for the server, Neox `AddRealm` / `AddJwtClient` / `AddOidcClient` / `WithLocalRedirectUri` for realm import, `Aspire.Keycloak.Authentication` in the sample API with an explicit `Keycloak:Authority` override, and a Vite SPA stub wired via `WithKeycloakSpa`. Entra-specific IdP wiring lives in [`keycloak-entraid`](keycloak-entraid.md).

## User scenarios

1. **AppHost author declares a realm on Keycloak** — `AddKeycloak(...).AddRealm(configure, realm)` writes `{realm}-realm.json` and registers a child `KeycloakRealmResource`.
2. **AppHost author configures realm JSON** — optional callback mutates `RealmRepresentation` from `Neox.Keycloak.Provisioning.Realm` before serialization.
3. **Multiple realms on one container** — repeated `AddRealm` calls add multiple JSON files; `WithRealmImport` is applied once via internal annotation.
4. **Contributor validates behavior locally** — xUnit tests assert JSON output, child resource wiring, organization builders, JWT/OIDC client builders, and env projection; the harness AppHost runs Keycloak with a sample realm, JWT-protected API, and SPA stub.
5. **AppHost author declares organizations** — `AddRealm(...).WithOrganizations()` enables organizations; `WithOrganization(name, domain)` adds an org and domain idempotently via read-modify-write of `{realm}-realm.json`.
6. **AppHost author wires API JWT auth** — `AddRealm(...).AddJwtClient(...).WithLocalRedirectUri(path)` (dev) or `WithRedirectUrl(uri)` (fixed URLs) then `AddProject(...).WithReference(keycloak).WithKeycloakJwtBearer(jwtClient)`; API sets `options.Authority = Keycloak:Authority` when calling `AddKeycloakJwtBearer`.
7. **AppHost author registers Swagger OAuth redirect** — `WithLocalRedirectUri` in Development or `WithRedirectUrl(Uri)` for exact redirect URIs; both enable standard flow in realm JSON.
8. **AppHost author wires SPA OIDC client** — `AddRealm(...).AddOidcClient(...).WithLocalRedirectUri("/")` (dev) or `WithRedirectUrl(uri)` then `AddViteApp(...).WithReference(keycloak).WithKeycloakSpa(oidcClient)`; SPA reads `KEYCLOAK_*` env vars (Vite `envPrefix`).
9. **AppHost author adds an identity provider** — `AddRealm(...).AddIdentityProvider(alias, providerId, configure?)` upserts `identityProviders` in realm JSON by alias and registers a child **Keycloak identity provider** resource.

## Business rules

1. **One Shipping package** — `Neox.Aspire.Hosting.Keycloak` is packable and targets `net10.0`.
2. **Child resources** — `KeycloakRealmResource` implements `IResourceWithParent<KeycloakResource>`, is excluded from manifest, and is named `{keycloakName}-{realm}`. `KeycloakJwtClientResource`, `KeycloakOidcClientResource`, and `KeycloakIdentityProviderResource` implement `IResourceWithParent<KeycloakRealmResource>` and are excluded from manifest.
3. **Import directory** — `.aspire/keycloak-realms/{keycloakName}/` resolved relative to AppHost directory; gitignored locally.
4. **Realm parameter wins** — callback may set `Realm`; the `realm` parameter is forced after the callback.
5. **Single import mount** — `KeycloakRealmImportAnnotation` ensures `WithRealmImport` runs once per `KeycloakResource`.
6. **JWT env projection** — `WithKeycloakJwtBearer` resolves `AuthServerUrl` from the Keycloak container **HTTPS** endpoint (fallback HTTP); `Authority` is `{AuthServerUrl}/realms/{Realm}`; Audience defaults to ClientId when not specified on `AddJwtClient`.
7. **JWT client auth defaults** — `AddJwtClient` / redirect builders ensure `protocol: openid-connect` and an `oidc-audience-mapper` (`included.client.audience` = audience) so access tokens validate against `Keycloak__Audience`.
8. **OIDC client defaults** — `AddOidcClient` writes a public client (`publicClient: true`, `protocol: openid-connect`); no client secret or audience mapper. PKCE is handled by Keycloak defaults for public clients.
9. **Redirect URLs** — `WithRedirectUrl` on JWT or OIDC clients accumulates redirect URIs idempotently and derives matching `webOrigins` only (no dev wildcard). `WithLocalRedirectUri` (Development only) adds HTTP loopback URIs without port and `webOrigins: *`. Both set `standardFlowEnabled` when at least one redirect URL is present.
10. **SPA env projection** — `WithKeycloakSpa` resolves `KEYCLOAK_URL` from the Keycloak **HTTPS** endpoint (fallback HTTP), `KEYCLOAK_REALM` from the OIDC client's parent realm, and `KEYCLOAK_CLIENT_ID` from the OIDC client.
11. **Consumer Authority contract** — `Aspire.Keycloak.Authentication.AddKeycloakJwtBearer` defaults to `https+http://{serviceName}/realms/{realm}`; consumers must set `options.Authority` from `Keycloak:Authority` projected by `WithKeycloakJwtBearer` so JWT `iss` matches Keycloak.
12. **Harness isolation** — sample AppHost projects under `tests/keycloak/` are harness-only and not product runtimes.
13. **Upstream vs Neox** — container lifecycle and `WithRealmImport` delegation use upstream `Aspire.Hosting.Keycloak`; Neox owns realm JSON generation and the child resource model.
14. **Post-AddRealm mutation** — organization / JWT / OIDC client / identity provider / redirect builders re-read, mutate, and rewrite `{realm}-realm.json`; `ConcurrentBagExtensions.GetOrAdd` prevents duplicate entries.
15. **Identity providers** — `AddIdentityProvider(alias, providerId, configure?)` returns `IResourceBuilder<KeycloakIdentityProviderResource>`. Defaults: `Enabled = true`, `Alias`, `ProviderId`. Upserts by `Alias`. Optional `configure` mutates `IdentityProviderRepresentation` (including `Config`). Aspire resource name is `{realmResource.Name}-idp-{alias}`.

## Dependencies

- [`domain-glossary`](domain-glossary.md)
- [`aspire-bootstrap`](aspire-bootstrap.md)
- [`keycloak-provisioning-realm`](keycloak-provisioning-realm.md) — `RealmRepresentation` and `KeycloakRealmJsonOptions` for JSON serialization
- [`keycloak-entraid`](keycloak-entraid.md) — Entra-specific IdP bridge (consumer of `AddIdentityProvider`)
- Upstream integration: [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/) — packages `Aspire.Hosting.Keycloak` (hosting) and `Aspire.Keycloak.Authentication` (client JWT/OIDC handlers; sample API uses client package directly)

## Out of scope

- Dedicated Neox consumer authentication package wrapping `AddKeycloakJwtBearer`
- Realm import in production / `aspire publish` (upstream limitation)
- Advanced client flows (service accounts, UMA) beyond identity-provider JSON builders
- Full Keycloak JS / MSAL integration in the sample SPA (ConnectionStub only)
- MSAL/Entra-specific behavior and contracts (owned by [`keycloak-entraid`](keycloak-entraid.md))
- Identity provider mappers catalogue UI; SAML-only helpers

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Keycloak` exists under `src/hosting/Neox.Aspire.Hosting.Keycloak/`.
- [x] Public API exposes `AddRealm`, `WithOrganizations` / `WithOrganization`, `AddJwtClient`, `AddOidcClient`, `WithRedirectUrl`, `WithLocalRedirectUri`, `WithKeycloakJwtBearer`, and `WithKeycloakSpa`.
- [x] Public API exposes `AddIdentityProvider`; registers `KeycloakIdentityProviderResource` and upserts `identityProviders` in realm JSON by alias.
- [x] Tests assert IdP JSON, child resource wiring, and alias idempotence.
- [x] `AddRealm` writes `{realm}-realm.json`, registers child `KeycloakRealmResource`, and calls upstream `WithRealmImport` once per container.
- [x] `AddJwtClient` writes client entry to realm JSON and registers child `KeycloakJwtClientResource`.
- [x] `AddOidcClient` writes public client entry to realm JSON and registers child `KeycloakOidcClientResource`.
- [x] `WithKeycloakJwtBearer` projects `Keycloak__*` env vars (HTTPS AuthServerUrl when available) and `WaitFor`s Keycloak and realm.
- [x] `WithKeycloakSpa` projects `KEYCLOAK_*` env vars and `WaitFor`s Keycloak and realm.
- [x] `WithLocalRedirectUri` writes loopback redirect URIs and `webOrigins: *` in Development only (JWT and OIDC clients).
- [x] Tests exist under `tests/keycloak/` with xUnit assertions for JSON, child resources, organization builders, JWT/OIDC client, redirect builders, and env projection.
- [x] Sample AppHost/API/SPA projects are registered in `Neox.Aspire.slnx`; sample API uses `Aspire.Keycloak.Authentication` with `Keycloak:Authority` override; sample SPA vitest asserts `KEYCLOAK_*` mapping and ConnectionStub `configured` status.
- [x] Upstream Aspire Keycloak documentation is indexed in this spec (Dependencies + Summary).

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/hosting/Neox.Aspire.Hosting.Keycloak/` |
| Package id | `Neox.Aspire.Hosting.Keycloak` |
| Namespace | `Neox.Aspire.Hosting.Keycloak` |
| Core types | `KeycloakRealmResource`, `KeycloakJwtClientResource`, `KeycloakOidcClientResource`, `KeycloakIdentityProviderResource`, `KeycloakRealmImportAnnotation`, `KeycloakHostingExtensions` |
| Provisioning dependency | `Neox.Keycloak.Provisioning.Realm` (ProjectReference) |
| Upstream package | `Aspire.Hosting.Keycloak` $(AspireHostingKeycloakVersion) (preview; no stable $(AspireVersion) on nuget.org) |
| Client package (harness) | `Aspire.Keycloak.Authentication` $(AspireKeycloakAuthenticationVersion) |
| Upstream doc | https://aspire.dev/fr/integrations/security/keycloak/ |
| Unit tests | `tests/keycloak/Neox.Aspire.Hosting.Keycloak.Tests.csproj` |
| Sample AppHost | `tests/keycloak/aspire/apphost/` |
| Sample API | `tests/keycloak/src/apis/` |
| Sample SPA | `tests/keycloak/src/spa/` |
