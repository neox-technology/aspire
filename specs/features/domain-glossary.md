# Glossary — Neox Aspire

| Field | Value |
|-------|-------|
| Slug | `domain-glossary` |
| Status | draft |
| Last code review | 2026-07-30 |

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
| **provision-{env}-domains** | Gate that depends on all `provision-{resource}-domain` steps for an ACA environment (no ARM). |
| **provision-{env}-certificates** | Creates missing managed certificates on the ACA environment (long wait); requires hostnames already on apps. |
| **plan-{resource}-domain** | Prepares/validates the per-resource domain model (hostname, HTTP\|CNAME, expected cert name) without ARM calls. |
| **provision-{resource}-domain** | Adds the custom hostname to the Container App without a certificate (`BindingType.Disabled`); no-op if the hostname already exists (does not detach an existing cert). |
| **deploy-{resource}-domain** | Binds an existing managed certificate to the Container App hostname (SNI); rebinds if a different cert is already linked. |
| **deploy-domains** | Gate that depends on all `deploy-{resource}-domain` steps; required by Aspire `deploy`. |
| **managed certificate** | Free DigiCert TLS certificate issued and renewed by Azure Container Apps for a validated custom domain. |
| **OctoDNS sync** | Applying **upserted** DNS records (create/update only; DomainOps never deletes) by running the official OctoDNS Docker image (`octodns/cloudflare`, `octodns/ovh`, …) with `octodns-dump` then `octodns-sync`, mounting generated config/zones and injecting credentials via container env (`env/VAR` refs in YAML). DomainOps does not treat the zone YAML as full zone ownership. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
