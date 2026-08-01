# Neox.Aspire.Hosting.Auth

Aspire hosting helpers (**AuthOps**) that provision Entra ID app registrations via Microsoft Graph and inject workload credentials into resources as generic environment variables (`AUTH_ENTRA_*`).

## Quick start

```csharp
var entra = builder.AddAuthProvider("entra")
    .Entra(o => o.TenantId = "00000000-0000-0000-0000-000000000000");

var web = entra.AddApp("web", o =>
{
    o.DisplayName = "MyApp-Local";
    o.ApplicationType = AuthApplicationType.Web;
    o.RedirectUris = ["https://localhost:7001/signin-oidc"];
    o.CreateClientSecret = true;
});

builder.AddProject<Projects.Api>("api")
    .WithAuth(web);
```

Then run `aspire do` / `aspire deploy`. Pipeline steps: `prereq-auth` → `prereq-auth-entra` → `plan-auth-{app}` → `provision-auth-{app}` → `deploy-auth`.

## Environment variables

Single app under the provider:

| Env | Aspire parameter (CI) |
|-----|------------------------|
| `AUTH_ENTRA_TENANT_ID` | `Parameters__entra-tenant-id` |
| `AUTH_ENTRA_CLIENT_ID` | `Parameters__entra-web-client-id` |
| `AUTH_ENTRA_CLIENT_SECRET` | `Parameters__entra-web-client-secret` |
| `AUTH_ENTRA_AUTHORITY` | derived |

Multiple apps: `AUTH_ENTRA_{APP}_*` (app slug uppercased).

Override prefix / mapping with `WithAuth(app, env => { env.Prefix = "..."; })`.

## Adopt an existing registration

```csharp
entra.AddApp("web", o =>
{
    o.ExistingClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    o.CreateClientSecret = false;
});
```

Provide the secret via `Parameters__entra-web-client-secret` when needed. AuthOps validates redirect URIs and does not rotate secrets unless you opt in later.

## Management vs workload credentials

| Concern | Source |
|---------|--------|
| Management (Graph create/update) | `ITokenCredentialProvider` / `DefaultAzureCredential` (`az login` / CI federated) |
| Workload (ClientId / secret) | Parameters injected as `AUTH_*` — never logged into manifests |

## Sample AppHost

Smoke sample under `tests/auth-providers/sample-apphost/`: Blazor Server (`sample-blazor`, Web + client secret) and Vite/React ops SPA (`sample-ops`, Spa + `VITE_ENTRA_*` via `WithAuth` env maps). Build with `dotnet build` — no live Graph in CI.

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
