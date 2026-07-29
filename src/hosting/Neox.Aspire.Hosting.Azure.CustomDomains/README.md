# Neox.Aspire.Hosting.Azure.CustomDomains

Aspire hosting helpers that automate **Azure Container Apps** custom domains: multi-provider DNS via [OctoDNS](https://github.com/octodns/octodns), **managed certificates**, and GitHub Actions variable updates.

## Prerequisites (consumer / CI)

- [Aspire CLI](https://aspire.dev/) and Azure authentication for `aspire deploy`
- [Azure CLI](https://learn.microsoft.com/cli/azure/) (`az`)
- [OctoDNS](https://github.com/octodns/octodns) (`octodns-sync`) configured for your DNS provider(s)
- [GitHub CLI](https://cli.github.com/) (`gh`) with permission to set Actions variables

> DigiCert managed certificates require the CNAME to point **directly** at the Container App FQDN (no Cloudflare proxy / intermediate CNAME).

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
        options.CertificateGitHubVariableName = "CERTIFICATE_NAME";
    });
#pragma warning restore ASPIREACADOMAINS001
```

### Pipeline steps

| Step | Command |
|------|---------|
| Verify | `aspire do domain-verify --non-interactive --environment production` |
| Provision | `aspire do domain-provision --non-interactive --environment production` |
| Guard | `aspire do domain-guard --non-interactive --environment production` |

See the feature spec [`azure-custom-domains`](../../../specs/features/azure-custom-domains.md) for bootstrap vs steady-state flows.

## Package

```bash
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
```
