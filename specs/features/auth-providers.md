# AuthOps — identity provider app registrations

| Field | Value |
|-------|-------|
| Slug | `auth-providers` |
| Status | implemented |
| Last code review | 2026-08-01 |

## Summary

AuthOps is split into two hosting packages:

- **`Neox.Aspire.Hosting.Auth.Abstractions`** — common AuthOps model (`AuthOpsResourceBase`), generic `WithAuth` env injection, shared `AuthOpsResource`, and gate `prereq-providers-auth`.
- **`Neox.Aspire.Hosting.Auth.EntraId`** — Entra ID provider (`AddAuthProvider` → `.Entra(...)` → `EntraAuthOpsResource` / `AddApp`), Graph provisioning, provider prereq `prereq-{providerResource}-auth`, app prereq `prereq-{app}-auth` (ClientId Choice: Create + apps in tenant + custom GUID), and `deploy-auth` on the provider resource.

Consumers reference **`Neox.Aspire.Hosting.Auth.EntraId`** (pulls Abstractions transitively). The former package id **`Neox.Aspire.Hosting.Auth` is retired** (breaking). Namespace remains `Neox.Aspire.Hosting.Auth` in both assemblies.

**v1 decisions (frozen):**

- Provisioning: **Entra only** (create/update application + client secret). Google / GitHub / generic OAuth2 are out of scope for v1.
- Secret injection: **generic env only** (no ASP.NET Core `Authentication__Schemes__*` mapping).
- Model mirrors DomainOps: provider resource → app registration → pipeline plan/provision → consumer bind.

## User scenarios

- AppHost registers `AddAuthProvider("auth-provider-entra").Entra(...)`, defines one or more apps with `AddApp(...)`, binds consumers with `WithAuth(app)`; local interactive `aspire do` prompts unresolved parameters (tenant via Choice of accessible tenants; ClientId via Choice of Create + app registrations in that tenant + custom GUID); CI supplies `Parameters__*` / Azure credential with `--non-interactive`.
- **Create path:** `prereq-{app}-auth` Choice selects create (or ClientId unset) → `provision-auth-{app}` creates the Entra application using `AuthAppOptions.DisplayName` (+ optional password credential), writes ClientId into the Aspire parameter, then `WithAuth` wires `AUTH_ENTRA_*` env on the consumer.
- **Adopt path:** ClientId parameter set (interactive Choice / `Parameters__*` / `ExistingClientId` default) → Graph GET + validate redirect URIs / model against plan; **do not** rotate client secret unless an explicit option requests rotation; bind existing ClientId/TenantId (and secret parameter if already provided).
- **Idempotent re-run:** provision finds the app via deployment state (`Auth:Entra:{app}`) and/or DisplayName + Neox extension property/tag; updates redirect URIs when the plan differs.
- Management credentials (Graph) stay separate from workload ClientId/ClientSecret; secrets are never logged or written into manifests.
- Unit tests under `tests/auth-providers/` use fakes (no live Graph / no `az`).

## Routes (if UI)

