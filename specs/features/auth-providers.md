# AuthOps — identity provider app registrations

| Field | Value |
|-------|-------|
| Slug | `auth-providers` |
| Status | implemented |
| Last code review | 2026-08-02 |

## Summary

AuthOps is split into two hosting packages:

- **`Neox.Aspire.Hosting.Auth.Abstractions`** — common AuthOps model (`AuthOpsResourceBase`), generic `WithAuth` env injection, shared `AuthOpsResource`, and gate `prereq-providers-auth`.
- **`Neox.Aspire.Hosting.Auth.EntraId`** — Entra ID provider (`AddAuthProvider` → `.Entra(...)` → `EntraAuthOpsResource` / `AddAppRegistration`), Graph planning + provisioning, provider prereq `prereq-{providerResource}-auth`, app prereq `prereq-{app}-auth` (ClientId Choice: Create + apps in tenant + custom GUID), and `deploy-auth` on the provider resource.

Consumers reference **`Neox.Aspire.Hosting.Auth.EntraId`** (pulls Abstractions transitively). The former package id **`Neox.Aspire.Hosting.Auth` is retired** (breaking). Namespace remains `Neox.Aspire.Hosting.Auth` in both assemblies.

**v1 decisions (frozen):**

- Provisioning: **Entra only** (create/update application). Google / GitHub / generic OAuth2 are out of scope for v1.
- Secret injection: **generic env only** (no ASP.NET Core `Authentication__Schemes__*` mapping).
- Model mirrors DomainOps: provider resource → app registration → pipeline plan/provision → consumer bind.
- App registration configuration uses **`AddAppRegistration(name, displayName)`** plus `WithXxx` methods (no options bag). Redirect URI desired-state: `WithLocalhostRedirectUri` / `WithRedirectUri` — applied on Graph create/adopt via `plan|provision-{app}-auth`. Supported account types: `WithSupportedAccounts(SupportedAccountsType)` → Graph `signInAudience` (default single-tenant). API exposition: `WithApiExposition` / `WithAppRoleExposition` → Graph `identifierUris`, `oauth2PermissionScopes`, `appRoles`; consume via `WithApiPermission` → Graph `requiredResourceAccess`.

## User scenarios

- AppHost registers `AddAuthProvider("auth-provider-entra").Entra(...)`, defines one or more apps with `AddAppRegistration(name, displayName)`, binds consumers with `WithAuth(app)`; local interactive `aspire do` prompts unresolved parameters (tenant via Choice of accessible tenants; ClientId via Choice of Create + app registrations in that tenant + custom GUID); CI supplies `Parameters__*` / Azure credential with `--non-interactive`.
- **Create path:** `prereq-{app}-auth` Choice selects create (or ClientId unset) → `plan-{app}-auth` plans a create → `provision-{app}-auth` creates a minimal Entra application using `DisplayName`, writes ClientId into the Aspire parameter, then `WithAuth` wires `AUTH_ENTRA_*` env on the consumer.
- **Adopt path:** ClientId parameter set (interactive Choice / `Parameters__*`) → `plan-{app}-auth` GET + compare desired vs existing (DisplayName, redirect URIs, `signInAudience`, identifier URIs, scopes, app roles, required resource access) → `provision-{app}-auth` applies plan actions only; bind existing ClientId/TenantId.
- **API exposition:** an Auth app exposes scopes via `WithApiExposition` (optional Application ID URI; default `api://{ClientId}` resolved after ClientId is known) and app roles via `WithAppRoleExposition`; another Auth app consumes them with `WithApiPermission` (Graph `requiredResourceAccess`). `provision-{consumer}-auth` **DependsOn** `provision-{exposer}-auth` when a permission references an exposition owned by a different Auth app.
- **Idempotent re-run:** plan finds the app via ClientId and/or DisplayName + Neox marker; provision applies only planned actions.
- **Remember selections:** after interactive tenant / ClientId resolution (and after provision), AuthOps persists values into Aspire deployment state under `Parameters:{parameterName}` (same contract as `ParameterProcessor`) so a later `aspire do` does not re-prompt; create sentinel is never persisted as ClientId.
- Management credentials (Graph) stay separate from workload ClientId/ClientSecret; secrets are never logged or written into manifests.
- Unit tests under `tests/auth-providers/` use fakes (no live Graph / no `az`).

