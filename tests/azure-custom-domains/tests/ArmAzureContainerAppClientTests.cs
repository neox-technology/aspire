using Azure.Core;
using Azure.ResourceManager.AppContainers.Models;
using Microsoft.Extensions.DependencyInjection;
using Neox.Aspire.Hosting.Azure.Provisioning;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class ArmAzureContainerAppClientTests
{
    [Fact]
    public void Constructor_RequiresSubscriptionId()
    {
        Assert.Throws<ArgumentException>(() => new ArmAzureContainerAppClient(new StubCredential(), " "));
    }

    [Fact]
    public void Constructor_RequiresCredential()
    {
        Assert.Throws<ArgumentNullException>(() => new ArmAzureContainerAppClient(null!, "sub-id"));
    }

    [Fact]
    public void Create_RequiresSubscriptionIdEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("Azure__SubscriptionId");
        var previousAlt = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        try
        {
            Environment.SetEnvironmentVariable("Azure__SubscriptionId", null);
            Environment.SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", null);
            var services = new ServiceCollection().BuildServiceProvider();
            var ex = Assert.Throws<InvalidOperationException>(() => ArmAzureContainerAppClient.Create(services));
            Assert.Contains("Subscription id", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Azure__SubscriptionId", previous);
            Environment.SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", previousAlt);
        }
    }

    [Theory]
    [InlineData("HTTP")]
    [InlineData("http")]
    public void ResolveDomainControlValidation_MapsHttp(string method)
    {
        Assert.Equal(
            ManagedCertificateDomainControlValidation.Http,
            ArmAzureContainerAppClient.ResolveDomainControlValidation(method));
    }

    [Theory]
    [InlineData("CNAME")]
    [InlineData("cname")]
    public void ResolveDomainControlValidation_MapsCname(string method)
    {
        Assert.Equal(
            ManagedCertificateDomainControlValidation.Cname,
            ArmAzureContainerAppClient.ResolveDomainControlValidation(method));
    }

    [Fact]
    public void ResolveDomainControlValidation_RejectsUnknownMethod()
    {
        Assert.Throws<ArgumentException>(() => ArmAzureContainerAppClient.ResolveDomainControlValidation("TXT"));
    }

    [Fact]
    public void IsAlreadyBoundToCertificate_TrueWhenSniMatches()
    {
        var certId = new ResourceIdentifier(
            "/subscriptions/x/resourceGroups/rg/providers/Microsoft.App/managedEnvironments/env/managedCertificates/www-contoso-com");
        var domain = new ContainerAppCustomDomain("www.contoso.com", certId)
        {
            BindingType = ContainerAppCustomDomainBindingType.SniEnabled
        };

        Assert.True(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate([domain], "www.contoso.com", certId));
        Assert.True(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate([domain], "WWW.CONTOSO.COM", certId));
    }

    [Fact]
    public void IsAlreadyBoundToCertificate_FalseWhenDisabledOrDifferentCert()
    {
        var certId = new ResourceIdentifier(
            "/subscriptions/x/resourceGroups/rg/providers/Microsoft.App/managedEnvironments/env/managedCertificates/www-contoso-com");
        var otherCertId = new ResourceIdentifier(
            "/subscriptions/x/resourceGroups/rg/providers/Microsoft.App/managedEnvironments/env/managedCertificates/other");

        var disabled = new ContainerAppCustomDomain("www.contoso.com")
        {
            BindingType = ContainerAppCustomDomainBindingType.Disabled
        };
        var wrongCert = new ContainerAppCustomDomain("www.contoso.com", otherCertId)
        {
            BindingType = ContainerAppCustomDomainBindingType.SniEnabled
        };

        Assert.False(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate([disabled], "www.contoso.com", certId));
        Assert.False(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate([wrongCert], "www.contoso.com", certId));
        Assert.False(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate([], "www.contoso.com", certId));
        Assert.False(ArmAzureContainerAppClient.IsAlreadyBoundToCertificate(null, "www.contoso.com", certId));
    }

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(GetToken(requestContext, cancellationToken));
    }
}
