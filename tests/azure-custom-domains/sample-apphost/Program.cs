using Neox.Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

var dns = builder.AddDomainOpsProvider("dns")
    .Ovh();

builder.AddAzureContainerAppEnvironment("aca-env");

// Primary hostname: string overload GetOrAdds api-domain / api-certificate.
// Apex: distinct domain param + domain-only overload → api-apex-domain-certificate.
var customApexDomain = builder.AddParameter("api-apex-domain", "sokomwatt.com");

// Minimal smoke AppHost: registers domain-ops pipeline steps for `aspire do --list-steps`.
// No real project is published; this validates package wiring compile-time / list-steps locally.
builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
    .WithHttpEndpoint(targetPort: 8080)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((_, _) => { })
    .WithAzureCustomDomainOps("www.sokomwatt.com", dns, options => {
        options.DnsZoneName = "sokomwatt.com";
    })
    .WithAzureCustomDomainOps(customApexDomain, dns, options => {
        options.DnsZoneName = "sokomwatt.com";
    });

builder.Build().Run();
