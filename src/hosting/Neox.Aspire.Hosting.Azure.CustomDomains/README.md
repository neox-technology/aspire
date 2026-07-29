# Neox.Aspire.Hosting.Azure.CustomDomains

Aspire hosting helpers that automate **Azure Container Apps** custom domains: multi-provider DNS via [OctoDNS](https://github.com/octodns/octodns), **managed certificates**, and GitHub Actions variable updates.

## Prerequisites (consumer / CI)

- [Aspire CLI](https://aspire.dev/) and Azure authentication for `aspire deploy`
- [Azure CLI](https://learn.microsoft.com/cli/azure/) (`az`)
- [OctoDNS](https://github.com/octodns/octodns) (`octodns-sync`) configured for your DNS provider(s)
- [GitHub CLI](https://cli.github.com/) (`gh`) with permission to set Actions variables

### Secrets / tokens

| Need | Typical source |
|------|----------------|
| Azure | OIDC / service principal (`Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`) |
| OctoDNS providers | Provider API tokens in the runner environment (see your OctoDNS config) |
| GitHub variables | PAT or GitHub App token that can write repository Actions variables (`gh variable set`) |

> **DigiCert / managed certificates:** the CNAME must point **directly** at the Container App FQDN (`*.azurecontainerapps.io`). Do **not** use a Cloudflare orange-cloud proxy, Traffic Manager, or other intermediate CNAME — issuance and renewal will fail.

## Usage

```csharp
var customDomain = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName");

builder.AddAzureContainerAppEnvironment("env");

#pragma warning disable ASPIREACADOMAINS001
builder.AddProject<Projects.Api>("api")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infrastructure, app) =>
    {
        app.ConfigureCustomDomain(customDomain, certificateName);
    })
    .WithAzureCustomDomainOps(customDomain, certificateName, options =>
    {
        options.ContainerAppResourceName = "api";
        options.OctoDnsConfigPath = "dns/octodns.yaml";
        options.OctoDnsZoneDirectory = "dns/zones";
        options.CertificateGitHubVariableName = "CERTIFICATE_NAME";
        // Bootstrap first deploy: options.RequireCertificateName = false;
    });
#pragma warning restore ASPIREACADOMAINS001
```

### Pipeline steps

| Step | Command |
|------|---------|
| Verify | `aspire do domain-verify --non-interactive --environment production` |
| Provision | `aspire do domain-provision --non-interactive --environment production` |
| Guard | `aspire do domain-guard --non-interactive --environment production` |

Optional env for verify DNS planning without re-querying Azure: `NEOX_ACA_FQDN`, `NEOX_ACA_STATIC_IP`, `NEOX_ACA_ASUID`.

## GitHub Actions flows

Pass Aspire parameters non-interactively (`Parameters__customDomain`, `Parameters__certificateName`) plus Azure settings.

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
