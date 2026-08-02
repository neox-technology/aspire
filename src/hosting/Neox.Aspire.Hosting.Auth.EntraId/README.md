# Neox.Aspire.Hosting.Auth.EntraId

Aspire hosting helpers (**AuthOps**) that provision Entra ID app registrations via Microsoft Graph and inject workload credentials as Microsoft.Identity.Web environment variables (`AzureAd__*`).

Depends on [`Neox.Aspire.Hosting.Auth.Abstractions`](../Neox.Aspire.Hosting.Auth.Abstractions/README.md).

## Quick start

```csharp
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra(); // creates parameter auth-provider-entra-tenant-id (Choice of accessible tenants)

var web = entra.AddAppRegistration("web", "MyApp-Local")
    .WithClientSecret() // opt-in: emit AzureAd__ClientSecret + UI create when app exists
    .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc") // scheme: Https default; use Both for http+https
    .WithSupportedAccounts(SupportedAccountsType.SingleTenant);

builder.AddProject<Projects.Api>("api")
    .WithAuth(web);
```

Then run `aspire do` / `aspire deploy`. Pipeline steps: `prereq-auth-provider-entra-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth` → `deploy-auth` on each `EntraAuthAppRegistrationResource`.

## Client secret (`WithClientSecret`)

```csharp
// Default: auto parameter {provider}-{app}-client-secret under child {app}-clientsecret
web.WithClientSecret();

// Optional override (may include a default value)
var secret = builder.AddParameter("web-client-secret", secret: true);
web.WithClientSecret(secret);
```

Adds dashboard child resource `{app}-clientsecret` (e.g. `appregistration-api-clientsecret`) under the Auth app.

| Context | Behavior |
|---------|----------|
| Pipeline / CI | Resolves the secret parameter in memory for env when already provided (`Parameters__*` / deployment state). Does **not** call Graph `addPassword`. |
| AppHost run (dashboard) | When `{app}-clientsecret` is Waiting (parent Healthy, secret empty): notification as soon as InteractionService is available → prompt for secret **name** + **lifetime** (6 / 12 / 24 months) → Graph `addPassword` (`EndDateTime`) → persist one-shot value into AppHost secrets. Command **Create client secret** on `{app}-clientsecret` re-triggers the same path. |

## Dashboard status (local run)

In Aspire run mode, Entra AuthOps resources start as **Waiting**, then publish **Running** + Healthy/Unhealthy from Microsoft Graph probes:

- Scopes / app roles / API permissions (`{app}-apiperm-{value}`) — present on the Graph app (and Waiting while the parent app registration is not Healthy); API permissions check consumer `requiredResourceAccess`
- `{app}-clientsecret` — Waiting until parent Healthy and secret set; Healthy when secret is in AppHost (not part of parent worst-wins)
- App registrations — ClientId set and app exists; children (scopes, roles, API permissions) aggregated with worst-wins (Unhealthy > Waiting > Healthy)
- Provider — TenantId set; aggregates app registrations
- `auth-ops` — aggregates Entra providers

Each Entra app registration exposes a dashboard command **Provision app registration** (`provision-auth`) that runs the same plan + provision path as the pipeline, then refreshes status.

## Environment variables (Microsoft.Identity.Web)

| Env | Notes |
|-----|--------|
| `AzureAd__Instance` | `https://login.microsoftonline.com/` (omit with `IncludeInstance = false`) |
| `AzureAd__TenantId` | from tenant parameter |
| `AzureAd__ClientId` | from app ClientId parameter |
| `AzureAd__ClientSecret` | when `WithClientSecret` is used, or `IncludeClientSecret = true` |

SPA escape hatch:

```csharp
.WithAuth(spa, env =>
{
    env.IncludeInstance = false;
    env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
    env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
});
```

## Sample AppHost

Under `tests/auth-providers/sample-apphost/`:

- WebAPI `api` — `GET /me` via Microsoft Graph
- Blazor `blazor` — Microsoft.Identity.Web login + call `/me`
- Vite `ops` — MSAL login + call `/me`

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
