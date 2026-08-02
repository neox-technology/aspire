# AuthOps — identity provider app registrations

| Field | Value |
|-------|-------|
| Slug | `auth-providers` |
| Status | implemented |
| Last code review | 2026-08-02 |

## Summary

AuthOps is split into hosting packages:

- **`Neox.Aspire.Hosting.Auth.Abstractions`** — common AuthOps model (`AuthOpsResourceBase`), abstract `AuthAppRegistrationResource`, shared `AuthOpsResource`, gate `prereq-providers-auth`, plan/provision/prereq step-name helpers, and **flat** redirect desired-state (`WithRedirectUri` / `WithLocalhostRedirectUri` without Graph platform buckets). **No** `WithAuth` in Abstractions.
- **`Neox.Aspire.Hosting.Auth.EntraId`** — Entra ID provider (`AddAuthProvider` → `.Entra(...)` → `EntraAuthOpsResource` / `AddAppRegistration` → `EntraAuthAppRegistrationResource`), Graph planning + provisioning, typed redirect overloads (`AuthApplicationType`), provider `WithAuth` emitting Microsoft.Identity.Web `AzureAd__*` env vars, and `deploy-auth` on the provider resource.
- **`Neox.Aspire.Hosting.Auth.Google`** — Google adopt/bind provider (`.Google(...)` → `GoogleAuthAppRegistrationResource`); see [`auth-provider-google`](auth-provider-google.md).

Consumers reference a provider package (EntraId or Google), which pulls Abstractions transitively. The former package id **`Neox.Aspire.Hosting.Auth` is retired** (breaking). Namespace remains `Neox.Aspire.Hosting.Auth` across assemblies.

**Decisions (this revision):**

- Entra workload bind: **Microsoft.Identity.Web** config keys (`AzureAd__Instance`, `AzureAd__TenantId`, `AzureAd__ClientId`, optional `AzureAd__ClientSecret`). SPA may `Map` to `VITE_ENTRA_*`.
- Model mirrors DomainOps: provider resource → app registration → pipeline plan/provision → consumer bind.
- App registration configuration uses **`AddAppRegistration(name, displayName)`** plus `WithXxx` methods (no options bag). Abstractions redirects are a flat list; Entra typed overloads map to Graph Web/Spa/Native buckets (`AuthApplicationType.Api` ignored). Supported account types: `WithSupportedAccounts(SupportedAccountsType)` → Graph `signInAudience` (default single-tenant). API exposition: `WithApiExposition` / `WithAppRoleExposition` → Graph `identifierUris`, `oauth2PermissionScopes`, `appRoles`; consume via `WithApiPermission` → Graph `requiredResourceAccess`. Well-known Microsoft Graph permissions: see [`auth-entra-graph-permissions`](auth-entra-graph-permissions.md).
- `WithAuth` is **provider-scoped** and typed to the concrete registration resource (`EntraAuthAppRegistrationResource`, `GoogleAuthAppRegistrationResource`).

## User scenarios

- AppHost registers `AddAuthProvider("auth-provider-entra").Entra(...)`, defines one or more apps with `AddAppRegistration(name, displayName)`, binds consumers with Entra `WithAuth(app)`; local interactive `aspire do` prompts unresolved parameters (tenant via Choice of accessible tenants; ClientId via Choice of Create + app registrations in that tenant + custom GUID); CI supplies `Parameters__*` / Azure credential with `--non-interactive`.
- **Create path:** `prereq-{app}-auth` Choice selects create (or ClientId unset) → `plan-{app}-auth` plans a create → `provision-{app}-auth` creates a minimal Entra application using `DisplayName`, writes ClientId into the Aspire parameter, then `WithAuth` wires `AzureAd__*` env on the consumer.
- **Adopt path:** ClientId parameter set (interactive Choice / `Parameters__*`) → `plan-{app}-auth` GET + compare desired vs existing (DisplayName, redirect URIs, `signInAudience`, identifier URIs, scopes, app roles, required resource access) → `provision-{app}-auth` applies plan actions only; bind existing ClientId/TenantId.
- **API exposition:** an Auth app exposes scopes via `WithApiExposition` (optional Application ID URI; default `api://{ClientId}` resolved after ClientId is known) and app roles via `WithAppRoleExposition`; another Auth app consumes them with `WithApiPermission` (Graph `requiredResourceAccess`). `provision-{consumer}-auth` **DependsOn** `provision-{exposer}-auth` when a permission references an exposition owned by a different Auth app.
- **Sample workloads:** WebAPI (`sample-api`) exposes authorized `GET /me` reading the signed-in user via Microsoft Graph; Blazor Server and Vite ops SPA sign in and call `/me`.
- **Idempotent re-run:** plan finds the app via ClientId and/or DisplayName + Neox marker; provision applies only planned actions.
- **Remember selections:** after interactive tenant / ClientId resolution (and after provision), AuthOps persists values into Aspire deployment state under `Parameters:{parameterName}` (same contract as `ParameterProcessor`) so a later `aspire do` does not re-prompt; create sentinel is never persisted as ClientId.
- Management credentials (Graph) stay separate from workload ClientId/ClientSecret; secrets are never logged or written into manifests.
- Unit tests under `tests/auth-providers/` use fakes (no live Graph / no `az`).

