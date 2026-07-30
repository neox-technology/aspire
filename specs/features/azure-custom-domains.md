# Azure Container Apps custom domain ops

| Field | Value |
|-------|-------|
| Slug | `azure-custom-domains` |
| Status | implemented |
| Last code review | 2026-07-30 |

## Summary

Ship a reusable **hosting** NuGet package (`Neox.Aspire.Hosting.Azure.CustomDomains`) that AppHosts use to automate custom domain binding for Azure Container Apps: multi-provider DNS via **OctoDNS** (config generated in-process; sync via **Docker**) and **managed certificates** (env inventory → create → resource bind).

Consumers:

1. Register a DNS provider with `AddDomainOpsProvider(name).{Provider}(...)` (registers `prereq-domain` + `prereq-domain-{provider}` + `plan-domain-{provider}`).
2. Call `WithAzureCustomDomainOps(..., provider, ...)` on the compute resource (registers zone plan/provision, hostname add, env certificate plan/provision, and per-resource deploy/bind steps).
3. Invoke pipeline steps with `aspire do` around non-interactive `aspire deploy`.

V1 supports **one hostname per binding** (apex **or** subdomain, auto-detected). One provider resource may be shared by multiple bindings. Multiple apps in the **same DNS zone** share a single `plan-domain-{zone}` / `provision-domain-{zone}` pair (aggregated). Bootstrap: deploy with empty cert parameter → DomainOps DNS → add hostnames → create managed certs → bind certs (`deploy-*-domain`) → redeploy with `Parameters__certificateName` supplied by CI (GitHub variable automation is **out of scope V1**). Bicep `ConfigureCustomDomain` and DomainOps bind may both set hostname/cert (voluntary duplication).

### OctoDNS provider catalogue + source generator

