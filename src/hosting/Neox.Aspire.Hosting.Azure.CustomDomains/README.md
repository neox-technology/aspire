# Neox.Aspire.Hosting.Azure.CustomDomains

Aspire hosting helpers that automate **Azure Container Apps** custom domains: multi-provider DNS via [OctoDNS](https://github.com/octodns/octodns) (config generated in-process; sync via **Docker**), and **managed certificates** (inventory → create → bind).

Fluent DNS provider APIs (`.Cloudflare()`, `.Ovh()`, `.Route53()`, …) are **source-generated** from the versioned catalogue [`Provider/octodns-providers.json`](Provider/octodns-providers.json) (official `octodns/{flavor}` Docker images, excluding `octodns` all / `etchosts` / `dyn`).

## Prerequisites (consumer / CI)

- [Aspire CLI](https://aspire.dev/) and Azure authentication for `aspire deploy` / DomainOps
- Aspire Azure credential via `ITokenCredentialProvider` (local: typically `az login` or another `Azure__CredentialSource`; CI: OIDC / service principal). The Azure CLI **binary is not required** for DomainOps ARM calls.
- [Docker](https://docs.docker.com/) with access to pull the provider image (e.g. `octodns/cloudflare`, `octodns/ovh`, `octodns/route53`)

### Secrets / tokens

| Need | Typical source |
|------|----------------|
| Azure | OIDC / service principal (`Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`) + Aspire credential |
| Provider auth | `Parameters__{providerName}-{setting}` from the generated Options (e.g. Cloudflare `Parameters__dns-token`; OVH `Parameters__dns-application-key` / `-application-secret` / `-consumer-key`) |

Credentials are **never** written into generated `octodns.yaml` (only `env/VAR` refs). Values are injected as container env vars when running `docker run`. DomainOps reads Container Apps and manages certificates/hostnames via **Azure Resource Manager** (`Azure.ResourceManager.AppContainers`), using the same token scope as Aspire deploy (`https://management.azure.com/.default`).

> **DigiCert / managed certificates:** the CNAME must point **directly** at the Container App FQDN (`*.azurecontainerapps.io`). Do **not** use a Cloudflare orange-cloud proxy, Traffic Manager, or other intermediate CNAME — issuance and renewal will fail.

## Usage

```csharp
var customDomain = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName");

var dns = builder.AddDomainOpsProvider("dns")
    .Cloudflare(); // or .Ovh(), .Route53(), … — auth from Parameters__dns-* when options are omitted

builder.AddAzureContainerAppEnvironment("env");

#pragma warning disable ASPIREACADOMAINS001
builder.AddProject<Projects.Api>("api")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infrastructure, app) =>
    {
        app.ConfigureCustomDomain(customDomain, certificateName);
    })
    .WithAzureCustomDomainOps(customDomain, certificateName, dns, options =>
    {
        options.ContainerAppResourceName = "api";
        options.DnsZoneName = "example.com"; // recommended for step naming / multi-app aggregation
        options.OctoDnsConfigPath = "dns/octodns.yaml";
        options.OctoDnsZoneDirectory = "dns/zones";
        // Bootstrap first deploy: options.RequireCertificateName = false;
    });
#pragma warning restore ASPIREACADOMAINS001
```

The same provider resource can be passed to multiple `WithAzureCustomDomainOps` bindings. Multiple apps in the **same DNS zone** share one `plan-domain-{zone}` / `provision-domain-{zone}` pair.

### Generated providers

Methods on `IDomainOpsProviderBuilder` mirror catalogue entries (Docker flavors). Refresh the catalogue after OctoDNS adds flavors:

```bash
dotnet run --project tools/octodns-provider-catalog
```

### Pipeline steps

| Step | Command | Depends on |
|------|---------|------------|
| Shared DomainOps gate | (via deploy graph) `prereq-domain` | `provision-{acaEnv}` |
| Provider image pull | `prereq-domain-{slug}` | `prereq-domain` |
| Plan OctoDNS config | `aspire do plan-domain-{slug}` | `prereq-domain-{slug}` |
| Plan zone YAML | `aspire do plan-domain-{zone}` | `plan-domain-{slug}`; `provision-{resource}-containerapp` (when materialized) |
| Provision zone (OctoDNS sync) | `aspire do provision-domain-{zone}` | `plan-domain-{zone}` |
| Plan env certificates | `aspire do plan-{env}-certificates` | `provision-{acaEnv}` |
| Plan resource domain (model) | `aspire do plan-{resource}-domain` | `provision-domain-{zone}` |
| Provision env certificates | `aspire do provision-{env}-certificates` | `plan-{env}-certificates`; resource plans; zone provision |
| Bind resource domain | `aspire do provision-{resource}-domain` | `provision-{env}-certificates`; `provision-{resource}-containerapp` |

Zone slug: registrable domain with `.` → `-` (e.g. `example.com` → `plan-domain-example-com`).

`provision-domain-{zone}` dumps the live zone and dry-runs OctoDNS **internally** (upsert-only; refuses Deletes), then applies. Named dump/dry-run steps are deferred to V2.

DNS DomainOps is **upsert-only**: planned A/CNAME/`asuid` TXT records are merged into the existing zone; other records are left untouched.

Unresolved parameters open Aspire's **Set parameter** modal for interactive `aspire do` before `GetValueAsync`; non-interactive CI must supply `Parameters__*`.

## GitHub Actions flows

Pass Aspire parameters non-interactively (`Parameters__customDomain`, `Parameters__certificateName`, provider auth) plus Azure settings. GitHub variable automation (`gh variable set`) is **out of scope for V1** — supply `Parameters__certificateName` yourself for the Bicep redeploy.

### Bootstrap (`CERTIFICATE_NAME` / cert parameter empty)

```yaml
- name: Bootstrap deploy (empty certificate)
  env:
    Azure__SubscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    Azure__Location: ${{ vars.AZURE_LOCATION }}
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ""
  run: aspire deploy --non-interactive --environment production

- name: Provision DNS + managed certs + bind
  env:
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ""
    Parameters__dns-token: ${{ secrets.CLOUDFLARE_TOKEN }}
  run: |
    aspire do provision-domain-example-com --non-interactive --environment production
    aspire do provision-env-certificates --non-interactive --environment production
    aspire do provision-api-domain --non-interactive --environment production

- name: Redeploy with certificate binding
  env:
    Azure__SubscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    Azure__Location: ${{ vars.AZURE_LOCATION }}
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ${{ vars.CERTIFICATE_NAME }}
  run: aspire deploy --non-interactive --environment production
```

### Steady-state

```yaml
- name: Deploy
  env:
    Azure__SubscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    Azure__Location: ${{ vars.AZURE_LOCATION }}
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ${{ vars.CERTIFICATE_NAME }}
  run: aspire deploy --non-interactive --environment production
```

## Package

```bash
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
```

Feature spec: [`azure-custom-domains`](../../../specs/features/azure-custom-domains.md).
