using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class AzureCustomDomainOpsParameterTests
{
    [Fact]
    public void NamingHelpers_UseDashConvention()
    {
        Assert.Equal("api-domain", AzureCustomDomainOpsExtensions.GetCustomDomainParameterName("api"));
        Assert.Equal("api-certificate", AzureCustomDomainOpsExtensions.GetCertificateParameterName("api"));

        var builder = DistributedApplication.CreateBuilder();
        var domain = builder.AddParameter("api-domain", "www.example.com");
        Assert.Equal(
            "api-domain-certificate",
            AzureCustomDomainOpsExtensions.GetCertificateParameterName(domain.Resource));
    }

    [Fact]
    public void EnsureAzureCustomDomainParameters_CreatesDomainAndCertificate()
    {
        var builder = DistributedApplication.CreateBuilder();

        var (domain, cert) = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainParameters(
            builder,
            "api",
            "www.example.com");

        Assert.Equal("api-domain", domain.Resource.Name);
        Assert.Equal("api-certificate", cert.Resource.Name);
        Assert.Equal("www.example.com", AzureCustomDomainOpsExtensions.TryPeekHostname(builder, domain.Resource));
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "api-domain");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "api-certificate");
    }

    [Fact]
    public void EnsureAzureCustomDomainParameters_IsIdempotent()
    {
        var builder = DistributedApplication.CreateBuilder();

        var first = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainParameters(
            builder,
            "api",
            "www.example.com");
        var second = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainParameters(
            builder,
            "api",
            "other.example.com");

        Assert.Same(first.CustomDomain.Resource, second.CustomDomain.Resource);
        Assert.Same(first.CertificateName.Resource, second.CertificateName.Resource);
        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "api-domain"));
        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "api-certificate"));
        // Existing parameter keeps original default.
        Assert.Equal("www.example.com", AzureCustomDomainOpsExtensions.TryPeekHostname(builder, first.CustomDomain.Resource));
    }

    [Fact]
    public void EnsureAzureCustomDomainCertificateParameter_UsesDomainNameSuffix()
    {
        var builder = DistributedApplication.CreateBuilder();
        var domain = builder.AddParameter("custom-apex-domain", "example.com");

        var cert = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainCertificateParameter(builder, domain);

        Assert.Equal("custom-apex-domain-certificate", cert.Resource.Name);
        var again = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainCertificateParameter(builder, domain);
        Assert.Same(cert.Resource, again.Resource);
        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "custom-apex-domain-certificate"));
    }

    [Fact]
    public async Task DomainOnlyOverload_CreatesCertificateAndRegistersSteps()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var domain = builder.AddParameter("api-apex-domain", "example.com");
        var dns = builder.AddDomainOpsProvider("dns").Ovh();
        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(domain, dns, o => o.DnsZoneName = "example.com");

        Assert.Contains(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "api-apex-domain-certificate");

        var annotation = Assert.Single(
            builder.Resources.Single(r => r.Name == "api").Annotations.OfType<AzureCustomDomainOpsAnnotation>());
        Assert.Equal("api-apex-domain", annotation.CustomDomain.Resource.Name);
        Assert.Equal("api-apex-domain-certificate", annotation.CertificateName.Resource.Name);

        var steps = await CollectDomainOpsStepsAsync(builder);
        Assert.Contains(steps, s => s.Name == "plan-api-domain-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-api-domain-example-com");
    }

    [Fact]
    public async Task HostnameOverload_CreatesResourceNamedParametersAndRegistersSteps()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();
        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps("www.example.com", dns, o => o.DnsZoneName = "example.com");

        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "api-domain");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "api-certificate");

        var annotation = Assert.Single(
            builder.Resources.Single(r => r.Name == "api").Annotations.OfType<AzureCustomDomainOpsAnnotation>());
        Assert.Equal("api-domain", annotation.CustomDomain.Resource.Name);
        Assert.Equal("api-certificate", annotation.CertificateName.Resource.Name);
        Assert.Equal(
            "www.example.com",
            AzureCustomDomainOpsExtensions.TryPeekHostname(builder, annotation.CustomDomain.Resource));

        var steps = await CollectDomainOpsStepsAsync(builder);
        Assert.Contains(steps, s => s.Name == "plan-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "provision-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-api-domain-www-example-com");
    }

    [Fact]
    public void HostnameOverload_ReusesEnsureParameters()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var (domain, cert) = AzureCustomDomainOpsExtensions.EnsureAzureCustomDomainParameters(
            builder,
            "api",
            "www.example.com");
        var dns = builder.AddDomainOpsProvider("dns").Ovh();
        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps("www.example.com", dns, o => o.DnsZoneName = "example.com");

        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "api-domain"));
        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "api-certificate"));

        var annotation = Assert.Single(
            builder.Resources.Single(r => r.Name == "api").Annotations.OfType<AzureCustomDomainOpsAnnotation>());
        Assert.Same(domain.Resource, annotation.CustomDomain.Resource);
        Assert.Same(cert.Resource, annotation.CertificateName.Resource);
    }

#pragma warning disable ASPIREPIPELINES001
    private static async Task<List<PipelineStep>> CollectDomainOpsStepsAsync(IDistributedApplicationBuilder builder)
    {
        var model = new DistributedApplicationModel([.. builder.Resources]);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var services = new ServiceCollection().BuildServiceProvider();
        var pipelineContext = new PipelineContext(
            model,
            executionContext,
            services,
            NullLogger.Instance,
            CancellationToken.None);

        var steps = new List<PipelineStep>();
        foreach (var resource in builder.Resources.Where(static r =>
                     r is AzureCustomDomainOpsResource or DomainOpsProviderResource))
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var context = new PipelineStepFactoryContext
                {
                    Resource = resource,
                    PipelineContext = pipelineContext
                };
                var created = await annotation.CreateStepsAsync(context);
                steps.AddRange(created);
            }
        }

        return steps;
    }
#pragma warning restore ASPIREPIPELINES001
}
