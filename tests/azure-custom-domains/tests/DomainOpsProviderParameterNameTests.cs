using Neox.Aspire.Hosting.Azure;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsProviderParameterNameTests
{
    [Theory]
    [InlineData("dns", "token", "dns-token")]
    [InlineData("dns", "application_key", "dns-application-key")]
    [InlineData("dns", "application_secret", "dns-application-secret")]
    [InlineData("dns", "consumer_key", "dns-consumer-key")]
    [InlineData("ovh", "application_key", "ovh-application-key")]
    public void GetParameterName_UsesHyphensCompatibleWithAspireResourceNames(
        string resourceName,
        string yamlPropertyName,
        string expected)
    {
        var provider = new CloudflareDomainOpsProviderResource(resourceName);

        var actual = provider.GetParameterName(yamlPropertyName);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain('_', actual);
    }
}
