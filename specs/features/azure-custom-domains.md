# Azure Container Apps custom domain ops

| Field | Value |
|-------|-------|
| Slug | `azure-custom-domains` |
| Status | implemented |
| Last code review | 2026-07-29 |

## Summary

Ship a reusable **hosting** NuGet package (`Neox.Aspire.Hosting.Azure.CustomDomains`) that AppHosts use to automate custom domain binding for Azure Container Apps: multi-provider DNS via **OctoDNS** (config generated in-process; sync via **Docker**), **managed certificates**, and GitHub Actions variable updates.

Consumers:

1. Register a DNS provider with `AddDomainOpsProvider(name).Cloudflare(...)` or `.Ovh(...)`.
2. Call `WithAzureCustomDomainOps(..., provider, ...)` on the compute resource.
3. Invoke pipeline steps with `aspire do` (`domain-verify`, `domain-provision`, `domain-guard`) around non-interactive `aspire deploy`, **or** run the same actions from the local Aspire dashboard via resource commands on each DomainOps provider.

V1 supports **one hostname per binding** (apex **or** subdomain, auto-detected). One provider resource may be shared by multiple bindings. Multiple provider resources may be registered; each only operates on its own bindings. Bootstrap uses an empty `certificateName` on the first deploy; steady-state fails closed when the certificate parameter is missing.

## User scenarios

- A contributor packs `Neox.Aspire.Hosting.Azure.CustomDomains` as a Shipping nupkg from this repo.
- A consumer AppHost wires `AddDomainOpsProvider` + `ConfigureCustomDomain` + `WithAzureCustomDomainOps(provider)`, and runs CI with `Parameters__*` / `Azure__*` / `--non-interactive`.
- Provider auth without explicit options resolves from `Parameters__{providerResourceName}-{param}` (e.g. `Parameters__dns-token`; Aspire also accepts underscore env fallback).
- **Bootstrap**: `aspire deploy` (empty cert) → `aspire do domain-provision` (generate OctoDNS YAML without secrets, `docker run` sync with `-e` credentials, hostname bind, `gh variable set`) → `aspire deploy` (cert name set).
- **Steady-state**: `aspire do domain-verify` → `aspire deploy` with `Parameters__certificateName` from the GitHub variable; `domain-guard` fails if the cert is required and empty.
- Contributors run xUnit unit tests (no live Azure) covering DNS planning, YAML generation (no secrets on disk), verify/guard, provision orchestration with process fakes, and dashboard command registration (multi-provider / idempotence).
- Locally, the Aspire dashboard shows **Verify**, **Guard**, and **Deploy** on each DomainOps provider that has at least one `WithAzureCustomDomainOps` binding; Deploy runs `domain-provision` logic. Command outcomes surface as Markdown in the notification center (**View response** / text visualizer); Verify opens the visualizer immediately.

## Routes (if UI)