## Routes (if UI)

None — `aspire do` / `aspire deploy` pipeline steps only. No dashboard `WithCommand` in v1.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting` (Abstractions: resources, parameters, pipelines, `IInteractionService`)
- `Aspire.Hosting.Azure`, Azure.Identity, Microsoft Graph (EntraId only) for application CRUD
- Aspire Azure credential surface where available (`ITokenCredentialProvider` / equivalent) for **management** Graph calls; CI may use app-only Graph separately from workload secrets
- Patterns: DomainOps (`AddDomainOpsProvider`, `WithPipelineStepFactory`, parameter prompts) in [`azure-custom-domains`](azure-custom-domains.md)

## Out of scope

- Google / GitHub / generic OAuth2 providers (API not exposed in v1; reserved for later adopt-or-provision)
- ASP.NET Core authentication scheme config (`AddMicrosoftIdentityWebApp`, `Authentication__Schemes__*`)
- Azure managed identity (`AddAzureUserAssignedIdentity`) — different concern
- Key Vault sync of workload secrets (candidate phase 2)
- Dedicated Dashboard UI
- Automatic secret rotation on every deploy (opt-in only if added later)
- CIAM / External ID–specific flows (configuring Graph `signInAudience` via `WithSupportedAccounts` is in scope; full multi-tenant / CIAM product flows are not)
- Meta-package / type-forwarding shim retaining package id `Neox.Aspire.Hosting.Auth`
- `WithAuth(IncludeRedirectUri)` env emit, application type as a separate `WithApplicationType`, client-secret create/rotate (`WithXxx` — later revision)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Auth.Abstractions` under `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/` (Aspire.Hosting only).
- [x] Package `Neox.Aspire.Hosting.Auth.EntraId` under `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` (refs Abstractions + Graph/Azure).
- [x] Former package id `Neox.Aspire.Hosting.Auth` removed (no shipping assembly with that id).
- [x] `AddAuthProvider(name)` lives in Abstractions; `.Entra(configure)` is an EntraId extension on `IAuthProviderBuilder`.
- [x] `AddAppRegistration(name, displayName)` models an Entra app registration (`AuthAppResource`) with required display name; no options bag (`WithRedirectUri` / `WithLocalhostRedirectUri` for redirect desired-state; `WithSupportedAccounts` for `signInAudience`; `WithApiExposition` / `WithAppRoleExposition` / `WithApiPermission` for API exposure and consumption).
- [x] `WithSupportedAccounts(SupportedAccountsType)` sets desired Entra supported accounts on `AuthAppResource` (annotation; replace on re-call); default when omitted is `SingleTenant` (`AzureADMyOrg`).
- [x] `WithApiExposition(url, configure)` / `WithApiExposition(configure)` expose an Application ID URI (`identifierUris`; default `api://{ClientId}` deferred until ClientId known) and scopes via `IApiExpositionBuilder.AddScopeWithAdminConsent` / `AddScopeWithAdminAndUserConsent` → `IResourceBuilder<ScopeApiExposition>`.
- [x] `WithAppRoleExposition(AllowedMemberType, value, description)` exposes an app role → `IResourceBuilder<AppRoleApiExposition>`.
- [x] `WithApiPermission<TApiExposition>(IResourceBuilder<TApiExposition>)` on a consumer Auth app records Graph `requiredResourceAccess` (Scope or Role) against the exposer app.
- [x] `plan-{app}-auth` / `provision-{app}-auth` include identifier URIs, `oauth2PermissionScopes`, `appRoles`, and `requiredResourceAccess` (upsert without deleting Graph entries outside the model); create resolves deferred `api://{ClientId}` after AppId is known.
- [x] When `WithApiPermission` references an exposition owned by another Auth app, `provision-{consumer}-auth` **DependsOn** `provision-{exposer}-auth`.
- [x] Unit tests cover API exposition / app role / permission model accumulation, plan compare actions, and Fake provisioner desired fields; sample AppHost wires an API Auth app + `WithApiPermission` on the web Auth app.
- [x] `plan-{app}-auth` / `provision-{app}-auth` include `signInAudience`: create uses desired audience; adopt plans `UpdateSignInAudience` when it differs and provision PATCHes it.
- [x] `WithAuth(app)` / `WithAuth(app, env => …)` injects only generic `AUTH_*` (or custom prefix) via `WithEnvironment` late-bound to parameters — never resolves secrets at model build time; Authority uses provider `AuthorityExpression` (`ReferenceExpression`, Entra sets `login.microsoftonline.com/{tenant}`).
- [x] `WithAuth` omits client secret unless `IncludeClientSecret == true`; no default redirect URI from app registration options.
- [x] `WithLocalhostRedirectUri(redirectUriType, port?, path?)`, `WithRedirectUri(redirectUriType, string)`, and `WithRedirectUri(redirectUriType, parameter, path?)` accumulate desired redirect URIs on `AuthAppResource`.
- [x] Unit tests cover redirect URI accumulation, localhost defaults, parameter+path, validation, and Graph redirect apply helpers.
- [x] `plan-{app}-auth` / `provision-{app}-auth` resolve redirect URIs and apply them on Graph create; adopt plans `UpdateRedirectUris` when Web/Spa/Native buckets differ (`AuthApplicationType.Api` entries are ignored for Graph).
- [x] Pipeline steps match **Pipeline step contracts** below; tags include `auth-ops`; `deploy-auth` is required by Aspire `deploy`.
- [x] Shared `AuthOpsResource` (`auth-ops`) hosts noop gate `prereq-providers-auth` (fan-in of all `prereq-{providerResource}-auth`).
- [x] Dashboard hierarchy: `auth-ops` → Entra provider → Auth apps → scopes/app roles; tenant parameter under provider; ClientId/ClientSecret under Auth app (`IResourceWithParent` + `WithParentRelationship`).
- [x] `.Entra(...)` creates `EntraAuthOpsResource` (: `AuthOpsResourceBase`); provider prereq is `prereq-{providerResource.Name}-auth` (e.g. `prereq-auth-provider-entra-auth`).
- [x] `EntraAuthProviderOptions.TenantId` is `IResourceBuilder<ParameterResource>?`; auto-creates `{name}-tenant-id` when null; parameter uses `WithCustomInput` Choice (+ `AllowCustomChoice`) of ARM tenants.
- [x] Per Auth app, `prereq-{app}-auth` **DependsOn** `prereq-providers-auth`; ClientId parameter Choice (create sentinel + `AllowCustomChoice` GUID); `DisplayName` is a required `AddAppRegistration` argument (not a ParameterResource).
- [x] `prereq-{app}-auth` ClientId Choice prefetches Graph app registrations in the selected tenant (Create + listed apps + custom GUID); empty list on Graph failure / missing `Application.Read.All` (fallback Create + Other only).
- [x] `plan-{app}-auth` **DependsOn** `prereq-{app}-auth`; read-only Graph resolve + desired-vs-existing compare; attaches plan (no mutating writes).
- [x] `provision-{app}-auth` **DependsOn** `plan-{app}-auth`; applies plan only (create = DisplayName + redirect URIs; optional DisplayName / redirect URI patches); create sentinel is never persisted as ClientId.
- [x] Management vs workload credentials are separated; workload secrets use `ParameterResource` with `secret: true`.
- [x] Interactive prompts for missing params when `IInteractionService` is available; otherwise require `Parameters__*` / credential config (CI).
- [x] Unit tests with Graph fakes; EntraId package README for consumers.
- [x] Unit tests assert AuthOps pipeline graph (`prereq-{resource}-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth`).
- [x] Sample AppHost (`tests/auth-providers/sample-apphost/`) wires Blazor Web + Vite Spa via AuthOps; Auth app resource names stay distinct from workload resources (`blazor`/`ops`); CI smoke is `dotnet build` only (no live Graph).
- [x] Glossary terms for AuthOps promoted in [`domain-glossary`](domain-glossary.md).
- [x] Tenant and ClientId Choice labels identify the Auth provider resource and Auth app (`Entra tenant — {provider}`, `Entra app — {app} ({displayName})`).
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
| App API | `AddAppRegistration(name, displayName)`, `AuthAppResource`, `WithRedirectUri` / `WithLocalhostRedirectUri`, `WithSupportedAccounts`, `WithApiExposition` / `WithAppRoleExposition` / `WithApiPermission` |
| Binding API | `WithAuth`, env mapping options |
| Provisioner | `EntraGraphAppProvisioner` (`PlanAsync` / `ProvisionAsync`) in EntraId |
| Unit tests | `tests/auth-providers/` |
| Sample AppHost | `tests/auth-providers/sample-apphost/` (Blazor Server + Vite ops SPA; smoke `dotnet build` only) |
| Sample Blazor | `tests/auth-providers/sample-blazor/` — workload `blazor`; Auth app → `AUTH_ENTRA_*` |
| Sample ops | `tests/auth-providers/sample-ops/` — workload `ops`; Auth app; `WithAuth` maps `VITE_ENTRA_*` |
| Pipeline tags | `["auth-ops"]` |