## Routes (if UI)

None — `aspire do` / `aspire deploy` pipeline steps only. No dashboard `WithCommand` in v1. Sample Blazor/Ops UIs are test workloads, not AuthOps product UI.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting` (Abstractions: resources, parameters, pipelines, `IInteractionService`)
- `Aspire.Hosting.Azure`, Azure.Identity, Microsoft Graph (EntraId only) for application CRUD
- Aspire Azure credential surface where available (`ITokenCredentialProvider` / equivalent) for **management** Graph calls; CI may use app-only Graph separately from workload secrets
- Patterns: DomainOps (`AddDomainOpsProvider`, `WithPipelineStepFactory`, parameter prompts) in [`azure-custom-domains`](azure-custom-domains.md)
- Sample workloads: Microsoft.Identity.Web (+ UI / Graph) for Blazor + WebAPI; MSAL browser for ops SPA

## Out of scope

- GitHub / generic OAuth2 providers (reserved for later)
- Google details live in [`auth-provider-google`](auth-provider-google.md) (not duplicated here)
- Well-known Microsoft Graph permission catalogue / codegen details live in [`auth-entra-graph-permissions`](auth-entra-graph-permissions.md)
- Emitting `Authentication__Schemes__*` (Identity.Web uses `AzureAd__*` section binding instead)
- Automatic `DownstreamApi__*` emission inside `WithAuth` (samples set scopes explicitly)
- Azure managed identity (`AddAzureUserAssignedIdentity`) — different concern
- Key Vault sync of workload secrets (candidate phase 2)
- Dedicated Dashboard UI
- Automatic secret rotation on every deploy (opt-in only if added later)
- CIAM / External ID–specific flows (configuring Graph `signInAudience` via `WithSupportedAccounts` is in scope; full multi-tenant / CIAM product flows are not)
- Meta-package / type-forwarding shim retaining package id `Neox.Aspire.Hosting.Auth`
- `WithAuth(IncludeRedirectUri)` env emit, application type as a separate `WithApplicationType`, client-secret create/rotate (`WithXxx` — later revision)
- Live Graph / interactive `aspire do` in CI (build-only smoke remains)
- Google Blazor as a real Google OIDC sample (Entra Blazor is the Identity.Web demo)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Auth.Abstractions` under `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/` (Aspire.Hosting only).
- [x] Package `Neox.Aspire.Hosting.Auth.EntraId` under `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` (refs Abstractions + Graph/Azure).
- [x] Former package id `Neox.Aspire.Hosting.Auth` removed (no shipping assembly with that id).
- [x] `AddAuthProvider(name)` lives in Abstractions; `.Entra(configure)` is an EntraId extension on `IAuthProviderBuilder`.
- [x] `AuthAppRegistrationResource` is abstract in Abstractions; Entra constructs `EntraAuthAppRegistrationResource`; Google constructs `GoogleAuthAppRegistrationResource`.
- [x] `AddAppRegistration(name, displayName)` returns the provider concrete registration builder; no options bag (`WithRedirectUri` / `WithLocalhostRedirectUri` for redirect desired-state; Entra: `WithSupportedAccounts`, `WithApiExposition` / `WithAppRoleExposition` / `WithApiPermission`).
- [x] `WithSupportedAccounts(SupportedAccountsType)` sets desired Entra supported accounts (annotation; replace on re-call); default when omitted is `SingleTenant` (`AzureADMyOrg`).
- [x] `WithApiExposition(url, configure)` / `WithApiExposition(configure)` expose an Application ID URI (`identifierUris`; default `api://{ClientId}` deferred until ClientId known) and scopes via `IApiExpositionBuilder.AddScopeWithAdminConsent` / `AddScopeWithAdminAndUserConsent` → `IResourceBuilder<ScopeApiExposition>`.
- [x] `WithAppRoleExposition(AllowedMemberType, value, description)` exposes an app role → `IResourceBuilder<AppRoleApiExposition>`.
- [x] `WithApiPermission<TApiExposition>(IResourceBuilder<TApiExposition>)` on a consumer Auth app records Graph `requiredResourceAccess` (Scope or Role) against the exposer app.
- [x] `plan-{app}-auth` / `provision-{app}-auth` include identifier URIs, `oauth2PermissionScopes`, `appRoles`, and `requiredResourceAccess` (upsert without deleting Graph entries outside the model); create resolves deferred `api://{ClientId}` after AppId is known.
- [x] When `WithApiPermission` references an exposition owned by another Auth app, `provision-{consumer}-auth` **DependsOn** `provision-{exposer}-auth`.
- [x] Unit tests cover API exposition / app role / permission model accumulation, plan compare actions, and Fake provisioner desired fields; sample AppHost wires an API Auth app + `WithApiPermission` on the web Auth app.
- [x] `plan-{app}-auth` / `provision-{app}-auth` include `signInAudience`: create uses desired audience; adopt plans `UpdateSignInAudience` when it differs and provision PATCHes it.
- [x] Abstractions does **not** expose `WithAuth`; Entra `WithAuth(EntraAuthAppRegistrationResource)` injects `AzureAd__Instance` / `AzureAd__TenantId` / `AzureAd__ClientId` (optional `AzureAd__ClientSecret`) via `WithEnvironment` late-bound to parameters — never resolves secrets at model build time.
- [x] `WithAuth` omits client secret unless `IncludeClientSecret == true`; SPA may `Map` outputs to `VITE_ENTRA_*`.
- [x] `WithLocalhostRedirectUri` / `WithRedirectUri` accumulate desired redirect URIs on the registration resource.
- [x] Unit tests cover redirect URI accumulation, localhost defaults, parameter+path, validation, and Graph redirect apply helpers.
- [x] `plan-{app}-auth` / `provision-{app}-auth` resolve redirect URIs and apply them on Graph create; adopt plans `UpdateRedirectUris` when Web/Spa/Native buckets differ (`AuthApplicationType.Api` entries are ignored for Graph).
- [x] Pipeline steps match **Pipeline step contracts** below; tags include `auth-ops`; `deploy-auth` is required by Aspire `deploy`.
- [x] Shared `AuthOpsResource` (`auth-ops`) hosts noop gate `prereq-providers-auth` (fan-in of all `prereq-{providerResource}-auth`).
- [x] Dashboard hierarchy: `auth-ops` → Entra provider → Auth apps → scopes/app roles; tenant parameter under provider; ClientId/ClientSecret under Auth app (`IResourceWithParent` + `WithParentRelationship`).
- [x] `.Entra(...)` creates `EntraAuthOpsResource` (: `AuthOpsResourceBase`); provider prereq is `prereq-{providerResource.Name}-auth`.
- [x] `EntraAuthProviderOptions.TenantId` is `IResourceBuilder<ParameterResource>?`; auto-creates `{name}-tenant-id` when null; parameter uses `WithCustomInput` Choice (+ `AllowCustomChoice`) of ARM tenants.
- [x] Per Auth app, `prereq-{app}-auth` **DependsOn** `prereq-providers-auth`; ClientId parameter Choice (create sentinel + `AllowCustomChoice` GUID); `DisplayName` is a required `AddAppRegistration` argument (not a ParameterResource).
- [x] `prereq-{app}-auth` ClientId Choice prefetches Graph app registrations in the selected tenant; ClientId Choice model-time input uses `DynamicLoading`.
- [x] `plan-{app}-auth` **DependsOn** `prereq-{app}-auth`; read-only Graph resolve + desired-vs-existing compare; attaches plan (no mutating writes).
- [x] `provision-{app}-auth` **DependsOn** `plan-{app}-auth`; applies plan only; create sentinel is never persisted as ClientId.
- [x] Management vs workload credentials are separated; workload secrets use `ParameterResource` with `secret: true`.
- [x] Interactive prompts for missing params when `IInteractionService` is available; otherwise require `Parameters__*` / credential config (CI).
- [x] Unit tests with Graph fakes; EntraId package README for consumers.
- [x] Unit tests assert AuthOps pipeline graph (`prereq-{resource}-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth`).
- [x] Sample AppHost wires WebAPI + Blazor Web + Vite Spa via AuthOps; API `GET /me` calls Graph; Blazor and Ops sign in and call `/me`; Auth app resource names stay distinct from workload resources (`api`/`blazor`/`ops`); CI smoke is `dotnet build` only (no live Graph).
- [x] Glossary terms for AuthOps promoted in [`domain-glossary`](domain-glossary.md).
- [x] Tenant and ClientId Choice labels identify the Auth provider resource and Auth app.
- [x] Resolved tenant (and non-sentinel ClientId) are persisted via `IDeploymentStateManager` section `Parameters:{parameterName}` + `SetValue` after prereq (and again after provision with the real ClientId); create sentinel is never written as ClientId.
- [x] Unit tests cover deployment-state section key / `SetValue` and prompt labels.