Local Aspire dashboard only (resource commands on `DomainOpsProvider`). Not available when the dashboard runs in Azure Container Apps.

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting.Azure.AppContainers` (`AspireVersion` in `eng/Versions.props`)
- YamlDotNet (OctoDNS zone/config serialization; no octodns NuGet — OctoDNS is Python-only)
- Official OctoDNS JSON Schemas for zone/config shape:
  - https://octodns.readthedocs.io/en/stable/_static/octodns-zone.schema.json
  - https://octodns.readthedocs.io/en/stable/_static/octodns-config.schema.json
- External CLIs (consumer / CI): Azure CLI (`az`), Docker (`docker run` of `octodns/cloudflare` or `octodns/ovh`), GitHub CLI (`gh`)
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

## Acceptance criteria

- [x] Package id is `Neox.Aspire.Hosting.Azure.CustomDomains` under `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`.
- [x] `AddDomainOpsProvider(name)` returns a builder with `.Cloudflare(...)` / `.Ovh(...)` producing `DomainOpsProviderResource` subtypes.
- [x] Auth options null → Aspire parameters `Parameters__{resourceName}-{param}` (hyphenated; Aspire-valid resource names); credentials never written into generated YAML (`env/VAR` refs only).
- [x] `WithAzureCustomDomainOps` requires `IResourceBuilder<TProvider>` where `TProvider : DomainOpsProviderResource` (breaking).
- [x] One provider resource may be referenced by multiple `WithAzureCustomDomainOps` bindings.
- [x] `domain-provision` generates zone YAML + `octodns.yaml`, runs `docker run … octodns-sync --doit` with `-e` secrets, binds managed hostname, updates GitHub variable.
- [x] Docker images default to `octodns/cloudflare` / `octodns/ovh` (overridable).
- [x] Zone/config writers use YamlDotNet models aligned with OctoDNS JSON Schemas.
- [x] `domain-verify` / `domain-guard` behavior preserved (drift / missing cert).
- [x] Package README documents provider API, `Parameters__*`, Docker prerequisite, bootstrap vs steady-state.
- [x] Unit tests under `tests/azure-custom-domains/` (xUnit; no live Azure); assert `docker` args and no secrets in written YAML.
- [x] DigiCert constraint documented: CNAME must point directly at the ACA FQDN.
- [x] `WithAzureCustomDomainOps` registers dashboard commands `domain-verify` / `domain-guard` / `domain-provision` (display names Verify / Guard / Deploy) on the referenced `DomainOpsProvider` exactly once (idempotent across shared bindings).
- [x] Each provider’s commands run `DomainOpsOrchestrator` only for bindings that reference that provider (`ReferenceEquals`); multiple bindings on one provider run sequentially and fail fast.
- [x] Package README documents dashboard commands (local-only) and that Deploy ≡ `domain-provision`.
- [x] Dashboard commands return Markdown `CommandResults` payload (View response / CLI stdout); Verify uses `displayImmediately`; progress uses `context.Logger`.
- [x] Dashboard commands and interactive `aspire do` steps prompt unresolved parameters via `ParameterProcessor.SetParameterAsync` before `GetValueAsync` (avoids hanging on incomplete `WaitForValueTcs`).

## Terminology

See [`domain-glossary`](domain-glossary.md) (`custom domain ops`, `DomainOps provider`, `DomainOps provider commands`, `domain-provision`, `domain-verify`, `domain-guard`, `managed certificate`, `OctoDNS sync`).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/` |
| Package id | `Neox.Aspire.Hosting.Azure.CustomDomains` |
| Namespace | `Neox.Aspire.Hosting.Azure` |
| Extensions | `AddDomainOpsProvider`, `WithAzureCustomDomainOps` |
| Unit tests | `tests/azure-custom-domains/` |
| Sample AppHost | `tests/azure-custom-domains/sample-apphost/` |

### Pipeline step contracts

| Step | Exit 0 | Exit ≠ 0 |
|------|--------|----------|
| `domain-verify` | Expected DNS + cert parameter consistent | Drift, missing cert in strict mode, tool failure |
| `domain-guard` | Cert parameter non-empty when required | Empty/missing cert |
| `domain-provision` | YAML generated, Docker OctoDNS sync, managed cert bound, GH var updated | Azure/Docker/gh/DNS poll failure |

### Dashboard command contracts

Registered on each `DomainOpsProvider` after the first `WithAzureCustomDomainOps(..., provider)` call. Same orchestrator as the pipeline steps; `aspire do` remains the CI surface.

| Command name | Display name | Action |
|--------------|--------------|--------|
| `domain-verify` | Verify | `DomainOpsOrchestrator.VerifyAsync` for bindings of this provider |
| `domain-guard` | Guard | `DomainOpsOrchestrator.GuardAsync` for bindings of this provider |
| `domain-provision` | Deploy | `DomainOpsOrchestrator.ProvisionAsync` for bindings of this provider |

Success and failure return `CommandResults` with a Markdown `Data` payload (dashboard notification center + CLI stdout). Verify sets `displayImmediately`. Progress logs use `ExecuteCommandContext.Logger` (provider console logs).

### Non-interactive inputs

- `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`
- `Parameters__customDomain`, `Parameters__certificateName` (empty string allowed on bootstrap deploy)
- Provider auth: `Parameters__{providerName}-token` (Cloudflare) or `Parameters__{providerName}-application-key` / `-application-secret` / `-consumer-key` (OVH)
- Docker available on the runner PATH
- GitHub token with permission to set Actions variables
- `--non-interactive` on `aspire deploy` / `aspire do`
