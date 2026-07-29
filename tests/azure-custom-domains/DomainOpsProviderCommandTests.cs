using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsProviderCommandTests
{
    private static readonly string[] ExpectedCommandNames =
    [
        AzureCustomDomainOpsExtensions.DomainVerifyStepName,
        AzureCustomDomainOpsExtensions.DomainGuardStepName,
        AzureCustomDomainOpsExtensions.DomainProvisionStepName
    ];

    [Fact]
    public void WithAzureCustomDomainOps_RegistersDashboardCommandsOnProvider()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain");
        var certificateName = builder.AddParameter("certificateName");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, dns, options =>
            {
                options.RequireCertificateName = false;
            });

        AssertProviderCommands(dns.Resource, expectedCount: 3);
        Assert.Contains(
            dns.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == AzureCustomDomainOpsExtensions.DomainProvisionStepName
                 && a.DisplayName == "Deploy");
    }

    [Fact]
    public void WithAzureCustomDomainOps_SameProvider_DoesNotDuplicateCommands()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain");
        var certificateName = builder.AddParameter("certificateName");
        var customDomain2 = builder.AddParameter("customDomain2");
        var certificateName2 = builder.AddParameter("certificateName2");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, dns, o => o.RequireCertificateName = false);

        builder.AddContainer("web", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain2, certificateName2, dns, o => o.RequireCertificateName = false);

        Assert.Single(dns.Resource.Annotations.OfType<DomainOpsProviderCommandsAnnotation>());
        AssertProviderCommands(dns.Resource, expectedCount: 3);
    }

    [Fact]
    public void WithAzureCustomDomainOps_MultipleProviders_EachGetsOwnCommands()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain");
        var certificateName = builder.AddParameter("certificateName");
        var customDomain2 = builder.AddParameter("customDomain2");
        var certificateName2 = builder.AddParameter("certificateName2");
        var cf = builder.AddDomainOpsProvider("dns-cf").Cloudflare();
        var ovh = builder.AddDomainOpsProvider("dns-ovh").Ovh();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, cf, o => o.RequireCertificateName = false);

        builder.AddContainer("web", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain2, certificateName2, ovh, o => o.RequireCertificateName = false);

        AssertProviderCommands(cf.Resource, expectedCount: 3);
        AssertProviderCommands(ovh.Resource, expectedCount: 3);
        Assert.Single(cf.Resource.Annotations.OfType<DomainOpsProviderCommandsAnnotation>());
        Assert.Single(ovh.Resource.Annotations.OfType<DomainOpsProviderCommandsAnnotation>());
    }

    [Fact]
    public void FindBindings_FiltersByProviderReference()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomainBuilder = builder.AddParameter("customDomain");
        var certificateBuilder = builder.AddParameter("certificateName");
        var customDomain2Builder = builder.AddParameter("customDomain2");
        var certificate2Builder = builder.AddParameter("certificateName2");

        var cf = new CloudflareDomainOpsProviderResource("dns-cf");
        var ovh = new OvhDomainOpsProviderResource("dns-ovh");
        var api = new ContainerResource("api");
        var web = new ContainerResource("web");

        api.Annotations.Add(new AzureCustomDomainOpsAnnotation(
            customDomainBuilder,
            certificateBuilder,
            cf,
            new AzureCustomDomainOpsOptions { ContainerAppResourceName = "api" }));

        web.Annotations.Add(new AzureCustomDomainOpsAnnotation(
            customDomain2Builder,
            certificate2Builder,
            ovh,
            new AzureCustomDomainOpsOptions { ContainerAppResourceName = "web" }));

        var model = new DistributedApplicationModel([api, web, cf, ovh]);

        var cfBindings = DomainOpsProviderCommandBindings.FindBindings(model, cf);
        var ovhBindings = DomainOpsProviderCommandBindings.FindBindings(model, ovh);

        Assert.Single(cfBindings);
        Assert.Equal("api", cfBindings[0].Target.Name);
        Assert.Same(cf, cfBindings[0].Annotation.Provider);

        Assert.Single(ovhBindings);
        Assert.Equal("web", ovhBindings[0].Target.Name);
        Assert.Same(ovh, ovhBindings[0].Annotation.Provider);
    }

    private static void AssertProviderCommands(DomainOpsProviderResource provider, int expectedCount)
    {
        var commands = provider.Annotations.OfType<ResourceCommandAnnotation>().ToList();
        Assert.Equal(expectedCount, commands.Count);
        Assert.Equal(ExpectedCommandNames.OrderBy(n => n), commands.Select(c => c.Name).OrderBy(n => n));
    }
}