### Proposed AppHost surface (v1)

```csharp
// PackageReference: Neox.Aspire.Hosting.Auth.EntraId
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra(o =>
    {
        // Optional: bind an existing Aspire parameter. Otherwise AuthOps creates
        // {name}-tenant-id with a Choice combobox of tenants the credential can access.
        // o.TenantId = builder.AddParameter("my-tenant");
    });

var apiAppReg = entra.AddAppRegistration("api", "MyApi-Local");
IResourceBuilder<ScopeApiExposition>? accessAsUser = null;
apiAppReg.WithApiExposition(api =>
{
    accessAsUser = api.AddScopeWithAdminAndUserConsent(
        "access_as_user", "Access API", "Allows access to the API as the signed-in user.",
        "Access API", "Allow the app to access the API on your behalf.");
});
var apiCaller = apiAppReg.WithAppRoleExposition(
    AllowedMemberType.Applications, "Api.Caller", "Applications that call the API");

var webAppReg = entra.AddAppRegistration("web", "MyApp-Local")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc")
    .WithSupportedAccounts(SupportedAccountsType.SingleTenant) // default; MultiTenant / MultiTenantAndPersonal / PersonalMicrosoftAccount
    .WithApiPermission(accessAsUser!)
    .WithApiPermission(apiCaller);
// webAppReg.WithRedirectUri(AuthApplicationType.Web, "https://contoso.example/signin-oidc");
// webAppReg.WithRedirectUri(AuthApplicationType.Spa, builder.AddParameter("public-base-url"), "/");
// Future: webAppReg.WithApplicationType(...); // app-level type; per-URI type is already on WithRedirectUri


var api = builder.AddProject<Projects.Api>("api")
    .WithAuth(webAppReg);

api.WithAuth(webAppReg, env =>
{
    env.Prefix = "AUTH_ENTRA"; // default from provider slug
    // env.Map(AuthOutput.ClientId, "MY_CUSTOM_CLIENT_ID"); // escape hatch
    // env.IncludeClientSecret = true; // omit secret unless explicitly requested
});
```

