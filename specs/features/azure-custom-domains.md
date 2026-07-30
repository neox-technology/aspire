# Azure Container Apps custom domain ops

| Field | Value |
|-------|-------|
| Slug | `azure-custom-domains` |
| Status | implemented |
| Last code review | 2026-07-30 |

## Summary

Ship a reusable **hosting** NuGet package (`Neox.Aspire.Hosting.Azure.CustomDomains`) that AppHosts use to automate custom domain binding for Azure Container Apps: multi-provider DNS via **OctoDNS** (config generated in-process; sync via **Docker**), **managed certificates**, and GitHub Actions variable updates.

Consumers:

1. Register a DNS provider with `AddDomainOpsProvider(name).Cloudflare(...)` or `.Ovh(...)` (registers `prereq-domain` + `prereq-domain-{provider}`).
2. Call `WithAzureCustomDomainOps(..., provider, ...)` on the compute resource (registers `provision-{resource}-domain`, plus `domain-verify` / `domain-guard`).
3. Invoke pipeline steps with `aspire do` (`domain-verify`, `provision-{resource}-domain`, `domain-guard`) around non-interactive `aspire deploy`.

V1 supports **one hostname per binding** (apex **or** subdomain, auto-detected). One provider resource may be shared by multiple bindings. Multiple provider resources may be registered; each only operates on its own bindings. Bootstrap uses an empty `certificateName` on the first deploy; steady-state fails closed when the certificate parameter is missing.

## User scenarios

- A contributor packs `Neox.Aspire.Hosting.Azure.CustomDomains` as a Shipping nupkg from this repo.
- A consumer AppHost wires `AddDomainOpsProvider` + `ConfigureCustomDomain` + `WithAzureCustomDomainOps(provider)`, and runs CI with `Parameters__*` / `Azure__*` / `--non-interactive`.
- Provider auth without explicit options resolves from `Parameters__{providerResourceName}-{param}` (e.g. `Parameters__dns-token`; Aspire also accepts underscore env fallback).
- **Bootstrap**: `aspire deploy` (empty cert) → `aspire do provision-{resource}-domain` (after prereqs + ACA/containerapp provision; dump live zone, **upsert** ACA records into zone YAML, dry-run assert Creates/Updates only, `docker run` sync `--doit` with `-e` credentials, ARM hostname bind, `gh variable set`) → `aspire deploy` (cert name set).
- DNS DomainOps is **upsert-only**: create or update planned ACA records; **delete is not a feature** (no purge/replace-zone path).
- **Steady-state**: `aspire do domain-verify` → `aspire deploy` with `Parameters__certificateName` from the GitHub variable; `domain-guard` fails if the cert is required and empty.
- Contributors run xUnit unit tests (no live Azure) covering DNS planning, YAML generation (no secrets on disk), verify/guard, and provision orchestration with Docker/`gh` process fakes and ARM client fakes (no `az` process).

## Routes (if UI)

None — invocation is via `aspire do` pipeline steps only.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting.Azure.AppContainers` (`AspireVersion` in `eng/Versions.props`) including public `ITokenCredentialProvider`
- `Azure.ResourceManager.AppContainers` for Container App read + managed hostname bind (ARM `Microsoft.App`; token scope `https://management.azure.com/.default`)
- YamlDotNet (OctoDNS zone/config serialization; no octodns NuGet — OctoDNS is Python-only)
- Official OctoDNS JSON Schemas for zone/config shape:
  - https://octodns.readthedocs.io/en/stable/_static/octodns-zone.schema.json
  - https://octodns.readthedocs.io/en/stable/_static/octodns-config.schema.json
- External CLIs (consumer / CI): Docker (`docker pull` / `docker run` of `octodns/cloudflare` or `octodns/ovh`), GitHub CLI (`gh`). Azure CLI is **not** required for DomainOps; Aspire Azure credential (`Azure__CredentialSource`, often `az login` for local) supplies tokens.
- Experimental Aspire API `ConfigureCustomDomain` (`ASPIREACADOMAINS001`) remains consumer-owned

## Out of scope

