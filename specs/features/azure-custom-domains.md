# Azure Container Apps custom domain ops

| Field | Value |
|-------|-------|
| Slug | `azure-custom-domains` |
| Status | defined |
| Last code review | 2026-07-29 |

## Summary

Ship a reusable **hosting** NuGet package (`Neox.Aspire.Hosting.Azure.CustomDomains`) that AppHosts use to automate custom domain binding for Azure Container Apps: multi-provider DNS via **OctoDNS**, **managed certificates**, and GitHub Actions variable updates. Consumers call `WithAzureCustomDomainOps` and invoke pipeline steps with `aspire do` (`domain-verify`, `domain-provision`, `domain-guard`) around non-interactive `aspire deploy`.

V1 supports **one hostname per binding** (apex **or** subdomain, auto-detected). Bootstrap uses an empty `certificateName` on the first deploy; steady-state fails closed when the certificate parameter is missing.

## User scenarios

- A contributor packs `Neox.Aspire.Hosting.Azure.CustomDomains` as a Shipping nupkg from this repo.
- A consumer AppHost references the package, wires `ConfigureCustomDomain` plus `WithAzureCustomDomainOps`, and runs CI non-interactively with `Parameters__*` / `Azure__*` / `--non-interactive`.
- **Bootstrap**: `aspire deploy` (empty cert) → `aspire do domain-provision` (read ACA targets, OctoDNS sync, hostname bind, `gh variable set`) → `aspire deploy` (cert name set).
- **Steady-state**: `aspire do domain-verify` → `aspire deploy` with `Parameters__certificateName` from the GitHub variable; `domain-guard` fails the pipeline if the cert is required and empty.
- Contributors run xUnit unit tests (no live Azure) that cover DNS planning, verify/guard, and provision orchestration with process fakes.

## Routes (if UI)

_N/A — hosting / pipeline library._

## Dependencies

- Arcade pack/publish ([`nuget-org`](nuget-org.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Aspire.Hosting.Azure.AppContainers` (`AspireVersion` in `eng/Versions.props`)
- External CLIs (consumer / CI prerequisites): Azure CLI (`az`), OctoDNS (`octodns-sync`), GitHub CLI (`gh`)
- Experimental Aspire API `ConfigureCustomDomain` (`ASPIREACADOMAINS001`) remains consumer-owned; this package orchestrates around it

## Out of scope

- Multi-hostname / apex+www in a single binding API (V1 = one hostname)
- Bring-your-own certificates / Key Vault upload
- Azure Front Door or other edge frontends
- Live Azure + real DNS integration harness in default CI
- Implementing OctoDNS providers inside the package (OctoDNS remains an external CLI)
- Replacing `ConfigureCustomDomain` itself

## Acceptance criteria

- [x] Package id is `Neox.Aspire.Hosting.Azure.CustomDomains` under `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/`.
- [x] `WithAzureCustomDomainOps` registers pipeline steps `domain-verify`, `domain-provision`, and `domain-guard` invocable via `aspire do`.
- [x] `DnsRecordPlanner` emits A+TXT for apex and CNAME+TXT for subdomain from hostname + FQDN / static IP / `asuid`.
- [x] OctoDNS zone YAML can be generated for the planned records.
- [x] `domain-verify` exits non-zero on DNS drift or missing certificate in strict/steady-state mode.
- [x] `domain-guard` fails when certificate name is required and empty.
- [x] `domain-provision` reads ACA ingress targets, runs OctoDNS sync, binds managed hostname, updates GitHub variable (default `CERTIFICATE_NAME`) via `IProcessRunner` (fakes in tests).
- [ ] Package README documents bootstrap vs steady-state GitHub Actions flows and required secrets/tokens.
- [x] Unit tests live under `tests/azure-custom-domains/` (xUnit; no live Azure requirement).
- [x] Arcade pack produces a Shipping nupkg for the project.
- [ ] DigiCert constraint is documented: CNAME must point directly at the ACA FQDN (no proxied/intermediate CNAME).

## Terminology

See [`domain-glossary`](domain-glossary.md) (`custom domain ops`, `domain-provision`, `domain-verify`, `domain-guard`, `managed certificate`, `OctoDNS sync`).

## Implementation notes

| Item | Path / value |
|------|----------------|
| Project | `src/hosting/Neox.Aspire.Hosting.Azure.CustomDomains/` |
| Package id | `Neox.Aspire.Hosting.Azure.CustomDomains` |
| Namespace | `Neox.Aspire.Hosting.Azure` |
| Extension | `WithAzureCustomDomainOps(...)` |
| Unit tests | `tests/azure-custom-domains/` |
| Sample AppHost | `tests/azure-custom-domains/sample-apphost/` |

### Pipeline step contracts

| Step | Exit 0 | Exit ≠ 0 |
|------|--------|----------|
| `domain-verify` | Expected DNS + cert parameter consistent | Drift, missing cert in strict mode, tool failure |
| `domain-guard` | Cert parameter non-empty when required | Empty/missing cert |
| `domain-provision` | DNS synced, managed cert bound, GH var updated | Azure/OctoDNS/gh/DNS poll failure |

### Non-interactive inputs

- `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`
- `Parameters__customDomain`, `Parameters__certificateName` (empty string allowed on bootstrap deploy)
- OctoDNS config path and provider credentials (consumer env)
- GitHub token with permission to set Actions variables
- `--non-interactive` on `aspire deploy` / `aspire do`