### Environment naming (generic)

Convention: `AUTH_{PROVIDER_SLUG}_{SETTING}` with provider slug uppercased.

| Setting | Default env (Entra) | Aspire parameter (dash) |
|---------|---------------------|-------------------------|
| Tenant id | `AUTH_ENTRA_TENANT_ID` | `{provider}-tenant-id` (e.g. `entra-tenant-id`) |
| Client id | `AUTH_ENTRA_CLIENT_ID` | `{provider}-client-id` or `{provider}-{app}-client-id` when multiple apps |
| Client secret | `AUTH_ENTRA_CLIENT_SECRET` | `{provider}-client-secret` / `{provider}-{app}-client-secret` (`secret: true`) — injected only when `IncludeClientSecret == true` |
| Authority | `AUTH_ENTRA_AUTHORITY` | Derived via provider `AuthorityExpression` (`https://login.microsoftonline.com/{tenant}` for Entra) — optional emit |
| Redirect URI | `AUTH_ENTRA_REDIRECT_URI` | Optional emit later (`IncludeRedirectUri`); desired URIs via `WithRedirectUri` / `WithLocalhostRedirectUri` |

CI: `Parameters__entra-client-id`, `Parameters__entra-client-secret`, `Parameters__entra-tenant-id` (and app-qualified names when multiple apps share one provider resource).