## Terminology

See [`domain-glossary`](domain-glossary.md). Feature-local API names below until promoted.

## Implementation notes

| Item | Path / value |
|------|----------------|
| Abstractions project | `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/` |
| Abstractions package id | `Neox.Aspire.Hosting.Auth.Abstractions` |
| EntraId project | `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` |
| EntraId package id | `Neox.Aspire.Hosting.Auth.EntraId` |
| Namespace (both) | `Neox.Aspire.Hosting.Auth` |
| Product vocabulary | AuthOps |
| Provider API | `AddAuthProvider`, `AuthOpsResourceBase`, `.Entra(...)` → `EntraAuthOpsResource` (EntraId) |
| App API | `AddAppRegistration(name, displayName)`, abstract `AuthAppRegistrationResource`, `EntraAuthAppRegistrationResource`, `WithRedirectUri` / `WithLocalhostRedirectUri`, `WithSupportedAccounts`, `WithApiExposition` / `WithAppRoleExposition` / `WithApiPermission` |
| Binding API | Entra `WithAuth` → `AzureAd__*` (Identity.Web); Google `WithAuth` → `AUTH_GOOGLE_*` |
| Provisioner | `EntraGraphAppProvisioner` (`PlanAsync` / `ProvisionAsync`) in EntraId |
| Unit tests | `tests/auth-providers/` |
| Sample AppHost | `tests/auth-providers/sample-apphost/` |
| Sample API | `tests/auth-providers/sample-api/` — workload `api`; `GET /me` via Graph |
| Sample Blazor | `tests/auth-providers/sample-blazor/` — workload `blazor`; Microsoft.Identity.Web + call `/me` |
| Sample ops | `tests/auth-providers/sample-ops/` — workload `ops`; MSAL + `VITE_ENTRA_*` + call `/me` |
| Pipeline tags | `["auth-ops"]` |

