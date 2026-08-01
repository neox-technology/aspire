# Neox.Aspire.Hosting.Auth.EntraId

Aspire hosting helpers (**AuthOps**) that provision Entra ID app registrations via Microsoft Graph and inject workload credentials into resources as generic environment variables (`AUTH_ENTRA_*`).

Depends on [`Neox.Aspire.Hosting.Auth.Abstractions`](../Neox.Aspire.Hosting.Auth.Abstractions/README.md).

## Quick start

```csharp
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra(); // creates parameter auth-provider-entra-tenant-id (Choice of accessible tenants)

// Or bind an existing parameter:
// .Entra(o => o.TenantId = builder.AddParameter("my-tenant"));

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

Then run `aspire do` / `aspire deploy`. Pipeline steps: `prereq-auth-provider-entra-auth` → `prereq-providers-auth` → `plan-auth-{app}` → `provision-auth-{app}` → `deploy-auth` (`prereq-providers-auth` on shared `auth-ops`; provider prereq and `deploy-auth` on the `EntraAuthOpsResource`).

The tenant parameter prompts as a **Choice** combobox (dashboard / CLI) listing Entra tenants the current Azure credential can access (`AllowCustomChoice` for a manual GUID).

## Environment variables

Single app under the provider:

| Env | Aspire parameter (CI) |
|-----|------------------------|
| `AUTH_ENTRA_TENANT_ID` | `Parameters__auth-provider-entra-tenant-id` |
| `AUTH_ENTRA_CLIENT_ID` | `Parameters__auth-provider-entra-web-client-id` |
| `AUTH_ENTRA_CLIENT_SECRET` | `Parameters__auth-provider-entra-web-client-secret` |
| `AUTH_ENTRA_AUTHORITY` | derived |

Multiple apps: `AUTH_ENTRA_{APP}_*` (app slug uppercased).

`CLIENT_SECRET` is emitted only when `CreateClientSecret` is true (or `WithAuth(..., env => env.IncludeClientSecret = true)`). SPA / public clients with `CreateClientSecret = false` do not get a secret env var and are not prompted for one after Graph create.

Override prefix / mapping with `WithAuth(app, env => { env.Prefix = "..."; })`.

## Adopt an existing registration

```csharp
entra.AddApp("web", o =>
{
    o.ExistingClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    o.CreateClientSecret = false;
});
```

For confidential clients that still need a workload secret, set `CreateClientSecret = true`, rotate, or `IncludeClientSecret = true` and supply the client-secret parameter. AuthOps validates redirect URIs and does not rotate secrets unless you opt in later.

## Management vs workload credentials

| Concern | Source |
|---------|--------|
| Management (Graph create/update) | `ITokenCredentialProvider` / `DefaultAzureCredential` (`az login` / CI federated) |
| Workload (ClientId / secret) | Parameters injected as `AUTH_*` — never logged into manifests |

## Sample AppHost

Smoke sample under `tests/auth-providers/sample-apphost/`:

- Auth apps: `AddApp("auth-appregistration-web")` / `AddApp("auth-appregistration-spa")` (names must differ from Aspire workload resources)
- Workloads: Blazor Server `blazor` (`AUTH_ENTRA_WEB_*`) and Vite/React `ops` (`VITE_ENTRA_*` via `WithAuth` maps)

Build with `dotnet build` — no live Graph in CI.

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
