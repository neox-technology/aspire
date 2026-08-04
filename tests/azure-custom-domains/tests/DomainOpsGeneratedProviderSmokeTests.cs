using Aspire.Hosting;
using Neox.Aspire.Hosting.Azure;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsGeneratedProviderSmokeTests
{
    [Fact]
    public void Route53_RegistersProviderResourceWithExpectedMetadata()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });
        builder.AddAzureContainerAppEnvironment("aca-env");

        var dns = builder.AddDomainOpsProvider("dns").Route53();

        Assert.Equal("route53", dns.Resource.ProviderSlug);
        Assert.Equal("octodns/route53", dns.Resource.DefaultDockerImage);
        Assert.Equal("octodns_route53.Route53Provider", dns.Resource.ProviderClass);
        Assert.Contains("access_key_id", dns.Resource.AuthParameters.Keys);
        Assert.Contains("secret_access_key", dns.Resource.AuthParameters.Keys);
    }

    [Fact]
    public void Cloudflare_StillCreatesTokenParameterWhenOptionsOmitted()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });
        builder.AddAzureContainerAppEnvironment("aca-env");

        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        Assert.Equal("cloudflare", dns.Resource.ProviderSlug);
        Assert.Contains("token", dns.Resource.AuthParameters.Keys);
        Assert.DoesNotContain("account_id", dns.Resource.AuthParameters.Keys);
        Assert.DoesNotContain("email", dns.Resource.AuthParameters.Keys);
    }
}
