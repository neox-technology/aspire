# Neox.Aspire.Hosting.Azure.CustomDomains

Aspire hosting helpers that automate **Azure Container Apps** custom domains: multi-provider DNS via [OctoDNS](https://github.com/octodns/octodns) (config generated in-process; sync via **Docker**), **managed certificates**, and GitHub Actions variable updates.

## Prerequisites (consumer / CI)

- [Aspire CLI](https://aspire.dev/) and Azure authentication for `aspire deploy` / DomainOps
- Aspire Azure credential via `ITokenCredentialProvider` (local: typically `az login` or another `Azure__CredentialSource`; CI: OIDC / service principal). The Azure CLI **binary is not required** for DomainOps ARM calls.
- [Docker](https://docs.docker.com/) with access to pull `octodns/cloudflare` or `octodns/ovh`
- [GitHub CLI](https://cli.github.com/) (`gh`) with permission to set Actions variables

### Secrets / tokens

| Need | Typical source |
|------|----------------|
| Azure | OIDC / service principal (`Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`) + Aspire credential |
| Cloudflare | `Parameters__{providerName}-token` (e.g. `Parameters__dns-token`; env fallback `Parameters__dns_token`) |
| OVH | `Parameters__{providerName}-application-key`, `-application-secret`, `-consumer-key` |
| GitHub variables | PAT or GitHub App token that can write repository Actions variables (`gh variable set`) |

Credentials are **never** written into generated `octodns.yaml` (only `env/VAR` refs). Values are injected as container env vars when running `docker run`. DomainOps reads Container Apps and binds managed hostnames via **Azure Resource Manager** (`Azure.ResourceManager.AppContainers`), using the same token scope as Aspire deploy (`https://management.azure.com/.default`).

> **DigiCert / managed certificates:** the CNAME must point **directly** at the Container App FQDN (`*.azurecontainerapps.io`). Do **not** use a Cloudflare orange-cloud proxy, Traffic Manager, or other intermediate CNAME — issuance and renewal will fail.

## Usage

```csharp
var customDomain = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName");

var dns = builder.AddDomainOpsProvider("dns")
    .Cloudflare(); // or .Ovh(); auth from Parameters__dns-* when options are omitted

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
        options.OctoDnsConfigPath = "dns/octodns.yaml";
        options.OctoDnsZoneDirectory = "dns/zones";
        options.CertificateGitHubVariableName = "CERTIFICATE_NAME";
        // Bootstrap first deploy: options.RequireCertificateName = false;
    });
#pragma warning restore ASPIREACADOMAINS001
```

The same provider resource can be passed to multiple `WithAzureCustomDomainOps` bindings.

### Pipeline steps

| Step | Command |
|------|---------|
| Verify | `aspire do domain-verify --non-interactive --environment production` |
| Provision | `aspire do domain-provision --non-interactive --environment production` |
| Guard | `aspire do domain-guard --non-interactive --environment production` |

`domain-provision` depends on Aspire's `create-provisioning-context` step (which itself depends on `validate-azure-login`), then **dumps** the live zone, **upserts** ACA DNS records (create/update only — DomainOps never deletes), dry-runs OctoDNS and applies only when the plan has no Deletes, and binds the managed certificate through ARM — it does not shell out to `az`.

DNS DomainOps is **upsert-only**: planned A/CNAME/`asuid` TXT records are merged into the existing zone; other records are left untouched. There is no delete/purge/replace-zone path.

Optional env for verify DNS planning without re-querying Azure: `NEOX_ACA_FQDN`, `NEOX_ACA_STATIC_IP`, `NEOX_ACA_ASUID`.

Unresolved parameters open Aspire's **Set parameter** modal for interactive `aspire do` before `GetValueAsync`; non-interactive CI must supply `Parameters__*`.

## GitHub Actions flows

Pass Aspire parameters non-interactively (`Parameters__customDomain`, `Parameters__certificateName`, provider auth) plus Azure settings.

### Bootstrap (`CERTIFICATE_NAME` empty)

```yaml
- name: Bootstrap deploy (empty certificate)
  env:
    Azure__SubscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    Azure__Location: ${{ vars.AZURE_LOCATION }}
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ""
  run: aspire deploy --non-interactive --environment production

- name: Provision DNS, managed cert, GitHub variable
  env:
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ""
    Parameters__dns-token: ${{ secrets.CLOUDFLARE_TOKEN }}
    GITHUB_TOKEN: ${{ secrets.GH_VARIABLES_PAT }}
  run: aspire do domain-provision --non-interactive --environment production

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
- name: Verify domain + certificate
  env:
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ${{ vars.CERTIFICATE_NAME }}
  run: aspire do domain-verify --non-interactive --environment production

- name: Deploy
  env:
    Azure__SubscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    Azure__Location: ${{ vars.AZURE_LOCATION }}
    Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
    Parameters__customDomain: ${{ vars.CUSTOM_DOMAIN }}
    Parameters__certificateName: ${{ vars.CERTIFICATE_NAME }}
  run: aspire deploy --non-interactive --environment production
```

Branching tip: if `vars.CERTIFICATE_NAME` is empty, run the bootstrap path; otherwise steady-state.

## Package

```bash
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
```

Feature spec: [`azure-custom-domains`](../../../specs/features/azure-custom-domains.md).
