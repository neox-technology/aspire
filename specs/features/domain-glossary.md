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
| **AuthOps** | Hosting helpers split as `Neox.Aspire.Hosting.Auth.Abstractions` (common model, `WithAuth`) + `Neox.Aspire.Hosting.Auth.EntraId` (Entra via Microsoft Graph) that provision identity-provider **app registrations** and inject workload credentials via generic `AUTH_*` environment variables through `aspire do` / `aspire deploy` pipeline steps. |
| **AuthOpsResourceBase** | Abstract non-container Aspire resource for an identity **provider** (slug, apps, authority formatter); owns provider AuthOps steps (`prereq-{providerResource}-auth`, `deploy-auth`). |
| **EntraAuthOpsResource** | Entra ID AuthOps provider resource (`AuthOpsResourceBase`); created by `.Entra(...)`. |
| **AuthOpsResource** | Hidden AppHost marker resource (`auth-ops`) that owns shared AuthOps pipeline gates (notably `prereq-providers-auth`). |
| **Auth app** | Logical app registration under an Auth provider (`AddApp` / `AuthAppResource`): display name, application type (Web/Spa/Api/Native), redirect URIs, optional adopt via existing client id, and optional client-secret creation. |
| **prereq-{providerResource}-auth** | Provider-specific AuthOps prereq named from the Aspire provider resource name (e.g. `prereq-auth-provider-entra-auth`); ensures tenant parameter ready (Choice of accessible tenants); **RequiredBy** `prereq-providers-auth`. |
| **prereq-providers-auth** | Shared AuthOps noop gate on `AuthOpsResource`; fan-in of all `prereq-{providerResource}-auth` so every registered provider is authenticated before `plan-auth-*`. |
| **plan-auth-{app}** | Validates the desired Entra application model for one Auth app (no mutating Graph writes required); **DependsOn** `prereq-providers-auth`. |
| **provision-auth-{app}** | Creates or adopts the Entra application via Microsoft Graph, sets workload `ParameterResource`s (client id/secret/tenant), persists idempotence state under `Auth:Entra:{app}`. |
| **deploy-auth** | Gate hosted on the Entra provider resource; depends on all `provision-auth-{app}` steps; required by Aspire `deploy`. |
| **AUTH_ env convention** | Generic consumer injection `AUTH_{PROVIDER_SLUG}_{SETTING}` (e.g. `AUTH_ENTRA_CLIENT_ID`); with multiple apps under one provider, `AUTH_{PROVIDER}_{APP}_{SETTING}`. No ASP.NET Core scheme mapping in AuthOps v1. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