### Proposed AppHost surface

```csharp
// PackageReference: Neox.Aspire.Hosting.Auth.EntraId
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra();

var apiAppReg = entra.AddAppRegistration("api", "MyApi-Local")
    .WithApiPermission(MicrosoftGraph.Delegated.UserRead);
IResourceBuilder<ScopeApiExposition>? accessAsUser = null;
apiAppReg.WithApiExposition(api =>
{
    accessAsUser = api.AddScopeWithAdminAndUserConsent(
        "access_as_user", "Access API", "Allows access to the API as the signed-in user.",
        "Access API", "Allow the app to access the API on your behalf.");
});

var webAppReg = entra.AddAppRegistration("web", "MyApp-Local")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, path: "signin-oidc")
    .WithApiPermission(accessAsUser!);

var api = builder.AddProject<Projects.Api>("api")
    .WithAuth(apiAppReg, env => env.IncludeClientSecret = true);

var web = builder.AddProject<Projects.Web>("web")
    .WithAuth(webAppReg, env => env.IncludeClientSecret = true)
    .WithReference(api);
```

### Environment naming (Entra / Identity.Web)

| Setting | Default env | Aspire parameter (dash) |
|---------|-------------|-------------------------|
| Instance | `AzureAd__Instance` = `https://login.microsoftonline.com/` | literal (toggle via options) |
| Tenant id | `AzureAd__TenantId` | `{provider}-tenant-id` |
| Client id | `AzureAd__ClientId` | `{provider}-{app}-client-id` |
| Client secret | `AzureAd__ClientSecret` | `{provider}-{app}-client-secret` (`secret: true`) — only when `IncludeClientSecret == true` |

