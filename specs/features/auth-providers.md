# AuthOps — identity provider app registrations

| Field | Value |
|-------|-------|
| Slug | `auth-providers` |
| Status | implemented |
| Last code review | 2026-08-01 |

## Summary

Hosting package `Neox.Aspire.Hosting.Auth` (**AuthOps**) provisions Entra ID app registrations via Microsoft Graph and injects workload credentials into Aspire resources as **generic environment variables** (`AUTH_{PROVIDER}_{SETTING}`). Consumers call `AddAuthProvider` → `.Entra(...)` → `AddApp` → `WithAuth` → `aspire do` / `aspire deploy`.

**v1 decisions (frozen):**

- Provisioning: **Entra only** (create/update application + client secret). Google / GitHub / generic OAuth2 are out of scope for v1.
- Secret injection: **generic env only** (no ASP.NET Core `Authentication__Schemes__*` mapping).
- Model mirrors DomainOps: provider resource → app registration → pipeline plan/provision → consumer bind.

## User scenarios

- AppHost registers `AddAuthProvider("entra").Entra(...)`, defines one or more apps with `AddApp("web", ...)`, binds consumers with `WithAuth(app)`; local interactive `aspire do` prompts unresolved parameters; CI supplies `Parameters__*` / Azure credential with `--non-interactive`.
- **Create path:** no `ExistingClientId` → `provision-auth-{app}` creates the Entra application (+ optional password credential), writes workload outputs into Aspire `ParameterResource`s (`secret: true` for secrets), then `WithAuth` wires `AUTH_ENTRA_*` env on the consumer.
- **Adopt path:** `ExistingClientId` set → Graph GET + validate redirect URIs / model against plan; **do not** rotate client secret unless an explicit option requests rotation; bind existing ClientId/TenantId (and secret parameter if already provided).
- **Idempotent re-run:** provision finds the app via deployment state (`Auth:Entra:{app}`) and/or DisplayName + Neox extension property/tag; updates redirect URIs when the plan differs.
- Management credentials (Graph) stay separate from workload ClientId/ClientSecret; secrets are never logged or written into manifests.
- Unit tests under `tests/auth-providers/` use fakes (no live Graph / no `az`).

## Routes (if UI)

None — `aspire do` / `aspire deploy` pipeline steps only. No dashboard `WithCommand` in v1.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting` (resources, parameters, pipelines, `IInteractionService`)
- Microsoft Graph (SDK or minimal HTTP client) for application CRUD + password credentials
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

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Auth` under `src/hosting/Neox.Aspire.Hosting.Auth/`.
- [x] `AddAuthProvider(name).Entra(configure)` registers a non-container `AuthProviderResource` (slug `entra`).
- [x] `AddApp(name, configure)` models an Entra app registration (`AuthAppResource` or equivalent) with Web / Spa / Api / Native, redirect URIs, optional identifier URIs, create-secret flag, optional `ExistingClientId`.
- [x] `WithAuth(app)` / `WithAuth(app, env => …)` injects only generic `AUTH_ENTRA_*` (or custom prefix) via `WithEnvironment` late-bound to parameters — never resolves secrets at model build time.
- [x] Pipeline steps match **Pipeline step contracts** below; tags include `auth-ops`; `deploy-auth` is required by Aspire `deploy`.
- [x] Management vs workload credentials are separated; workload secrets use `ParameterResource` with `secret: true`.
- [x] Interactive prompts for missing params when `IInteractionService` is available; otherwise require `Parameters__*` / credential config (CI).
- [x] Unit tests with Graph fakes; package README for consumers.
- [x] Sample AppHost (`tests/auth-providers/sample-apphost/`) wires Blazor Web + Vite Spa via AuthOps; CI smoke is `dotnet build` only (no live Graph).
- [x] Glossary terms for AuthOps promoted in [`domain-glossary`](domain-glossary.md).

## Terminology

