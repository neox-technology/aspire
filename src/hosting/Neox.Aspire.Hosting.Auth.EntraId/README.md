# Neox.Aspire.Hosting.Auth.EntraId

Aspire hosting helpers (**AuthOps**) that provision Entra ID app registrations via Microsoft Graph and inject workload credentials into resources as generic environment variables (`AUTH_ENTRA_*`).

Depends on [`Neox.Aspire.Hosting.Auth.Abstractions`](../Neox.Aspire.Hosting.Auth.Abstractions/README.md).

## Quick start

```csharp
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra(); // creates parameter auth-provider-entra-tenant-id (Choice of accessible tenants)

// Or bind an existing parameter:
// .Entra(o => o.TenantId = builder.AddParameter("my-tenant"));

var web = entra.AddAppRegistration("web", "MyApp-Local")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc");
// web.WithRedirectUri(AuthApplicationType.Web, "https://contoso.example/signin-oidc");
// web.WithRedirectUri(AuthApplicationType.Spa, builder.AddParameter("public-base-url"), "/");
// Future: web.WithApplicationType(...);

// Redirect URIs are applied on Graph create/adopt during plan|provision-{app}-auth
// (Web → web.redirectUris, Spa → spa.redirectUris, Native → publicClient.redirectUris).

builder.AddProject<Projects.Api>("api")
    .WithAuth(web);
```

Then run `aspire do` / `aspire deploy`. Pipeline steps: `prereq-auth-provider-entra-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth` → `deploy-auth` (`prereq-providers-auth` on shared `auth-ops`; provider prereq and `deploy-auth` on the `EntraAuthOpsResource`; app prereq / plan / provision on each `AuthAppResource`).

The tenant parameter prompts as a **Choice** combobox (dashboard / CLI) listing Entra tenants the current Azure credential can access (`AllowCustomChoice` for a manual GUID). The prompt label is `Entra tenant — {providerResourceName}` so multiple providers stay distinguishable.

Each app **ClientId** parameter (`{provider}-{app}-client-id`) prompts as a **Choice** after the tenant is resolved: existing app registrations in that tenant (Graph, label `DisplayName — appId`, up to 200), **Create new application** (uses the `displayName` argument to `AddAppRegistration` — not an Aspire parameter), or enter a custom Client ID GUID (`AllowCustomChoice`). The prompt label is `Entra app — {appName} ({displayName})`. Listing requires management Graph permission `Application.Read.All`; on failure the Choice falls back to Create + Other only.

After interactive resolution (and again after `provision-{app}-auth`), AuthOps persists tenant / ClientId into Aspire deployment state under `Parameters:{parameterName}` (same contract as Aspire `ParameterProcessor`) so a later `aspire do` does not re-prompt. The create sentinel is never persisted as ClientId — only the real app id after provision.

`plan-{app}-auth` resolves create vs existing (read-only Graph) and compares desired DisplayName and redirect URIs. `provision-{app}-auth` applies that plan (create includes DisplayName + redirect URIs; adopt may patch DisplayName and/or redirect URIs).

## Environment variables

Single app under the provider:

| Env | Aspire parameter (CI) |
|-----|------------------------|
| `AUTH_ENTRA_TENANT_ID` | `Parameters__auth-provider-entra-tenant-id` |
| `AUTH_ENTRA_CLIENT_ID` | `Parameters__auth-provider-entra-web-client-id` |
| `AUTH_ENTRA_CLIENT_SECRET` | `Parameters__auth-provider-entra-web-client-secret` (only when `IncludeClientSecret = true`) |
| `AUTH_ENTRA_AUTHORITY` | derived |

Multiple apps: `AUTH_ENTRA_{APP}_*` (app slug uppercased).

`CLIENT_SECRET` is omitted by default. Emit it with `WithAuth(..., env => env.IncludeClientSecret = true)`.

Override prefix / mapping with `WithAuth(app, env => { env.Prefix = "..."; })`.

## Adopt an existing registration

Choose an existing app in the ClientId Choice prompt, or set `Parameters__{provider}-{app}-client-id` to the ClientId GUID. Plan compares DisplayName; provision binds (and patches DisplayName when it differs). No client-secret create/rotate in this revision.

## Management vs workload credentials

| Concern | Source |
|---------|--------|
| Management (Graph plan/provision) | `ITokenCredentialProvider` / `DefaultAzureCredential` (`az login` / CI federated) |
| Workload (ClientId / secret) | Parameters injected as `AUTH_*` — never logged into manifests |

## Sample AppHost

Smoke sample under `tests/auth-providers/sample-apphost/`:

- Auth apps: `AddAppRegistration("auth-appregistration-web", …)` / `AddAppRegistration("auth-appregistration-spa", …)` (names must differ from Aspire workload resources)
- Workloads: Blazor Server `blazor` (`AUTH_ENTRA_*`) and Vite/React `ops` (`VITE_ENTRA_*` via `WithAuth` maps)

Build with `dotnet build` — no live Graph in CI.

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