None — `aspire do` / `aspire deploy` pipeline steps only. No dashboard `WithCommand` in v1.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting` (Abstractions: resources, parameters, pipelines, `IInteractionService`)
- `Aspire.Hosting.Azure`, Azure.Identity, Microsoft Graph (EntraId only) for application CRUD + password credentials
- Aspire Azure credential surface where available (`ITokenCredentialProvider` / equivalent) for **management** Graph calls; CI may use app-only Graph separately from workload secrets
- Patterns: DomainOps (`AddDomainOpsProvider`, `WithPipelineStepFactory`, parameter prompts) in [`azure-custom-domains`](azure-custom-domains.md)

## Out of scope

- Google / GitHub / generic OAuth2 providers (API not exposed in v1; reserved for later adopt-or-provision)
- ASP.NET Core authentication scheme config (`AddMicrosoftIdentityWebApp`, `Authentication__Schemes__*`)
- Azure managed identity (`AddAzureUserAssignedIdentity`) — different concern
- Key Vault sync of workload secrets (candidate phase 2)
- Dedicated Dashboard UI
- Automatic secret rotation on every deploy (opt-in only if added later)
- Multi-tenant / CIAM / External ID–specific flows beyond a single Entra tenant app registration
- Meta-package / type-forwarding shim retaining package id `Neox.Aspire.Hosting.Auth`

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Auth.Abstractions` under `src/hosting/Neox.Aspire.Hosting.Auth.Abstractions/` (Aspire.Hosting only).
- [x] Package `Neox.Aspire.Hosting.Auth.EntraId` under `src/hosting/Neox.Aspire.Hosting.Auth.EntraId/` (refs Abstractions + Graph/Azure).
- [x] Former package id `Neox.Aspire.Hosting.Auth` removed (no shipping assembly with that id).
- [x] `AddAuthProvider(name)` lives in Abstractions; `.Entra(configure)` is an EntraId extension on `IAuthProviderBuilder`.
- [x] `AddApp(name, configure)` models an Entra app registration (`AuthAppResource`) with Web / Spa / Api / Native, redirect URIs, optional identifier URIs, create-secret flag, optional `ExistingClientId`.
- [x] `WithAuth(app)` / `WithAuth(app, env => …)` injects only generic `AUTH_*` (or custom prefix) via `WithEnvironment` late-bound to parameters — never resolves secrets at model build time; Authority uses provider `AuthorityFormatter` (Entra sets `login.microsoftonline.com`).
- [x] Pipeline steps match **Pipeline step contracts** below; tags include `auth-ops`; `deploy-auth` is required by Aspire `deploy`.
- [x] Shared `AuthOpsResource` (`auth-ops`) hosts noop gate `prereq-providers-auth` (fan-in of all `prereq-{providerResource}-auth`).
- [x] `.Entra(...)` creates `EntraAuthOpsResource` (: `AuthOpsResourceBase`); provider prereq is `prereq-{providerResource.Name}-auth` (e.g. `prereq-auth-provider-entra-auth`).
- [x] `EntraAuthProviderOptions.TenantId` is `IResourceBuilder<ParameterResource>?`; auto-creates `{name}-tenant-id` when null; parameter uses `WithCustomInput` Choice (+ `AllowCustomChoice`) of ARM tenants.
- [x] Per Auth app, `prereq-{app}-auth` **DependsOn** `prereq-providers-auth`; ClientId parameter Choice (create sentinel + `AllowCustomChoice` GUID); `ExistingClientId` seeds the default; `DisplayName` stays on `AuthAppOptions` only (not a ParameterResource).
- [x] `prereq-{app}-auth` ClientId Choice prefetches Graph app registrations in the selected tenant (Create + listed apps + custom GUID); empty list on Graph failure / missing `Application.Read.All` (fallback Create + Other only).
- [x] `plan-auth-{app}` **DependsOn** `prereq-{app}-auth` (not directly `prereq-providers-auth`).
- [x] Provision adopt uses resolved ClientId parameter GUID (or `ExistingClientId`); create uses `options.DisplayName`; create sentinel is never persisted as ClientId.
- [x] Management vs workload credentials are separated; workload secrets use `ParameterResource` with `secret: true`.
- [x] Interactive prompts for missing params when `IInteractionService` is available; otherwise require `Parameters__*` / credential config (CI).
- [x] Unit tests with Graph fakes; EntraId package README for consumers.
- [x] Unit tests assert AuthOps pipeline graph (`prereq-{resource}-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-auth-*`).
- [x] Sample AppHost (`tests/auth-providers/sample-apphost/`) wires Blazor Web + Vite Spa via AuthOps; Auth app resource names stay distinct from workload resources (`blazor`/`ops`); CI smoke is `dotnet build` only (no live Graph).
- [x] `WithAuth` / provision skip client secret when `CreateClientSecret` is false (SPA / public client) unless `IncludeClientSecret` overrides.
- [x] Glossary terms for AuthOps promoted in [`domain-glossary`](domain-glossary.md).

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
| App API | `AddApp`, `AuthAppResource` / options |
| Binding API | `WithAuth`, env mapping options |
| Provisioner | `EntraGraphAppProvisioner` (Microsoft Graph) in EntraId |
| Unit tests | `tests/auth-providers/` |
| Sample AppHost | `tests/auth-providers/sample-apphost/` (Blazor Server + Vite ops SPA; smoke `dotnet build` only) |
| Sample Blazor | `tests/auth-providers/sample-blazor/` — workload `blazor`; Auth app `web` → `AUTH_ENTRA_WEB_*` |
| Sample ops | `tests/auth-providers/sample-ops/` — workload `ops`; Auth app `spa`; `WithAuth` maps `VITE_ENTRA_*` |
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