When multiple `AddAppRegistration` instances exist under one provider, parameter and env names **must** include the app slug to avoid collisions (e.g. `AUTH_ENTRA_WEB_CLIENT_ID` **or** keep `AUTH_ENTRA_*` only for a single default app and require `env.Prefix` / `Map` for additional apps). **v1 rule:** if more than one app is registered on the provider, default env vars are `AUTH_ENTRA_{APP}_{SETTING}` (app slug uppercased); single-app keep short `AUTH_ENTRA_{SETTING}`.

### Parameter / credential separation

| Concern | Source | Used by |
|---------|--------|---------|
| Management (create/update app) | `ITokenCredentialProvider` / Graph app-only | `prereq-{providerResource}-auth`, `plan-{app}-auth` (read), `provision-{app}-auth` (write) |
| Workload (ClientId / secret / tenant for apps) | Output `ParameterResource`s | `WithAuth` → consumer env |

Never resolve workload secrets at distributed-application model build time. Never write secrets into manifests or generated config files.

### Pipeline step contracts

Shared gate `prereq-providers-auth` is hosted on **`AuthOpsResource`** (`auth-ops`). Provider prereq and `deploy-auth` stay on the provider resource (`EntraAuthOpsResource`).

| Step | Registered by | DependsOn / RequiredBy | Exit 0 | Exit ≠ 0 |
|------|---------------|------------------------|--------|----------|
| `prereq-{providerResource}-auth` | `.Entra(...)` (idempotent; name = Aspire resource name, e.g. `prereq-auth-provider-entra-auth`) | **RequiredBy** `prereq-providers-auth` | Tenant parameter ready (Choice of ARM tenants / custom GUID) | Missing interactive input / credential failure |
| `prereq-providers-auth` | Abstractions on `AuthOpsResource` (idempotent) | Fan-in of all `prereq-{providerResource}-auth` (noop) | Gate only | Dependency failure |
| `prereq-{app}-auth` | `AddAppRegistration` on `AuthAppResource` (idempotent) | **DependsOn** `prereq-providers-auth` | ClientId parameter ready (Choice: Create + Graph apps in selected tenant + custom GUID); create uses `DisplayName` from `AddAppRegistration` | Missing interactive input |
| `plan-{app}-auth` | `AddAppRegistration` (EntraId) | **DependsOn** `prereq-{app}-auth` | Create vs existing resolved; desired-vs-existing compared; plan attached (no mutating Graph writes) | Graph failure / missing adopt target / invalid DisplayName |
| `provision-{app}-auth` | `AddAppRegistration` (EntraId) | **DependsOn** `plan-{app}-auth` (+ `provision-{exposer}-auth` when `WithApiPermission` references another Auth app) | Plan applied (create DisplayName + redirects + signInAudience + identifier URIs / scopes / appRoles; adopt may patch those + `requiredResourceAccess`; or noop bind); workload ClientId/TenantId set | Graph failure |
| `deploy-auth` | EntraId gate on provider (idempotent) | all `provision-{app}-auth`; **RequiredBy** Aspire `deploy` | Gate only | Dependency failure |

`{app}` is the Auth app resource name slug (same dash rules as other Neox steps).

#### `prereq-{providerResource}-auth` behavior (tenant)

1. Prompt `{provider}-tenant-id` as Choice + `AllowCustomChoice` of ARM tenants; Label = `Entra tenant — {providerResourceName}`.
2. After resolution, persist the tenant value to deployment state section `Parameters:{parameterName}` via `SetValue` (Aspire-compatible).

#### `prereq-{app}-auth` behavior

1. Prompt `{provider}-{app}-client-id` as Choice + `AllowCustomChoice`: option **Create new application** (sentinel), options for existing app registrations in the **already resolved** tenant (Graph `Applications`, key = ClientId / `appId`, label = `DisplayName — appId`, cap 200), or paste a custom ClientId GUID. Prompt Label = `Entra app — {appName} ({displayName})`.
2. Prefetch uses a tenant-scoped management credential; on Graph failure / missing `Application.Read.All`, Options fall back to Create only (custom GUID still via Other).
3. `DisplayName` is a required argument to `AddAppRegistration` (not an Aspire parameter).
4. Create sentinel must not be persisted as a real ClientId; plan/provision treat it as create path.
5. After resolution, persist ClientId only when it is not the create sentinel (`Parameters:{parameterName}` + `SetValue`).

