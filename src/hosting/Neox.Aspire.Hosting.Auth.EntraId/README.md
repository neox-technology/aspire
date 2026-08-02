# Neox.Aspire.Hosting.Auth.EntraId

Aspire hosting helpers (**AuthOps**) that provision Entra ID app registrations via Microsoft Graph and inject workload credentials as Microsoft.Identity.Web environment variables (`AzureAd__*`).

Depends on [`Neox.Aspire.Hosting.Auth.Abstractions`](../Neox.Aspire.Hosting.Auth.Abstractions/README.md).

## Quick start

```csharp
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra(); // creates parameter auth-provider-entra-tenant-id (Choice of accessible tenants)

var web = entra.AddAppRegistration("web", "MyApp-Local")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc")
    .WithSupportedAccounts(SupportedAccountsType.SingleTenant);

builder.AddProject<Projects.Api>("api")
    .WithAuth(web, env => env.IncludeClientSecret = true);
```

Then run `aspire do` / `aspire deploy`. Pipeline steps: `prereq-auth-provider-entra-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth` → `deploy-auth` on each `EntraAuthAppRegistrationResource`.

## Environment variables (Microsoft.Identity.Web)

| Env | Notes |
|-----|--------|
| `AzureAd__Instance` | `https://login.microsoftonline.com/` (omit with `IncludeInstance = false`) |
| `AzureAd__TenantId` | from tenant parameter |
| `AzureAd__ClientId` | from app ClientId parameter |
| `AzureAd__ClientSecret` | only when `IncludeClientSecret = true` |

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
