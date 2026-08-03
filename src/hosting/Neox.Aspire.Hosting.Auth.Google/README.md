# Neox.Aspire.Hosting.Auth.Google

Aspire hosting helpers (**AuthOps**) that **adopt/bind** an existing Google OAuth ClientId (and ProjectId) and inject workload credentials as generic environment variables (`AUTH_GOOGLE_*`) via Google-scoped `WithAuth`.

Depends on [`Neox.Aspire.Hosting.Auth.Abstractions`](../Neox.Aspire.Hosting.Auth.Abstractions/README.md).

> **Does not create or patch** oauth clients (IAM or Google Auth Platform). Create Entra-like is out of scope. Redirect URIs on the AppHost are desired-state only (not applied to Google APIs). IAP oauth-clients are out of scope.

## Quick start

```csharp
var google = builder.AddAuthProvider("provider-google")
    .Google(); // creates parameter provider-google-project-id (Choice of ADC-visible projects)

var web = google.AddAppRegistration("web", "MyApp-Local")
    .WithLocalhostRedirectUri(7281, "/signin-oidc"); // model only — not applied to Google

builder.AddProject<Projects.Api>("api")
    .WithAuth(web); // AUTH_GOOGLE_* + WaitFor(web)
```

Then run `aspire do` / `aspire deploy`. Pipeline: `prereq-provider-google-auth` → `prereq-providers-auth` → `prereq-{app}-auth` → `plan-{app}-auth` → `provision-{app}-auth` → `deploy-auth` (shared on `auth-ops`).

Supply ClientId interactively (list + paste) or via `Parameters__provider-google-web-client-id`.

## Prerequisites

- Google Cloud project id (Choice via ADC / `gcloud projects list` fallback)
- Existing OAuth ClientId to bind (created outside AuthOps)
- Optional ADC for project/client listing; CI may set `Parameters__*` only

ProjectId Choice enumeration uses Resource Manager `projects:search` **without** the ADC `quota_project_id` header (that header often returns 403 when `cloudresourcemanager.googleapis.com` is not enabled on the quota project). If REST fails, AuthOps falls back to `gcloud projects list`.

## Environment variables

| Env | Aspire parameter (CI) |
|-----|------------------------|
| `AUTH_GOOGLE_TENANT_ID` | `Parameters__provider-google-project-id` (holds **ProjectId**) |
| `AUTH_GOOGLE_CLIENT_ID` | `Parameters__provider-google-web-client-id` |
| `AUTH_GOOGLE_CLIENT_SECRET` | only when `IncludeClientSecret = true` |
| `AUTH_GOOGLE_AUTHORITY` | `https://accounts.google.com` |

Multi-app: `AUTH_GOOGLE_{APP}_*`.

## Management vs workload

| Concern | Source |
|---------|--------|
| Management (optional list/validate) | ADC / `gcloud` |
| Workload (ClientId / secret) | Parameters injected as `AUTH_*` |

No automatic client-secret create/rotate. No IAM create/patch.

## Spec

See [`specs/features/auth-provider-google.md`](../../../specs/features/auth-provider-google.md).
