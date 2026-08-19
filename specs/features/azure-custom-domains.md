# Azure Container Apps custom domain ops

| Field | Value |
|-------|-------|
| Slug | `azure-custom-domains` |
| Status | implemented |
| Last code review | 2026-07-30 |

## Summary

Hosting package `Neox.Aspire.Hosting.Azure.CustomDomains` automates ACA custom domains: OctoDNS (config in-process, sync via Docker) and managed certificates (inventory → create → bind). Consumers call `AddDomainOpsProvider` → `WithAzureCustomDomainOps` → `aspire do` around `aspire deploy`.

## User scenarios

- AppHost wires `AddDomainOpsProvider` + `ConfigureCustomDomain` + `WithAzureCustomDomainOps`; CI supplies `Parameters__*` / `Azure__*` with `--non-interactive`.
- Shorthand binding: `WithAzureCustomDomainOps(hostname, provider)` GetOrAdds `{resource}-domain` / `{resource}-certificate`; `WithAzureCustomDomainOps(domain, provider)` GetOrAdds `{domain.Name}-certificate`. `EnsureAzureCustomDomainParameters` exposes the same params for consumer-owned `ConfigureCustomDomain`.
- Multi-hostname on one compute: string overload is for the primary hostname only; additional hostnames use distinct parameter names + domain-only or full overload (e.g. `api-apex-domain` → cert `api-apex-domain-certificate`).
- Bootstrap: deploy with empty cert → DomainOps DNS + hostname + managed cert + bind → redeploy with certificate parameter set (GH variable automation is out of scope V1).
- DNS is upsert-only (TTL `0` by default); credentials never land in generated YAML.
- Unit tests under `tests/azure-custom-domains/` use fakes (no live Azure / no `az` / no `gh`).

## Routes (if UI)

None — `aspire do` pipeline steps only.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md))
- Sibling Azure hosting ([`azure-entra-id`](azure-entra-id.md))
- `Aspire.Hosting.Azure.AppContainers` (`ITokenCredentialProvider`), `Azure.ResourceManager.AppContainers`, YamlDotNet
- Consumer/CI: Docker for OctoDNS images; Azure CLI binary not required for DomainOps ARM
- Experimental `ConfigureCustomDomain` (`ASPIREACADOMAINS001`) remains consumer-owned

## Out of scope

- Multi-hostname in one binding; BYO / Key Vault certs; Front Door
- Live Azure + real DNS in default CI; in-process DNS SDKs
- Providers without official `octodns/{flavor}` image; automatic catalogue refresh in CI
- Separate non-Azure DNS package; replacing `ConfigureCustomDomain`
- Named dump/dry-run/Deletes steps; `gh variable set`; `domain-verify` / `domain-guard` (V2 / deferred)

## Acceptance criteria

- [x] Package `Neox.Aspire.Hosting.Azure.CustomDomains` under `src/hosting/.../CustomDomains/`.
- [x] `AddDomainOpsProvider` + generated `.Cloudflare()` / `.Ovh()` / …; catalogue + generator offline-deterministic.
- [x] Pipeline steps match **Pipeline step contracts** below (prereq → plan/provision zone → hostname → certs → bind → `deploy-domains`).
- [x] Multi-resource same zone aggregates; multi-hostname same compute gets distinct `{dom}` steps and serialized ARM PATCH.
- [x] Auth via `Parameters__{provider}-*`; ARM via `ITokenCredentialProvider` (no `az` / no `gh` process).
- [x] Upsert-only DNS (`Deletes > 0` refused); DigiCert A + HTTP validation documented.
- [x] Interactive `aspire do` prompts unresolved params; no dashboard `WithCommand`.
- [x] Unit tests + package README for consumers.
- [x] `WithAzureCustomDomainOps` overloads: `(domain, cert, provider)`, `(domain, provider)` (cert `{domain}-certificate`), `(hostname, provider)` (params `{resource}-domain` / `{resource}-certificate`); all keep `provider` + optional `configure`.
- [x] Dash naming helpers + `EnsureAzureCustomDomainParameters` GetOrAdd (idempotent); `ConfigureCustomDomain` remains consumer-owned.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/` |
| Package id | `Neox.Aspire.Hosting.Azure.CustomDomains` |
| Namespace | `Neox.Aspire.Hosting.Azure` |
| Binding API | `WithAzureCustomDomainOps`, `AzureCustomDomainOps*` |
| Provider / pipeline | `AddDomainOpsProvider`, `DomainOps*` |
| Provider catalogue | `Provider/octodns-providers.json` |
| Source generator | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains.Generators.Internal/` |
| Catalogue refresh | `tools/octodns-provider-catalog/` |
| ARM client | `ArmAzureContainerAppClient` |
| Unit tests | `tests/azure-custom-domains/` |
| Sample AppHost | `tests/azure-custom-domains/sample-apphost/` |