#### `plan-{app}-auth` behavior

1. Resolve ClientId (GUID vs create sentinel).
2. If GUID: GET application by `appId`; fail if missing.
3. If create path: find-by-DisplayName for idempotence; if found, compare; else plan `CreateApplication`.
4. Compare desired `DisplayName` vs existing → plan `UpdateDisplayName` when it differs.
5. Resolve desired redirect URIs (`WithRedirectUri` / `WithLocalhostRedirectUri`); compare Web/Spa/Native buckets → plan `UpdateRedirectUris` when they differ (`AuthApplicationType.Api` ignored).
6. Resolve desired `signInAudience` from `WithSupportedAccounts` (default `AzureADMyOrg`); compare vs existing → plan `UpdateSignInAudience` when it differs.
7. Resolve desired identifier URIs (`WithApiExposition`; deferred `api://{ClientId}` stays unresolved on create-path plan until ClientId known, or compared when adopt ClientId is known), scopes (`AddScope*`), and app roles (`WithAppRoleExposition`); compare vs existing → plan `UpdateIdentifierUris` / `UpdateOauth2PermissionScopes` / `UpdateAppRoles` when they differ (upsert by URI / scope `value` / role `value`; do not delete Graph entries outside the model).
8. Resolve desired `requiredResourceAccess` from `WithApiPermission`; compare vs existing → plan `UpdateRequiredResourceAccess` when they differ (upsert; do not delete entries outside the model).
9. Attach `AuthAppRegistrationPlan` on the resource. **No** PATCH/POST/password.

#### `provision-{app}-auth` behavior

1. Apply plan actions only: create application (`DisplayName` + Neox marker + redirect URIs + `signInAudience`), then set deferred identifier URI `api://{AppId}` / scopes / appRoles when planned; and/or PATCH display name / redirect URIs / `signInAudience` / identifier URIs / scopes / appRoles / `requiredResourceAccess`, or bind existing.
2. Persist ClientId / TenantId into parameters and deployment state (`Parameters:{parameterName}` + `SetValue`, never the create sentinel). No client-secret create/rotate in this revision.
3. Do not re-diff Graph; trust the plan from `plan-{app}-auth` (recompute only if annotation missing).

### Interactive vs non-interactive

| Mode | Behavior |
|------|----------|
| Interactive | Unresolved required parameters prompted via `IInteractionService` / `ParameterProcessor` (DomainOps-style) |
| Non-interactive | Fail fast unless `Parameters__*` / Azure management credential present; `--non-interactive` on `aspire deploy` / `aspire do` |

### Target package layout

```
src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/
  AuthProviderExtensions.cs
  AuthOpsResource.cs            # shared auth-ops marker resource
  AuthOpsResourceBase.cs        # provider base (slug, apps, authority)
  AuthOpsExtensions.cs          # WithAuth, plan/provision/prereq step names, prereq-providers-auth gate
  Apps/AuthAppResource.cs
  README.md

src/hosting/Neox.Aspire.Hosting.Auth.EntraId/
  EntraAuthProviderBuilderExtensions.cs  # .Entra(...) → EntraAuthOpsResource
  EntraAuthOpsExtensions.cs              # prereq / plan / provision / deploy-auth
  Provider/EntraAuthOpsResource.cs
  Provider/EntraAuthProviderOptions.cs   # TenantId = ParameterResource?
  Provisioning/EntraGraphAppProvisioner.cs
  Provisioning/AuthAppRegistrationPlan.cs
  Provisioning/EntraTenantEnumerator.cs  # ARM tenant Choice options
  Provisioning/EntraAppRegistrationEnumerator.cs  # Graph apps in tenant for ClientId Choice
  Provisioning/EntraAppRegistrationParameterPrompt.cs  # ClientId Choice adopt-or-create
  README.md
```

### Implementation sequencing

1. Spec status → `defined` (this revision: `AddAppRegistration` + `plan|provision-{app}-auth`).
2. Abstractions + EntraId API/pipeline/plan-provision split.
3. Update tests/READMEs; build + unit tests.
4. Status → `implemented`.

This revision (`WithApiExposition` / `WithAppRoleExposition` / `WithApiPermission`) is **`implemented`**.