See [`domain-glossary`](domain-glossary.md). Feature-local API names below until promoted.

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Auth/` |
| Package id | `Neox.Aspire.Hosting.Auth` |
| Namespace | `Neox.Aspire.Hosting.Auth` |
| Product vocabulary | AuthOps |
| Provider API | `AddAuthProvider`, `AuthProviderResource`, `.Entra(...)` |
| App API | `AddApp`, `AuthAppResource` / options |
| Binding API | `WithAuth`, env mapping options |
| Provisioner | `EntraGraphAppProvisioner` (Microsoft Graph) |
| Unit tests | `tests/auth-providers/` |
| Sample AppHost | `tests/auth-providers/sample-apphost/` (Blazor Server + Vite ops SPA; smoke `dotnet build` only) |
| Sample Blazor | `tests/auth-providers/sample-blazor/` — Web app registration + `AUTH_ENTRA_BLAZOR_*` |
| Sample ops | `tests/auth-providers/sample-ops/` — Spa app registration; `WithAuth` maps `VITE_ENTRA_*` |
| Pipeline tags | `["auth-ops"]` |

### Proposed AppHost surface (v1)

```csharp
var entra = builder.AddAuthProvider("entra")
    .Entra(o =>
    {
        // Tenant for the app registration; may bind a ParameterResource.
        o.TenantId = "...";
        // Management uses ITokenCredentialProvider / az login locally;
        // CI: Azure__* or app-only Graph — never the workload client secret.
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
| Authority | `AUTH_ENTRA_AUTHORITY` | Derived (`https://login.microsoftonline.com/{tenant}`) — optional emit |
| Redirect URI | `AUTH_ENTRA_REDIRECT_URI` | Optional; primary redirect when useful for the consumer |

CI: `Parameters__entra-client-id`, `Parameters__entra-client-secret`, `Parameters__entra-tenant-id` (and app-qualified names when multiple apps share one provider resource).

When multiple `AddApp` instances exist under one provider, parameter and env names **must** include the app slug to avoid collisions (e.g. `AUTH_ENTRA_WEB_CLIENT_ID` **or** keep `AUTH_ENTRA_*` only for a single default app and require `env.Prefix` / `Map` for additional apps). **v1 rule:** if more than one app is registered on the provider, default env vars are `AUTH_ENTRA_{APP}_{SETTING}` (app slug uppercased); single-app keep short `AUTH_ENTRA_{SETTING}`.

### Parameter / credential separation

| Concern | Source | Used by |
|---------|--------|---------|
| Management (create/update app) | `ITokenCredentialProvider` / Graph app-only | `prereq-auth*`, `provision-auth-{app}` only |
| Workload (ClientId / secret / tenant for apps) | Output `ParameterResource`s | `WithAuth` → consumer env |

Never resolve workload secrets at distributed-application model build time. Never write secrets into manifests or generated config files.

### Pipeline step contracts

| Step | Registered by | DependsOn | Exit 0 | Exit ≠ 0 |
|------|---------------|-----------|--------|----------|
| `prereq-auth` | `AddAuthProvider` core | (none hard) — credential/Graph client obtainable | Management credential ready | Missing interactive input / credential failure |
| `prereq-auth-entra` | `.Entra(...)` (optional, idempotent) | `prereq-auth` | Tenant reachable / Graph scopes OK | Tenant or Graph permission failure |
| `plan-auth-{app}` | `AddApp` / AuthOps bind | `prereq-auth-entra` (or `prereq-auth`) | Desired model validated (display name, type, redirect URIs) | Invalid options |
| `provision-auth-{app}` | `AddApp` / AuthOps | `plan-auth-{app}` | App exists (created or adopted); workload parameters set; state saved for idempotence | Graph failure / validation failure |
| `deploy-auth` | AuthOps (idempotent gate) | all `provision-auth-{app}`; **RequiredBy** Aspire `deploy` | Gate only | Dependency failure |

`{app}` is the Auth app resource name slug (same dash rules as other Neox steps).

#### `provision-auth-{app}` behavior

1. **Adopt** (`ExistingClientId` set): GET application; diff redirect URIs / identifier URIs against plan; update if needed; do **not** create or rotate password credential unless explicit rotate option; bind ClientId + TenantId (+ existing secret parameter if supplied).
2. **Create**: POST application; optionally add password credential when `CreateClientSecret`; persist ClientId / secret / tenant into parameters; record id in deployment state section `Auth:Entra:{app}` (and/or Neox extension property on the application).
3. **Idempotent re-run**: load state or find by DisplayName + Neox marker; update in place; avoid duplicate apps.

### Interactive vs non-interactive

| Mode | Behavior |
|------|----------|
| Interactive | Unresolved required parameters prompted via `IInteractionService` / `ParameterProcessor` (DomainOps-style) |
| Non-interactive | Fail fast unless `Parameters__*` / Azure management credential present; `--non-interactive` on `aspire deploy` / `aspire do` |

### Target package layout

```
src/hosting/Neox.Aspire.Hosting.Auth/
  AuthProviderExtensions.cs
  AuthOpsExtensions*.cs
  Provider/
    AuthProviderResource.cs
    EntraAuthProviderBuilder.cs
  Apps/
    AuthAppResource.cs / options
  Provisioning/
    EntraGraphAppProvisioner.cs
  Pipeline/
    AuthOpsOrchestrator.cs   # if shared orchestration needed
  README.md
```

### Implementation sequencing (after status → `defined`)

1. Skeleton package + `AddAuthProvider` / `.Entra` / `AddApp` / `WithAuth` (env wiring, no Graph).
2. Pipeline step registration + parameter naming.
3. `EntraGraphAppProvisioner` (create/adopt/idempotent) + deployment state.
4. Parameter prompts + README + unit tests.
5. Status → `implemented`; expand glossary if needed.

**Do not implement code while this spec remains `draft`.** This spec is **`defined`**; implementation of `Neox.Aspire.Hosting.Auth` may proceed.