### Pipeline step contracts

| Step | Registered by | DependsOn | Exit 0 | Exit ≠ 0 |
|------|---------------|-----------|--------|----------|
| `prereq-domain` | `AddDomainOpsProviderCore` | `provision-{acaEnv.Name}` | ACA env provisioned | Missing ACA env / dependency failure |
| `prereq-domain-{provider}` | generated `.{Provider}` | `prereq-domain` | `docker pull` succeeded | Docker pull failure |
| `plan-domain-{provider}` | provider / DomainOps | `prereq-domain-{provider}` | `octodns.yaml` written | Writer / parameter failure |
| `plan-domain-{zone}` | `WithAzureCustomDomainOps` (idempotent per zone) | `plan-domain-{provider}`; `provision-{resource}-containerapp` (zone resources, PipelineConfiguration) | Zone YAML upserted | ARM discover / upsert failure |
| `provision-domain-{zone}` | `WithAzureCustomDomainOps` (idempotent per zone) | `plan-domain-{zone}` | OctoDNS apply (dry-run + Deletes guard internal) | Deletes>0 / Docker failure |
| `plan-{env}-certificates` | `WithAzureCustomDomainOps` (idempotent per env) | `provision-{acaEnv}` | Cert inventory loaded | ARM failure |
| `plan-{resource}-domain-{dom}` | `WithAzureCustomDomainOps` | `provision-domain-{zone}` | Domain model validated | Missing hostname / invalid model |
| `provision-{resource}-domain-{dom}` | `WithAzureCustomDomainOps` | `plan-{resource}-domain-{dom}`; zone provision; `provision-{resource}-containerapp`; previous sibling provision (ordinal slug) | Hostname present (no cert required) | ARM add failure |
| `provision-{env}-domains` | `WithAzureCustomDomainOps` (idempotent per env) | all `provision-{resource}-domain-{dom}` for env | Gate only | Dependency failure |
| `provision-{env}-certificates` | `WithAzureCustomDomainOps` (idempotent per env) | `plan-{env}-certificates`; `provision-{env}-domains` | Missing certs created in parallel | ARM / DigiCert failure |
| `deploy-{resource}-domain-{dom}` | `WithAzureCustomDomainOps` | `provision-{env}-certificates`; `provision-{resource}-containerapp`; previous sibling deploy (ordinal slug) | Hostname bound to cert | ARM bind failure |
| `deploy-domains` | `WithAzureCustomDomainOps` (idempotent) | all `deploy-{resource}-domain-{dom}`; RequiredBy Aspire `deploy` | Gate only | Dependency failure |

Aspire naming (upstream): ACA env Bicep `provision-{AzureContainerAppEnvironmentResource.Name}`; Container App Bicep `provision-{compute.Name}-containerapp`.

Zone / hostname slug: DNS name with `.` → `-`. Hostname for `{dom}` must be known at registration (parameter default or `Parameters:{name}`).

### Parameter naming (dash convention)

| Source | Domain param | Certificate param |
|--------|--------------|-------------------|
| Resource name `R` (string overload / `Ensure*`) | `{R}-domain` | `{R}-certificate` |
| Domain param name `X` (domain-only overload) | `X` (caller-owned) | `{X}-certificate` |

CI: `Parameters__{name}` (empty certificate allowed on bootstrap deploy). Explicit names (e.g. `customDomain` / `certificateName`) remain valid with the full overload.

### Non-interactive inputs

- `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`
- Domain / certificate: `Parameters__{resource}-domain`, `Parameters__{resource}-certificate` (shorthand) or any explicit pair with the full overload
- Provider auth: `Parameters__{providerName}-*`
- Docker on PATH; Aspire Azure credential (`ITokenCredentialProvider`)
- `--non-interactive` on `aspire deploy` / `aspire do`
