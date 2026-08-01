# Neox.Aspire.Hosting.Azure.CustomDomains

[![NuGet](https://img.shields.io/nuget/vpre/Neox.Aspire.Hosting.Azure.CustomDomains.svg?label=NuGet)](https://www.nuget.org/packages/Neox.Aspire.Hosting.Azure.CustomDomains)

Aspire hosting helpers for **Azure Container Apps** custom domains: DNS via [OctoDNS](https://github.com/octodns/octodns) (config generated in-process; sync via **Docker**), and **managed certificates** (inventory → create → bind).

## Install

```bash
dotnet add package Neox.Aspire.Hosting.Azure.CustomDomains
```

```xml
<PackageReference Include="Neox.Aspire.Hosting.Azure.CustomDomains" Version="1.0.0-preview.*" />
```

## Naming

| Prefix | What it is |
|--------|------------|
| `AzureCustomDomainOps*` | Binding API on the compute resource (`WithAzureCustomDomainOps`, options, annotation) |
| `DomainOps*` | DNS provider + pipeline (`AddDomainOpsProvider`, step names like `plan-domain-*`) |

## Prerequisites

- [Aspire CLI](https://aspire.dev/)
- Azure auth for deploy / DomainOps (`ITokenCredentialProvider`; local: `az login` or `Azure__CredentialSource`; CI: OIDC / SP). The Azure CLI **binary is not required** for DomainOps ARM calls.
- [Docker](https://docs.docker.com/) to pull the provider image (e.g. `octodns/cloudflare`, `octodns/ovh`)

| Need | Typical source |
|------|----------------|
| Azure | `Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup` + Aspire credential |
| Provider auth | `Parameters__{providerName}-{setting}` (e.g. Cloudflare `Parameters__dns-token`) |

Credentials are never written into generated `octodns.yaml` (only `env/VAR` refs).

> **DigiCert:** DomainOps plans an **A** record to the ACA environment static IP plus `asuid` TXT, and creates managed certs with **HTTP** validation. Do not put an orange-cloud proxy in front of the hostname during issuance/renewal.

## Usage

### Shorthand (dash parameter names)

```csharp
var dns = builder.AddDomainOpsProvider("dns")
    .Cloudflare(); // or .Ovh(), .Route53(), … — auth from Parameters__dns-* when options are omitted

builder.AddAzureContainerAppEnvironment("env");

// Optional: same params for consumer-owned ConfigureCustomDomain
var (customDomain, certificateName) = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainParameters(
    builder, "api", "www.example.com");
// → Parameters__api-domain / Parameters__api-certificate

#pragma warning disable ASPIREACADOMAINS001
builder.AddProject<Projects.Api>("api")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infrastructure, app) =>
    {
        app.ConfigureCustomDomain(customDomain, certificateName);
    })
    .WithAzureCustomDomainOps("www.example.com", dns, options =>
    {
        options.DnsZoneName = "example.com";
        options.OctoDnsConfigPath = "dns/octodns.yaml";
        options.OctoDnsZoneDirectory = "dns/zones";
    });
#pragma warning restore ASPIREACADOMAINS001
```

| Overload | Parameters |
|----------|------------|
| `(domain, cert, provider)` | Explicit pair |
| `(domain, provider)` | Cert GetOrAdd `{domain.Name}-certificate` |
| `(hostname, provider)` | GetOrAdd `{resource}-domain` / `{resource}-certificate`; string = hostname default |

Additional hostnames on the same resource need distinct domain parameter names (string overload is primary-only).

### Explicit parameters

```csharp
var customDomain = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName");

var dns = builder.AddDomainOpsProvider("dns")
    .Cloudflare();

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
        options.DnsZoneName = "example.com";
        options.OctoDnsConfigPath = "dns/octodns.yaml";
        options.OctoDnsZoneDirectory = "dns/zones";
        // options.RequireCertificateName = false; // bootstrap first deploy
    });
#pragma warning restore ASPIREACADOMAINS001
```

Same provider can serve multiple bindings. Apps in the **same DNS zone** share one `plan-domain-{zone}` / `provision-domain-{zone}` pair. List steps with `aspire do --list-steps`.

Fluent provider APIs include `.Cloudflare()`, `.Ovh()`, `.Route53()`, `.Azure()`, `.Digitalocean()`, and other OctoDNS-backed providers. Auth is taken from `Parameters__{providerName}-*` when options are omitted.

## Pipeline steps

| Step | Command |
|------|---------|
| Shared gate | `prereq-domain` |
| Provider image | `prereq-domain-{slug}` |
| Plan OctoDNS config | `aspire do plan-domain-{slug}` |
| Plan zone YAML | `aspire do plan-domain-{zone}` |
| Sync zone | `aspire do provision-domain-{zone}` |
| Plan env certs | `aspire do plan-{env}-certificates` |
| Plan resource domain | `aspire do plan-{resource}-domain-{dom}` |
| Add hostname (no cert) | `aspire do provision-{resource}-domain-{dom}` |
| Env domains gate | `aspire do provision-{env}-domains` |
| Create missing certs | `aspire do provision-{env}-certificates` |
| Bind cert | `aspire do deploy-{resource}-domain-{dom}` |
| Deploy domains gate | `aspire do deploy-domains` (required by Aspire `deploy`) |

Zone / hostname slug: `.` → `-` (e.g. `example.com` → `example-com`).

DNS DomainOps is **upsert-only**. Unresolved parameters open Aspire’s Set parameter modal for interactive `aspire do`; CI must supply `Parameters__*`.

## CI flows

Pass `Parameters__*` and `Azure__*` non-interactively. Set the certificate parameter yourself for the Bicep redeploy (e.g. `Parameters__api-certificate` or `Parameters__certificateName`).

**Bootstrap** (empty cert): `aspire deploy` → run DomainOps provision/bind steps (see `aspire do --list-steps`) → `aspire deploy` with certificate name set.

**Steady-state:** `aspire deploy --non-interactive` with all parameters populated.

## Notes

- This is an Aspire **hosting** package: wire providers and bindings on `IDistributedApplicationBuilder`, then run DomainOps via `aspire do` / `aspire deploy`.
- Credentials must stay in Aspire parameters / environment variables — they are never written into generated `octodns.yaml`.
- License: MIT.
