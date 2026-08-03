# Glossary — Neox Aspire

| Field | Value |
|-------|-------|
| Slug | `domain-glossary` |
| Status | defined |
| Last code review | 2026-08-01 |

## Summary

Terminology authority for Neox Aspire packages in this **public** repository (`aspire`). Seed terms only; expand as features land.

## Terminology

| Term | Definition |
|------|------------|
| **Neox Aspire packages** | Packable NuGet libraries under the `Neox.Aspire.*` root namespace (hosting and non-hosting), published from this repo to nuget.org. MIT-licensed. |
| **hosting package** | A `Neox.Aspire.Hosting.*` library that AppHosts reference for Aspire resource/extension helpers. Lives under `src/hosting/` when present. |
| **Shipping** | Arcade package output bucket for packages intended for consumers (`artifacts/packages/<Configuration>/Shipping/`). Distinct from non-shipping / internal artifacts. |
| **migration worker** | Reusable non-hosting DI helper (`Neox.Aspire.EntityFrameworkCore.MigrationWorker`) that runs EF Core `MigrateAsync` in a one-shot `BackgroundService`, then stops the host via `AddEfCoreMigrationService<TDbContext>()`. |
| **custom domain ops** | Hosting helpers (`Neox.Aspire.Hosting.Azure.CustomDomains`) that orchestrate ACA custom domain DNS and managed certificates via `aspire do` pipeline steps (plan/provision/deploy split for provider, zone, hostname add, env certs, and resource bind). |
| **DomainOps provider** | Aspire resource (`DomainOpsProviderResource` and **source-generated** subtypes such as Cloudflare/OVH/Route53) that selects an OctoDNS DNS **provider**, holds auth parameter bindings, and drives generated `octodns.yaml` + Docker image choice. Aligns with octoDNS terminology (**provider**, not provisioner). Types are emitted from the versioned OctoDNS provider catalogue. |
| **OctoDNS provider catalogue** | Checked-in JSON (`octodns-providers.json`) listing official `octodns/{flavor}` Docker providers (excluding all/etchosts/dyn) plus README-derived config settings; refreshed manually via `tools/octodns-provider-catalog`. |
| **prereq-domain** | Shared DomainOps pipeline gate registered by `AddDomainOpsProviderCore`; depends on Aspire `provision-{acaEnv.Name}` for the AppHost's `AzureContainerAppEnvironmentResource`. |
| **prereq-domain-{provider}** | Provider-specific DomainOps prereq (slug, e.g. `cloudflare`) that `docker pull`s `octodns/{provider}`; depends on `prereq-domain`. One step per provider slug, shared across resources of that type. |
| **plan-domain-{provider}** | Writes `octodns.yaml` for a provider (zone list from AppHost bindings; secrets as `env/VAR` refs only). |
| **plan-domain-{zone}** | Writes/upserts OctoDNS zone YAML for one registrable domain (aggregated across resources); dump live zone is internal. |
| **provision-domain-{zone}** | Runs OctoDNS sync for a zone (dry-run + Deletes=0 guard internal, then `--doit`); waits for DNS internally. |
| **plan-{env}-certificates** | Inventories managed certificates already on the ACA environment. |
| **provision-{env}-domains** | Gate that depends on all `provision-{resource}-domain-{dom}` steps for an ACA environment (no ARM). |
| **provision-{env}-certificates** | Creates missing managed certificates on the ACA environment in parallel (long wait); requires hostnames already on apps. |
| **plan-{resource}-domain-{dom}** | Prepares/validates the per-resource domain model (hostname, HTTP validation, expected cert name) without ARM calls. `{dom}` is the hostname slug (`.` → `-`). |
| **provision-{resource}-domain-{dom}** | Adds the custom hostname to the Container App without a certificate (`BindingType.Disabled`); no-op if the hostname already exists (does not detach an existing cert). |
| **deploy-{resource}-domain-{dom}** | Binds an existing managed certificate to the Container App hostname (SNI); rebinds if a different cert is already linked. |
| **deploy-domains** | Gate that depends on all `deploy-{resource}-domain-{dom}` steps; required by Aspire `deploy`. |
| **managed certificate** | Free DigiCert TLS certificate issued and renewed by Azure Container Apps for a validated custom domain. |
| **OctoDNS sync** | Applying **upserted** DNS records (create/update only; DomainOps never deletes) by running the official OctoDNS Docker image (`octodns/cloudflare`, `octodns/ovh`, …) with `octodns-dump` then `octodns-sync`, mounting generated config/zones and injecting credentials via container env (`env/VAR` refs in YAML). DomainOps does not treat the zone YAML as full zone ownership. |
| **AuthOps** | Hosting helpers split as `Neox.Aspire.Hosting.Auth.Abstractions` (common model, shared gates, flat redirects) + provider packages (`Neox.Aspire.Hosting.Auth.EntraId`, `Neox.Aspire.Hosting.Auth.Google`) that provision identity-provider **app registrations** / OAuth clients and inject workload credentials via provider-scoped `WithAuth` through `aspire do` / `aspire deploy` pipeline steps. |
| **AuthOpsResourceBase** | Abstract non-container Aspire resource for an identity **provider** (slug, apps, authority formatter); owns provider AuthOps steps (`prereq-{providerResource}-auth`, `deploy-auth`). |
| **EntraAuthOpsResource** | Entra ID AuthOps provider resource (`AuthOpsResourceBase`); created by `.Entra(...)`. |
| **GoogleAuthOpsResource** | Google Cloud AuthOps provider resource (`AuthOpsResourceBase`); created by `.Google(...)`; adopt/bind of an existing ClientId + ProjectId (does not create/patch Google oauth clients). |
| **AuthOpsResource** | Hidden AppHost marker resource (`auth-ops`) that owns shared AuthOps pipeline gates (notably `prereq-providers-auth`). |
| **Auth app registration** | Logical app registration under an Auth provider (`AddAppRegistration` / abstract `AuthAppRegistrationResource`); concrete types are provider-owned (`EntraAuthAppRegistrationResource`, `GoogleAuthAppRegistrationResource`). Does **not** implement Aspire `IResourceWithWaitSupport` (custom Auth resources must not participate in orchestrator WaitForDependencies). Redirect URI desired-state via Abstractions `WithRedirectUri` / `WithLocalhostRedirectUri` (flat list; `LocalhostRedirectScheme` for http/https/both); Entra adds Graph platform buckets via typed overloads (`AuthApplicationType`) plus `WithSupportedAccounts` / API exposition / `WithApiPermission`. In-model `WithApiPermission` records a dashboard WaitFor relationship to the exposer; provider `WithAuth` makes the workload **WaitFor** the Auth app. |
| **IAM oauth client** | Google Cloud IAM `projects.locations.oauthClients` resource (Workforce OAuth app). AuthOps Google may list/get existing clients for Choice/validate; it does not create or patch them. Distinct from Google Auth Platform / IAP oauth clients. |
| **API exposition** | Desired Entra Application ID URI (`identifierUris`) plus exposed OAuth2 permission scopes and/or app roles on an Auth app (`WithApiExposition` / `WithAppRoleExposition`); modeled as Aspire child resources (`ScopeApiExposition`, `AppRoleApiExposition`) under `ApiExposition`. |
| **OAuth2 permission scope** | Exposed delegated permission on an Auth app (`AddScopeWithAdminConsent` / `AddScopeWithAdminAndUserConsent`); Graph `api.oauth2PermissionScopes`. |
| **App role** | Exposed application role on an Auth app (`WithAppRoleExposition`); Graph `appRoles`; member types UsersAndGroups / Applications / Both. |
| **API permission** | Consumer Auth app dependency on an exposed scope or app role (`WithApiPermission` against an in-model `ApiExposition`) **or** a **well-known API permission** (first-party Microsoft Graph); Graph `requiredResourceAccess` (type Scope or Role). Modeled as Aspire child resource `ApiPermissionResource` (`AuthApiPermission`) under the consumer Auth app (`{consumer}-apiperm-{value}`). In-model consumption also adds Aspire `WaitFor` from consumer Auth app to exposer Auth app (in addition to provision `DependsOn`). |
| **Well-known API permission** | First-party Microsoft Graph delegated scope or application role bound via `WithApiPermission(WellKnownApiPermission)` using source-generated `MicrosoftGraph.Delegated` / `MicrosoftGraph.Application` constants; `ResourceAppId` is the Microsoft Graph app id (`00000003-0000-0000-c000-000000000000`). Creates an `AuthApiPermission` dashboard child like in-model permissions; does not add exposer `DependsOn` or `WaitFor`. |
| **Microsoft Graph permission catalogue** | Checked-in JSON (`microsoft-graph-permissions.json`) listing Graph `oauth2PermissionScopes` (delegated) and `appRoles` (application); refreshed manually via `tools/microsoft-graph-permissions-catalog`; consumed at build time by `Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal`. |
| **prereq-{providerResource}-auth** | Provider-specific AuthOps prereq named from the Aspire provider resource name (e.g. `prereq-auth-provider-entra-auth`); ensures tenant parameter ready (Choice of accessible tenants); **RequiredBy** `prereq-providers-auth`. |
| **prereq-providers-auth** | Shared AuthOps noop gate on `AuthOpsResource`; fan-in of all `prereq-{providerResource}-auth` so every registered provider is authenticated before per-app prereqs. |
| **prereq-{app}-auth** | Per Auth app prereq on `AuthAppRegistrationResource` (e.g. `prereq-web-auth`); **DependsOn** `prereq-providers-auth`; ensures ClientId parameter ready (Choice: Create new application, existing apps in the selected tenant via Graph, or custom GUID); create uses `AddAppRegistration` display name (not an Aspire parameter). |
| **plan-{app}-auth** | Read-only Graph resolve + desired-vs-existing compare for one Auth app; attaches an apply plan (no mutating Graph writes); **DependsOn** `prereq-{app}-auth`. |
| **provision-{app}-auth** | Applies the Auth app plan via Microsoft Graph (create with DisplayName + redirect URIs + `signInAudience` + identifier URIs / scopes / appRoles, patch those and/or `requiredResourceAccess`, or noop bind), sets workload ClientId/TenantId `ParameterResource`s; **DependsOn** `plan-{app}-auth` (and exposer provision when consuming another Auth app's exposition). |
| **deploy-auth** | Shared AuthOps deploy gate on `AuthOpsResource` (`auth-ops`); depends on all `provision-{app}-auth` steps across providers; required by Aspire `deploy`. |
| **AUTH_ env convention** | Google (and non-Identity.Web) consumer injection `AUTH_{PROVIDER_SLUG}_{SETTING}` (e.g. `AUTH_GOOGLE_CLIENT_ID`); with multiple apps under one provider, `AUTH_{PROVIDER}_{APP}_{SETTING}`. |
| **AzureAd__ env convention** | Entra `WithAuth` default injection aligned with Microsoft.Identity.Web (`AzureAd__Instance`, `AzureAd__TenantId`, `AzureAd__ClientId`, optional `AzureAd__ClientSecret`). SPA samples may `Map` to `VITE_ENTRA_*`. |
| **Workload client secret** | Entra Auth app password credential bound via `WithClientSecret` into a secret `ParameterResource` (`AzureAd__ClientSecret`). Dashboard child resource `{app}-clientsecret` (`EntraClientSecretResource`) hosts the create command and status. Pipeline uses a provided parameter value in memory only; AppHost run mode may create a Graph `passwordCredential` (`addPassword`) by display name + lifetime (6/12/24 months → `EndDateTime`) and persist the one-shot `secretText` into AppHost deployment state. Distinct from management Graph credentials. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
