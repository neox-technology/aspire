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
| **custom domain ops** | Hosting helpers (`Neox.Aspire.Hosting.Azure.CustomDomains`) that orchestrate ACA custom domain DNS, managed certificates, and GitHub variable updates via `aspire do` pipeline steps. |
| **DomainOps provider** | Aspire resource (`DomainOpsProviderResource` and subtypes such as Cloudflare/OVH) that selects an OctoDNS DNS **provider**, holds auth parameter bindings, and drives generated `octodns.yaml` + Docker image choice. Aligns with octoDNS terminology (**provider**, not provisioner). |
| **domain-provision** | Pipeline step that reads ACA ingress targets via Azure Resource Manager (`ITokenCredentialProvider`), dumps the live DNS zone, **upserts** planned ACA records into OctoDNS YAML (no secrets on disk), applies Creates/Updates only via `docker run` + `octodns-sync`, binds a managed certificate via ARM, and sets the GitHub Actions certificate variable. Depends on Aspire `create-provisioning-context`. |
| **domain-verify** | Pipeline step that checks DNS and certificate parameter consistency before a steady-state deploy; fails closed on drift or missing cert in strict mode. |
| **domain-guard** | Pipeline step that fails when a certificate name is required and empty (steady-state fail-fast). |
| **managed certificate** | Free DigiCert TLS certificate issued and renewed by Azure Container Apps for a validated custom domain. |
| **OctoDNS sync** | Applying **upserted** DNS records (create/update only; DomainOps never deletes) by running the official OctoDNS Docker image (`octodns/cloudflare`, `octodns/ovh`, …) with `octodns-dump` then `octodns-sync`, mounting generated config/zones and injecting credentials via container env (`env/VAR` refs in YAML). DomainOps does not treat the zone YAML as full zone ownership. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