- Catalogue JSON (`Provider/octodns-providers.json`) lists official Docker flavors from [octodns-docker](https://github.com/octodns/octodns-docker) except `octodns` (all), `etchosts`, and `dyn` (deprecated).
- Refresh is **manual** via `tools/octodns-provider-catalog` (scrapes docker README + per-provider READMEs); compile/CI stays offline and deterministic.
- A Roslyn source generator emits `{Name}DomainOpsProviderResource`, `{Name}DomainOpsProviderOptions`, and fluent `.MethodName(...)` on `IDomainOpsProviderBuilder`.
- Setting heuristics from provider README Configuration YAML: `env/...` → secret parameter; non-`env/` uncommented values → literals with defaults; commented non-`env/` toggles are ignored.

## User scenarios

- A contributor packs `Neox.Aspire.Hosting.Azure.CustomDomains` as a Shipping nupkg from this repo.
- A consumer AppHost wires `AddDomainOpsProvider` + `ConfigureCustomDomain` + `WithAzureCustomDomainOps(provider)`, and runs CI with `Parameters__*` / `Azure__*` / `--non-interactive`.
- Provider auth without explicit options resolves from `Parameters__{providerResourceName}-{param}` (e.g. `Parameters__dns-token`; Aspire also accepts underscore env fallback).
- **Bootstrap**: `aspire deploy` (empty cert) → DomainOps graph (`plan-domain-*` → `provision-domain-{zone}` → `plan-{resource}-domain` → `provision-{resource}-domain` → `provision-{env}-domains` → `provision-{env}-certificates` → `deploy-{resource}-domain` → `deploy-domains`) → `aspire deploy` with `Parameters__certificateName` set by the consumer.
- DNS DomainOps is **upsert-only**: create or update planned ACA records; **delete is not a feature** (no purge/replace-zone path). Dump live zone, dry-run, and Deletes=0 guard run **internally** (named pipeline steps deferred to V2).
- Contributors run xUnit unit tests (no live Azure) covering DNS planning, YAML generation (no secrets on disk), and provision orchestration with Docker process fakes and ARM client fakes (no `az` / no `gh` in V1).

## Routes (if UI)

None — invocation is via `aspire do` pipeline steps only.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting.Azure.AppContainers` (`AspireVersion` in `eng/Versions.props`) including public `ITokenCredentialProvider`
- `Azure.ResourceManager.AppContainers` for Container App read, hostname add (no cert), managed cert list/create, and hostname bind (ARM `Microsoft.App`; token scope `https://management.azure.com/.default`)
- YamlDotNet (OctoDNS zone/config serialization; no octodns NuGet — OctoDNS is Python-only)
- Official OctoDNS JSON Schemas for zone/config shape:
  - https://octodns.readthedocs.io/en/stable/_static/octodns-zone.schema.json
  - https://octodns.readthedocs.io/en/stable/_static/octodns-config.schema.json
- External CLIs (consumer / CI): Docker (`docker pull` / `docker run` of `octodns/cloudflare` or `octodns/ovh`). Azure CLI and GitHub CLI are **not** required for DomainOps V1.
- Experimental Aspire API `ConfigureCustomDomain` (`ASPIREACADOMAINS001`) remains consumer-owned

## Out of scope

- Multi-hostname / apex+www in a single binding API (V1 = one hostname)
- Bring-your-own certificates / Key Vault upload
- Azure Front Door or other edge frontends
- Live Azure + real DNS integration harness in default CI
- In-process DNS provider SDKs (Cloudflare/OVH C# APIs) — sync stays OctoDNS-in-Docker
- Providers listed only on OctoDNS docs without an official `octodns/{flavor}` Docker image
- Automatic catalogue refresh in Arcade CI (refresh is manual / contributor PR)
- Separate non-Azure DNS hosting package
- Replacing `ConfigureCustomDomain` itself
- Named pipeline steps for OctoDNS dump / dry-run / Deletes validation (V2)
- `gh variable set` / GitHub Actions variable automation (V2)
- `domain-verify` / `domain-guard` (removed; optional reintroduction later)

## Acceptance criteria

- [x] Package id is `Neox.Aspire.Hosting.Azure.CustomDomains` under `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`.
- [x] `AddDomainOpsProvider(name)` returns a builder with `.Cloudflare(...)` / `.Ovh(...)` producing `DomainOpsProviderResource` subtypes.
- [x] `.Cloudflare(...)` / `.Ovh(...)` register idempotent step `prereq-domain-{providerSlug}` that `docker pull`s the provider image and `DependsOn` `prereq-domain`.
- [x] Provider fluent API (`Resource` / `Options` / builder methods) is **source-generated** from `octodns-providers.json`; no hand-written Cloudflare/OVH provider types.
- [x] Catalogue covers official Docker flavors except `octodns` / `etchosts` / `dyn`; build succeeds offline without network access to OctoDNS.
- [x] `tools/octodns-provider-catalog` can refresh the catalogue from octodns-docker + provider READMEs.
- [x] Generator unit tests use a fixture catalogue (no network); smoke test covers an additional generated provider (e.g. Route53).
- [x] `AddDomainOpsProviderCore` registers idempotent step `prereq-domain` that `DependsOn` `provision-{acaEnv.Name}`.
- [x] `plan-domain-{provider}` writes `octodns.yaml` (zone names from model; `env/VAR` refs only); depends on `prereq-domain-{provider}`.
- [x] `plan-domain-{zone}` discovers ACA targets, dumps live zone (internal), upserts aggregated ACA records, writes zone YAML; depends on `plan-domain-{provider}` and per-resource `provision-{resource}-containerapp`.
- [x] `provision-domain-{zone}` dry-runs (internal), refuses Deletes, applies `octodns-sync --doit`; depends on `plan-domain-{zone}`.
- [x] `plan-{env}-certificates` inventories managed certs on the ACA environment; depends on `provision-{acaEnv}`.
- [x] `plan-{resource}-domain` prepares/validates domain model only (hostname, HTTP|CNAME, expected cert name) — **no ARM**.
- [x] `provision-{resource}-domain` adds hostname without certificate (`BindingType.Disabled`); no-op if hostname already present (does not detach an existing cert); depends on `plan-{resource}-domain`, zone provision, and `provision-{resource}-containerapp`.
- [x] `provision-{env}-domains` is a no-op gate depending on all `provision-{resource}-domain` for the env.
- [x] `provision-{env}-certificates` creates missing managed certificates (long wait); depends on `plan-{env}-certificates` and `provision-{env}-domains`.
- [x] `deploy-{resource}-domain` binds existing cert to hostname (SNI; rebinds wrong cert); depends on `provision-{env}-certificates` and `provision-{resource}-containerapp`; required by `deploy-domains`.
- [x] `deploy-domains` is a no-op gate depending on all `deploy-{resource}-domain`; required by Aspire `deploy`.
- [x] Multi-resource same zone: single `plan-domain-{zone}` / `provision-domain-{zone}` aggregating hostnames.
- [x] Auth options null → Aspire parameters `Parameters__{resourceName}-{param}`; credentials never written into generated YAML.
- [x] `WithAzureCustomDomainOps` requires `IResourceBuilder<TProvider>` where `TProvider : DomainOpsProviderResource`.
- [x] DomainOps DNS is **upsert-only**; a plan with `Deletes > 0` must not be applied.
- [x] ARM via `ITokenCredentialProvider` + `Azure.ResourceManager.AppContainers`; no `az` process.
- [x] No `gh` process in DomainOps V1.
- [x] `domain-verify` / `domain-guard` removed.
- [x] Docker images default to catalogue flavors (overridable).
- [x] Zone/config writers use YamlDotNet models aligned with OctoDNS JSON Schemas.
- [x] Package README documents the split pipeline graph, `Parameters__*`, Docker prerequisite, bootstrap vs steady-state (without GH automation).
- [x] Unit tests under `tests/azure-custom-domains/` (xUnit; no live Azure).
- [x] DigiCert constraint documented: CNAME must point directly at the ACA FQDN.
- [x] Interactive `aspire do` steps prompt unresolved parameters via `ParameterProcessor.SetParameterAsync` before `GetValueAsync`.
- [x] No Aspire dashboard resource commands (`WithCommand`); invocation is `aspire do` only.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/` |
| Package id | `Neox.Aspire.Hosting.Azure.CustomDomains` |
| Namespace | `Neox.Aspire.Hosting.Azure` |
| Extensions | `AddDomainOpsProvider`, `WithAzureCustomDomainOps` |
| Provider catalogue | `Provider/octodns-providers.json` |
| Source generator | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains.Generators/` |
| Catalogue refresh | `tools/octodns-provider-catalog/` |
| ARM client | `ArmAzureContainerAppClient` (`IAzureContainerAppClient`) |
| Unit tests | `tests/azure-custom-domains/` |
| Sample AppHost | `tests/azure-custom-domains/sample-apphost/` |

### Pipeline step contracts

| Step | Registered by | DependsOn | Exit 0 | Exit ≠ 0 |
|------|---------------|-----------|--------|----------|
| `prereq-domain` | `AddDomainOpsProviderCore` | `provision-{acaEnv.Name}` | ACA env provisioned | Missing ACA env / dependency failure |
| `prereq-domain-{provider}` | generated `.{Provider}` | `prereq-domain` | `docker pull` succeeded | Docker pull failure |
| `plan-domain-{provider}` | provider registration / DomainOps | `prereq-domain-{provider}` | `octodns.yaml` written | Writer / parameter failure |
| `plan-domain-{zone}` | `WithAzureCustomDomainOps` (idempotent per zone) | `plan-domain-{provider}`; `provision-{resource}-containerapp` (all resources in zone, via PipelineConfiguration) | Zone YAML upserted (dump internal) | ARM discover / dump soft-fail OK; upsert failure |
| `provision-domain-{zone}` | `WithAzureCustomDomainOps` (idempotent per zone) | `plan-domain-{zone}` | OctoDNS apply (dry-run+Deletes guard internal); DNS wait internal | Deletes>0 / Docker failure |
| `plan-{env}-certificates` | `WithAzureCustomDomainOps` (idempotent per env) | `provision-{acaEnv}` | Cert inventory loaded | ARM failure |
| `plan-{resource}-domain` | `WithAzureCustomDomainOps` | `provision-domain-{zone}` | Domain model validated | Missing hostname / invalid model |
| `provision-{resource}-domain` | `WithAzureCustomDomainOps` | `plan-{resource}-domain`; `provision-domain-{zone}`; `provision-{resource}-containerapp` (PipelineConfiguration) | Hostname present without requiring cert | ARM add failure |
| `provision-{env}-domains` | `WithAzureCustomDomainOps` (idempotent per env) | all `provision-{resource}-domain` for env (PipelineConfiguration) | Gate only | Dependency failure |
| `provision-{env}-certificates` | `WithAzureCustomDomainOps` (idempotent per env) | `plan-{env}-certificates`; `provision-{env}-domains` | Missing certs created | ARM / DigiCert validation failure |
| `deploy-{resource}-domain` | `WithAzureCustomDomainOps` | `provision-{env}-certificates`; `provision-{resource}-containerapp` (PipelineConfiguration) | Hostname bound to cert | ARM bind failure |
| `deploy-domains` | `WithAzureCustomDomainOps` (idempotent) | all `deploy-{resource}-domain`; RequiredBy Aspire `deploy` | Gate only | Dependency failure |

Aspire naming (upstream): ACA env Bicep step is `provision-{AzureContainerAppEnvironmentResource.Name}`; Container App Bicep step is `provision-{compute.Name}-containerapp`.

Zone step slug: registrable domain with `.` → `-` (e.g. `contoso.com` → `plan-domain-contoso-com`).

### Non-interactive inputs

- `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`
- `Parameters__customDomain`, `Parameters__certificateName` (empty string allowed on bootstrap deploy; required for Bicep `ConfigureCustomDomain` redeploy — supplied by consumer/CI)
- Provider auth: `Parameters__{providerName}-*`
- Docker available on the runner PATH
- Aspire Azure credential available (`ITokenCredentialProvider`; `Azure__SubscriptionId` required for `ArmClient`)
- `--non-interactive` on `aspire deploy` / `aspire do`