SPA escape hatch: `env.Map(...)` → `VITE_ENTRA_TENANT_ID` / `VITE_ENTRA_CLIENT_ID` (and `IncludeInstance = false`).

### Parameter / credential separation

| Concern | Source | Used by |
|---------|--------|---------|
| Management (create/update app) | `ITokenCredentialProvider` / Graph app-only | `prereq-{providerResource}-auth`, `plan-{app}-auth` (read), `provision-{app}-auth` (write) |
| Workload (ClientId / secret / tenant for apps) | Output `ParameterResource`s | Provider `WithAuth` → consumer env |

Never resolve workload secrets at distributed-application model build time. Never write secrets into manifests or generated config files.

### Pipeline step contracts

Shared gate `prereq-providers-auth` is hosted on **`AuthOpsResource`** (`auth-ops`). Provider prereq and `deploy-auth` stay on the provider resource (`EntraAuthOpsResource`).

| Step | Registered by | DependsOn / RequiredBy | Exit 0 | Exit ≠ 0 |
|------|---------------|------------------------|--------|----------|
| `prereq-{providerResource}-auth` | `.Entra(...)` (idempotent) | **RequiredBy** `prereq-providers-auth` | Tenant parameter ready | Missing interactive input / credential failure |
| `prereq-providers-auth` | Abstractions on `AuthOpsResource` (idempotent) | Fan-in of all `prereq-{providerResource}-auth` (noop) | Gate only | Dependency failure |
| `prereq-{app}-auth` | `AddAppRegistration` on registration resource (idempotent) | **DependsOn** `prereq-providers-auth` | ClientId parameter ready | Missing interactive input |
| `plan-{app}-auth` | `AddAppRegistration` (EntraId) | **DependsOn** `prereq-{app}-auth` | Plan attached (no mutating Graph writes) | Graph failure / missing adopt target / invalid DisplayName |
| `provision-{app}-auth` | `AddAppRegistration` (EntraId) | **DependsOn** `plan-{app}-auth` (+ exposer provision when needed) | Plan applied; workload ClientId/TenantId set | Graph failure |
| `deploy-auth` | EntraId gate on provider (idempotent) | all `provision-{app}-auth`; **RequiredBy** Aspire `deploy` | Gate only | Dependency failure |

`{app}` is the Auth app resource name slug (same dash rules as other Neox steps).

#### `prereq-{providerResource}-auth` / `prereq-{app}-auth` / `plan` / `provision`

Behavior unchanged from prior revision (tenant Choice, ClientId Choice create/adopt, Graph compare + apply). See previous implementation notes in git history for detailed step algorithms.

### Target package layout

```
src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/
  AuthProviderExtensions.cs
  AuthOpsResource.cs
  AuthOpsResourceBase.cs
  AuthOpsExtensions.cs          # step names + gates (no WithAuth)
  Apps/AuthAppRegistrationResource.cs
  Apps/AuthAppRegistrationResourceExtensions.cs
  README.md

src/hosting/Neox.Aspire.Hosting.Auth.EntraId/
  Apps/EntraAuthAppRegistrationResource.cs
  EntraAuthEnvOptions.cs
  EntraAuthProviderBuilderExtensions.cs
  EntraAuthOpsExtensions.cs              # prereq / plan / provision / deploy-auth + WithAuth
  ...
```

### Implementation sequencing

1. Spec status → `defined` (this revision).
2. Abstract registration resource + provider concretes + provider `WithAuth`.
3. Sample API / Blazor / Ops; update tests/READMEs; build + unit tests.
4. Status → `implemented`.