- Multi-hostname / apex+www in a single binding API (V1 = one hostname)
- Bring-your-own certificates / Key Vault upload
- Azure Front Door or other edge frontends
- Live Azure + real DNS integration harness in default CI
- In-process DNS provider SDKs (Cloudflare/OVH C# APIs) — sync stays OctoDNS-in-Docker
- Source generator for additional OctoDNS providers (manual Cloudflare + OVH in V1)
- Separate non-Azure DNS hosting package
- Replacing `ConfigureCustomDomain` itself
- Renaming `domain-verify` / `domain-guard` (left unchanged in this pipeline split)

## Acceptance criteria

- [x] Package id is `Neox.Aspire.Hosting.Azure.CustomDomains` under `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`.
- [x] `AddDomainOpsProvider(name)` returns a builder with `.Cloudflare(...)` / `.Ovh(...)` producing `DomainOpsProviderResource` subtypes.
- [x] `.Cloudflare(...)` / `.Ovh(...)` register idempotent step `prereq-domain-{providerSlug}` (`cloudflare` | `ovh`) that `docker pull`s the provider image and `DependsOn` `prereq-domain`.
- [x] `AddDomainOpsProviderCore` registers idempotent step `prereq-domain` that `DependsOn` `provision-{acaEnv.Name}` (dynamic Aspire Bicep step for `AzureContainerAppEnvironmentResource`).
- [x] Auth options null → Aspire parameters `Parameters__{resourceName}-{param}` (hyphenated; Aspire-valid resource names); credentials never written into generated YAML (`env/VAR` refs only).
- [x] `WithAzureCustomDomainOps` requires `IResourceBuilder<TProvider>` where `TProvider : DomainOpsProviderResource` (breaking).
- [x] One provider resource may be referenced by multiple `WithAzureCustomDomainOps` bindings.
- [x] `WithAzureCustomDomainOps` registers `provision-{resource}-domain` (replacing `domain-provision`) whose action generates `octodns.yaml`, dumps the live zone, **upserts** planned ACA records, dry-runs/applies OctoDNS (Creates/Updates only), binds managed hostname, updates GitHub variable.
- [x] `provision-{resource}-domain` depends on `prereq-domain-{providerSlug}`; dependency on `provision-{resource}-containerapp` is wired via `PipelineConfigurationAnnotation` after the ACA deployment target exists (static `DependsOnSteps` would fail `ValidateSteps` because that step is created only after `prepare-azure-container-apps`).
- [x] DomainOps DNS is **upsert-only**: no delete/purge/replace-zone API or apply path; a plan with `Deletes > 0` is an invariant violation and must not be applied.
- [x] Provision path reads ACA targets and binds managed hostname via ARM (`ITokenCredentialProvider` + `Azure.ResourceManager.AppContainers`); does not invoke the `az` process.
- [x] Docker images default to `octodns/cloudflare` / `octodns/ovh` (overridable).
- [x] Zone/config writers use YamlDotNet models aligned with OctoDNS JSON Schemas.
- [x] `domain-verify` / `domain-guard` behavior preserved (drift / missing cert).
- [x] Package README documents provider API, pipeline step graph, `Parameters__*`, Docker prerequisite, bootstrap vs steady-state.
- [x] Package README documents ARM auth (`ITokenCredentialProvider`) and that Azure CLI is not required for DomainOps.
- [x] Unit tests under `tests/azure-custom-domains/` (xUnit; no live Azure); assert step names/DependsOn and `docker pull` args; no secrets in written YAML.
- [x] Unit tests assert provision path does not shell to `az`; ARM client covered with fakes.
- [x] DigiCert constraint documented: CNAME must point directly at the ACA FQDN.
- [x] Interactive `aspire do` steps prompt unresolved parameters via `ParameterProcessor.SetParameterAsync` before `GetValueAsync` (avoids hanging on incomplete `WaitForValueTcs`).
- [x] No Aspire dashboard resource commands (`WithCommand`) are registered on DomainOps providers; invocation is `aspire do` only.

## Terminology

See [`domain-glossary`](domain-glossary.md) (`custom domain ops`, `DomainOps provider`, `prereq-domain`, `prereq-domain-{provider}`, `provision-{resource}-domain`, `domain-verify`, `domain-guard`, `managed certificate`, `OctoDNS sync`).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/` |
| Package id | `Neox.Aspire.Hosting.Azure.CustomDomains` |
| Namespace | `Neox.Aspire.Hosting.Azure` |
| Extensions | `AddDomainOpsProvider`, `WithAzureCustomDomainOps` |
| ARM client | `ArmAzureContainerAppClient` (`IAzureContainerAppClient`) |
| Unit tests | `tests/azure-custom-domains/` |
| Sample AppHost | `tests/azure-custom-domains/sample-apphost/` |

### Pipeline step contracts

| Step | Registered by | DependsOn | Exit 0 | Exit ≠ 0 |
|------|---------------|-----------|--------|----------|
| `prereq-domain` | `AddDomainOpsProviderCore` (via `.Cloudflare` / `.Ovh`) | `provision-{acaEnv.Name}` | ACA env provisioned; prereq gate ready | Missing ACA env in model / dependency failure |
| `prereq-domain-{provider}` | `.Cloudflare` / `.Ovh` (`provider` = `cloudflare` \| `ovh`) | `prereq-domain` | `docker pull octodns/{provider}` succeeded | Docker pull failure |
| `domain-verify` | `WithAzureCustomDomainOps` | — | Expected DNS + cert parameter consistent | Drift, missing cert in strict mode, tool failure |
| `domain-guard` | `WithAzureCustomDomainOps` | — | Cert parameter non-empty when required | Empty/missing cert |
| `provision-{resource}-domain` | `WithAzureCustomDomainOps` | `prereq-domain-{provider}`; `provision-{resource}-containerapp` (wired via `PipelineConfigurationAnnotation` after `prepare-azure-container-apps` materializes the deployment target — not a static `DependsOnSteps` string) | Config + upserted zone YAML, OctoDNS dump/dry-run/apply (Creates/Updates only), managed cert bound via ARM, GH var updated | Azure ARM/Docker/gh/DNS poll failure; OctoDNS plan with Deletes |

Aspire naming (upstream): ACA env Bicep step is `provision-{AzureContainerAppEnvironmentResource.Name}`; Container App Bicep step is `provision-{compute.Name}-containerapp`.

### Non-interactive inputs

- `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`
- `Parameters__customDomain`, `Parameters__certificateName` (empty string allowed on bootstrap deploy)
- Provider auth: `Parameters__{providerName}-token` (Cloudflare) or `Parameters__{providerName}-application-key` / `-application-secret` / `-consumer-key` (OVH)
- Docker available on the runner PATH
- GitHub token with permission to set Actions variables
- Aspire Azure credential available (`ITokenCredentialProvider`; `Azure__SubscriptionId` required for `ArmClient`)
- `--non-interactive` on `aspire deploy` / `aspire do`
