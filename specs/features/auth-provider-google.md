# AuthOps — Google adopt/bind

| Field | Value |
|-------|-------|
| Slug | `auth-provider-google` |
| Status | implemented |
| Last code review | 2026-08-02 |

## Summary

Adds **`Neox.Aspire.Hosting.Auth.Google`** as a sibling AuthOps provider to EntraId:

- `AddAuthProvider` → `.Google(...)` → `GoogleAuthOpsResource` / `AddAppRegistration`
- **Adopt/bind only**: resolve ProjectId + existing ClientId, then inject `AUTH_GOOGLE_*` via shared `WithAuth`
- Flat redirect desired-state stays on the AppHost model (`WithRedirectUri` / `WithLocalhostRedirectUri`) — **no** Google API apply
- Shared Abstractions owns flat redirects; Entra keeps Graph platform buckets (`AuthApplicationType`)

**Product boundary (frozen):** Google AuthOps does **not** create or patch oauth clients (neither IAM `oauthClients` nor Google Auth Platform console clients). Create Entra-like is out of scope with a clear error. IAP `oauth-clients` remain out of scope.

## User scenarios

- AppHost registers `AddAuthProvider("provider-google").Google(...)`, defines apps with `AddAppRegistration(name, displayName)` + optional flat redirects, binds consumers with `WithAuth(app)`.
- Interactive `aspire do` prompts ProjectId (Choice of ADC/`gcloud`-visible projects) then ClientId (listed existing IAM oauthClients when enumeration succeeds, plus custom paste via `AllowCustomChoice`); CI supplies `Parameters__*`.
- **Bind path:** ClientId set → `plan-{app}-auth` validates ClientId is present (rejects create/empty) and optionally GETs the client when IAM list/get succeeds → `provision-{app}-auth` persists ProjectId + ClientId only (no POST/PATCH).
- Persistence: ProjectId / ClientId via `IDeploymentStateManager` `Parameters:{name}` (same as Entra).
- Management ADC (project/client list) stays separate from workload ClientSecret; no secret create/rotate.

## Routes (if UI)

None — pipeline steps only.

## Dependencies

- Shared AuthOps model ([`auth-providers`](auth-providers.md)), terminology ([`domain-glossary`](domain-glossary.md)), pack/publish ([`nuget-org`](nuget-org.md))
- `Aspire.Hosting`; Google ADC / Cloud Resource Manager for ProjectId Choice; optional IAM list/get for ClientId Choice validation
- Patterns: EntraId provider pipeline (`prereq` / `plan` / `provision` / `deploy-auth`) — step **names** shared; Google steps bind only

## Out of scope

- Creating or patching IAM `oauthClients` (displayName, redirect URIs, credentials)
- Creating or patching Google Auth Platform / console « Sign-in with Google » clients
- IAP programmatic oauth-clients / brands
- Applying AppHost redirect desired-state to any Google API
- `WithSupportedAccounts`, `WithApiExposition` / app roles / `WithApiPermission` (Entra-only)
- Client-secret create/rotate; `WithAuth(IncludeRedirectUri)` env emit
- ASP.NET Core scheme mapping; live Google integration tests in CI

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Auth.Google` under `src/hosting/Neox.Aspire.Hosting.Auth.Google/` (refs Abstractions + Google.Apis.Auth).
- [x] `.Google(configure?)` creates `GoogleAuthOpsResource` (`ProviderSlug = "google"`); ProjectId parameter `{name}-project-id` Choice (+ `AllowCustomChoice`).
- [x] AuthorityExpression is constant `https://accounts.google.com`.
- [x] `AddAppRegistration(name, displayName)` wires ClientId/ClientSecret parameters and app pipeline steps; shares provider ProjectId as `TenantIdParameter`.
- [x] Flat redirects may be modeled on the Auth app; they are **not** applied via Google APIs.
- [x] Pipeline: `prereq-{provider}-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth` → `deploy-auth`.
- [x] ClientId Choice: listed oauthClients (when enum succeeds) + custom; **no** Create option; empty/create-sentinel → clear error in plan.
- [x] `plan` / `provision` bind ProjectId + ClientId only (no IAM create/patch).
- [x] `WithAuth` emits `AUTH_GOOGLE_*` (multi-app: `AUTH_GOOGLE_{APP}_*`); `AUTH_GOOGLE_TENANT_ID` holds ProjectId.
- [x] Unit tests with bind fakes; sample AppHost wires a Google Auth app; smoke `dotnet build` only.
- [x] Package README documents adopt/bind, ADC, env table, create out of scope.
- [x] Glossary + specs index aligned.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Google project | `src/hosting/Neox.Aspire.Hosting.Auth.Google/` |
| Package id | `Neox.Aspire.Hosting.Auth.Google` |
| Shipping | Not published yet (`IsPackable=false`); remains in repo + tests |
| Namespace | `Neox.Aspire.Hosting.Auth` |
| Provider API | `.Google(...)` → `GoogleAuthOpsResource` |
| Binder | `IGoogleIamOauthClientProvisioner` (`PlanAsync` / `ProvisionAsync` — bind only) |
| Unit tests | `tests/auth-providers/` |
| Sample | `tests/auth-providers/sample-apphost/` |

### Env defaults

| Setting | Default env | Aspire parameter |
|---------|-------------|------------------|
| ProjectId (as TenantId) | `AUTH_GOOGLE_TENANT_ID` | `Parameters__{provider}-project-id` |
| ClientId | `AUTH_GOOGLE_CLIENT_ID` | `Parameters__{provider}-{app}-client-id` |
| ClientSecret | `AUTH_GOOGLE_CLIENT_SECRET` | only when `IncludeClientSecret = true` |
| Authority | `AUTH_GOOGLE_AUTHORITY` | `https://accounts.google.com` |

### Pipeline step contracts

Same naming/dependency graph as Entra ([`auth-providers`](auth-providers.md)); Google `plan`/`provision` bind parameters only.
