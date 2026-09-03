# Keycloak Entra ID hosting

| Field | Value |
|-------|-------|
| Slug | `keycloak-entraid` |
| Status | implemented |
| Last code review | 2026-08-21 |

## Summary

Shipping bridge package **`Neox.Aspire.Hosting.Keycloak.EntraId`** under `src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/`. Namespace is `Neox.Aspire.Hosting.Keycloak.EntraId`. It references [`keycloak-hosting`](keycloak-hosting.md) and [`azure-entra-id`](azure-entra-id.md) and exposes **`AddEntraIdIdentityProvider`** on a **Keycloak realm resource**: registers a Keycloak OIDC **Keycloak identity provider** child from an **Azure app registration resource** (client id, client secret via **Entra ID password credential resource**, discovery URL from **Entra ID instance** + AppHost `Azure:TenantId`). Client id and secret are written into realm JSON at initialize time by a non-parented **config gate** resource (after `WaitFor` registration + password credentials); the Keycloak container `WaitFor`s that gate (not the IdP child) so `WithRealmImport` sees the final file. Optional `brokerRedirectUri` registers an **Azure Entra ID web application** redirect for the Keycloak broker callback.

## User scenarios

1. **AppHost author wires Entra as a Keycloak IdP** — after `AddRealm` and `AddAzureAppRegistration(...).WithSecret(...)`, calls `realm.AddEntraIdIdentityProvider(EntraIdInstance.Workforce, appRegistration, alias: "microsoft")` and gets a **Keycloak identity provider** child that fills OIDC config when Entra outputs are ready.
2. **AppHost author sets broker redirect on Entra** — passes `brokerRedirectUri` so the bridge `AddWebApplication`s `{alias}-broker` with that AbsoluteUri.
3. **Contributor runs unit tests** — in-process `CreateBuilder` under `tests/keycloak-entraid/` asserts Wait annotations, throw without secret, and IdP stub in realm JSON; no live Azure or Graph.

## Business rules

1. **One Shipping bridge package** — `Neox.Aspire.Hosting.Keycloak.EntraId` is packable (`net10.0`); it does not own generic IdP builders (those live in [`keycloak-hosting`](keycloak-hosting.md)) or Entra Graph provisioning (those live in [`azure-entra-id`](azure-entra-id.md)).
2. **API** — `AddEntraIdIdentityProvider(EntraIdInstance instance, IResourceBuilder<AzureEntraIdAppRegistrationResource> appRegistration, string alias = "microsoft", string? displayName = null, Uri? brokerRedirectUri = null)` returns `IResourceBuilder<KeycloakIdentityProviderResource>`.
3. **Secret required** — the app registration must have at least one `WithSecret` password credential; otherwise throw at Configure.
4. **Provider** — uses Keycloak `providerId` `oidc` via `AddIdentityProvider`; discovery endpoint `{instance}{tenantId}/v2.0/.well-known/openid-configuration`.
5. **Deferred realm JSON** — initialize resolves `ClientId`, `Azure:TenantId`, and the first password credential secret, fetches the Entra OIDC discovery document, then mutates IdP `config` (`clientId`, `clientSecret`, `clientAuthMethod=client_secret_post`, `defaultScope=openid profile email`, discovery flags/URL, plus resolved `authorizationUrl` / `tokenUrl` / `jwksUrl` / `issuer` and optional `userInfoUrl` / `logoutUrl`). Keycloak does not hydrate those URLs from `discoveryEndpoint` alone at login time.
6. **Email claim mappers** — at Configure (and re-asserted on initialize), writes realm `identityProviderMappers` for the IdP alias: `oidc-user-attribute-idp-mapper` claim `email` → user attribute `email`, and claim `preferred_username` → `email` (Workforce UPN fallback). Both use `syncMode=INHERIT`.
7. **Ordering** — a non-parented config gate (`KeycloakEntraIdConfigResource`, Aspire name `{idp.Name}-config`) `WaitFor`s the app registration and each password credential, then writes OIDC config into realm JSON; Keycloak `WaitFor`s the gate (Aspire forbids waiting on an `IResourceWithParent` descendant). The IdP itself remains the realm child returned by the API.
8. **Broker redirect** — when `brokerRedirectUri` is non-null, `AddWebApplication("{alias}-broker").WithRedirectUri(brokerRedirectUri)` on the app registration.

## Dependencies

- [`domain-glossary`](domain-glossary.md)
- [`keycloak-hosting`](keycloak-hosting.md) — `AddIdentityProvider` / **Keycloak identity provider**
- [`azure-entra-id`](azure-entra-id.md) — **Azure app registration resource**, **Entra ID instance**, **Entra ID password credential resource**, **Azure Entra ID web application**
- [`keycloak-provisioning-realm`](keycloak-provisioning-realm.md) — `IdentityProviderRepresentation`

## Out of scope

- Google or other non-Entra identity providers
- Identity provider mapper Admin UI / arbitrary claim mapping beyond email / preferred_username → email
- SAML federation
- Owning Entra Graph Bicep or generic Keycloak client builders
- Live Azure/Graph in unit tests
- Sample AppHost under `tests/keycloak-entraid/` (unit tests only; saas **test AppHost** may smoke-wire)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Keycloak.EntraId` under `src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/` (`IsPackable=true`, `net10.0`).
- [x] Projects listed in `Neox.Aspire.slnx`; `{paths.keycloakEntraId}` and `{aspire.packages.keycloakEntraId}` in `neox-rules.json`.
- [x] `AddEntraIdIdentityProvider` returns `IResourceBuilder<KeycloakIdentityProviderResource>` and requires `WithSecret` on the registration.
- [x] Initialize writes OIDC IdP config into realm JSON via a non-parented config gate; Keycloak `WaitFor`s the gate; the gate `WaitFor`s registration and password credentials.
- [x] Configure writes `identityProviderMappers` for `email` and `preferred_username` → user `email` (`oidc-user-attribute-idp-mapper`, `syncMode=INHERIT`).
- [x] Optional `brokerRedirectUri` registers an Entra web application child with that URI.
- [x] xUnit tests under `tests/keycloak-entraid/` (no live Azure/Graph).
- [x] Package README documents the API.

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/hosting/Neox.Aspire.Hosting.Keycloak.EntraId/` |
| Package id | `Neox.Aspire.Hosting.Keycloak.EntraId` |
| Namespace | `Neox.Aspire.Hosting.Keycloak.EntraId` |
| Public API | `AddEntraIdIdentityProvider` |
| Config gate | `KeycloakEntraIdConfigResource` (`{idp.Name}-config`; non-parented; Keycloak `WaitFor`s it) |
| Unit tests | `tests/keycloak-entraid/` |