var webAppReg = entra.AddApp("web", o =>
{
    o.DisplayName = "MyApp-Local";
    o.ApplicationType = AuthApplicationType.Web; // Web | Spa | Api | Native
    o.RedirectUris = ["https://localhost:7001/signin-oidc"];
    o.IdentifierUris = ["api://myapp"]; // when Api
    o.CreateClientSecret = true;
    // Adopt: o.ExistingClientId = "..."; // validate + bind; no create; no secret rotate by default
});

var api = builder.AddProject<Projects.Api>("api")
    .WithAuth(webAppReg);

api.WithAuth(webAppReg, env =>
{
    env.Prefix = "AUTH_ENTRA"; // default from provider slug
    // env.Map(AuthOutput.ClientId, "MY_CUSTOM_CLIENT_ID"); // escape hatch
});
```

### Environment naming (generic)

Convention: `AUTH_{PROVIDER_SLUG}_{SETTING}` with provider slug uppercased.

| Setting | Default env (Entra) | Aspire parameter (dash) |
|---------|---------------------|-------------------------|
| Tenant id | `AUTH_ENTRA_TENANT_ID` | `{provider}-tenant-id` (e.g. `entra-tenant-id`) |
| Client id | `AUTH_ENTRA_CLIENT_ID` | `{provider}-client-id` or `{provider}-{app}-client-id` when multiple apps |
| Client secret | `AUTH_ENTRA_CLIENT_SECRET` | `{provider}-client-secret` / `{provider}-{app}-client-secret` (`secret: true`) |
| Authority | `AUTH_ENTRA_AUTHORITY` | Derived via provider `AuthorityFormatter` (`https://login.microsoftonline.com/{tenant}` for Entra) — optional emit |
| Redirect URI | `AUTH_ENTRA_REDIRECT_URI` | Optional; primary redirect when useful for the consumer |

CI: `Parameters__entra-client-id`, `Parameters__entra-client-secret`, `Parameters__entra-tenant-id` (and app-qualified names when multiple apps share one provider resource).

When multiple `AddApp` instances exist under one provider, parameter and env names **must** include the app slug to avoid collisions (e.g. `AUTH_ENTRA_WEB_CLIENT_ID` **or** keep `AUTH_ENTRA_*` only for a single default app and require `env.Prefix` / `Map` for additional apps). **v1 rule:** if more than one app is registered on the provider, default env vars are `AUTH_ENTRA_{APP}_{SETTING}` (app slug uppercased); single-app keep short `AUTH_ENTRA_{SETTING}`.

### Parameter / credential separation

| Concern | Source | Used by |
|---------|--------|---------|
| Management (create/update app) | `ITokenCredentialProvider` / Graph app-only | `prereq-{providerResource}-auth`, `provision-auth-{app}` only |
| Workload (ClientId / secret / tenant for apps) | Output `ParameterResource`s | `WithAuth` → consumer env |

Never resolve workload secrets at distributed-application model build time. Never write secrets into manifests or generated config files.

### Pipeline step contracts

Shared gate `prereq-providers-auth` is hosted on **`AuthOpsResource`** (`auth-ops`). Provider prereq and `deploy-auth` stay on the provider resource (`EntraAuthOpsResource`).

