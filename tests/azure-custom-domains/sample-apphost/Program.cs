using Neox.Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

var customDomain = builder.AddParameter("customDomain", "sokomwatt.com");
var certificateName = builder.AddParameter("certificateName");

var dns = builder.AddDomainOpsProvider("dns")
    .Ovh();

builder.AddAzureContainerAppEnvironment("aca-env");

// Minimal smoke AppHost: registers domain-ops pipeline steps for `aspire do --list-steps`.
// No real project is published; this validates package wiring compile-time / list-steps locally.
builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
    .WithHttpEndpoint(targetPort: 8080)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((_, _) => { })
    .WithAzureCustomDomainOps(customDomain, certificateName, dns);

builder.Build().Run();
