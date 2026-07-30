using Neox.Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

var customDomain = builder.AddParameter("customDomain", "www.sokomwatt.com");
var customApexDomain = builder.AddParameter("customApexDomain", "sokomwatt.com");
var certificateName = builder.AddParameter("certificateName", string.Empty, publishValueAsDefault: true);
var certificateApexName = builder.AddParameter("certificateApexName", string.Empty, publishValueAsDefault: true);

var dns = builder.AddDomainOpsProvider("dns")
    .Ovh();

builder.AddAzureContainerAppEnvironment("aca-env");

// Minimal smoke AppHost: registers domain-ops pipeline steps for `aspire do --list-steps`.
// No real project is published; this validates package wiring compile-time / list-steps locally.
builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
    .WithHttpEndpoint(targetPort: 8080)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((_, _) => { })
    .WithAzureCustomDomainOps(customDomain, certificateName, dns, options => {
        options.DnsZoneName = "sokomwatt.com";
    })
    .WithAzureCustomDomainOps(customApexDomain, certificateApexName, dns, options => {
        options.DnsZoneName = "sokomwatt.com";
    });

builder.Build().Run();