| Step | Registered by | DependsOn / RequiredBy | Exit 0 | Exit ≠ 0 |
|------|---------------|------------------------|--------|----------|
| `prereq-{providerResource}-auth` | `.Entra(...)` (idempotent; name = Aspire resource name, e.g. `prereq-auth-provider-entra-auth`) | **RequiredBy** `prereq-providers-auth` | Tenant parameter ready (Choice of ARM tenants / custom GUID) | Missing interactive input / credential failure |
| `prereq-providers-auth` | Abstractions on `AuthOpsResource` (idempotent) | Fan-in of all `prereq-{providerResource}-auth` (noop) | Gate only | Dependency failure |
| `prereq-{app}-auth` | `AddApp` on `AuthAppResource` (idempotent) | **DependsOn** `prereq-providers-auth` | ClientId parameter ready (Choice: Create + Graph apps in selected tenant + custom GUID; `ExistingClientId` default); create uses `AuthAppOptions.DisplayName` (options-only) | Missing interactive input |
| `plan-auth-{app}` | `AddApp` / AuthOps bind (EntraId) | **DependsOn** `prereq-{app}-auth` | Desired model validated (display name, type, redirect URIs) | Invalid options |
| `provision-auth-{app}` | `AddApp` / AuthOps (EntraId) | **DependsOn** `plan-auth-{app}` | App exists (created or adopted); workload parameters set; state saved for idempotence | Graph failure / validation failure |
| `deploy-auth` | EntraId gate on provider (idempotent) | all `provision-auth-{app}`; **RequiredBy** Aspire `deploy` | Gate only | Dependency failure |

`{app}` is the Auth app resource name slug (same dash rules as other Neox steps).

#### `prereq-{app}-auth` behavior

1. Prompt `{provider}-{app}-client-id` as Choice + `AllowCustomChoice`: option **Create new application** (sentinel), options for existing app registrations in the **already resolved** tenant (Graph `Applications`, key = ClientId / `appId`, label = `DisplayName — appId`, cap 200), or paste a custom ClientId GUID.
2. Prefetch uses a tenant-scoped management credential; on Graph failure / missing `Application.Read.All`, Options fall back to Create only (custom GUID still via Other).
3. `AuthAppOptions.ExistingClientId` seeds the ClientId parameter default when set in AppHost.
4. `DisplayName` is **not** an Aspire parameter — only `o.DisplayName` on `AddApp` (defaults to app resource name).
5. Create sentinel must not be persisted as a real ClientId; provision treats it as create path.

#### `provision-auth-{app}` behavior

1. **Adopt** (ClientId parameter is a GUID, or `ExistingClientId` set): GET application; diff redirect URIs / identifier URIs against plan; update if needed; do **not** create or rotate password credential unless explicit rotate option; bind ClientId + TenantId (+ existing secret parameter if supplied).
2. **Create** (ClientId unset / create sentinel): POST application using `options.DisplayName`; optionally add password credential when `CreateClientSecret`; persist ClientId / secret / tenant into parameters; record id in deployment state section `Auth:Entra:{app}` (and/or Neox extension property on the application).
3. **Idempotent re-run**: load state or find by DisplayName + Neox marker; update in place; avoid duplicate apps.

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
  Apps/AuthAppResource.cs / options
  README.md

src/hosting/Neox.Aspire.Hosting.Auth.EntraId/
  EntraAuthProviderBuilderExtensions.cs  # .Entra(...) → EntraAuthOpsResource
  EntraAuthOpsExtensions.cs              # prereq-{name}-auth, prereq-{app}-auth, deploy-auth, plan/provision
  Provider/EntraAuthOpsResource.cs
  Provider/EntraAuthProviderOptions.cs   # TenantId = ParameterResource?
  Provisioning/EntraGraphAppProvisioner.cs
  Provisioning/EntraTenantEnumerator.cs  # ARM tenant Choice options
  Provisioning/EntraAppRegistrationEnumerator.cs  # Graph apps in tenant for ClientId Choice
  Provisioning/EntraAppRegistrationParameterPrompt.cs  # ClientId Choice adopt-or-create
  README.md
```

### Implementation sequencing

1. Spec status → `defined` (this revision: ClientId Choice lists Graph apps in selected tenant).
2. Enumerator + prefetch in `EnsureReadyAsync`; wire TenantIdParameter.
3. Update tests/READMEs; build + unit tests.
4. Status → `implemented`.

This revision (ClientId Choice lists Graph apps) is **`implemented`**.
