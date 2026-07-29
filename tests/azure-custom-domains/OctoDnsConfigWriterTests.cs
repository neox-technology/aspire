using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.Dns;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class OctoDnsConfigWriterTests
{
    [Fact]
    public void WriteConfigYaml_UsesEnvRefsAndNeverEmbedsSecrets()
    {
        var provider = new CloudflareDomainOpsProviderResource("dns");
        var token = new ParameterResource("dns_token", _ => "super-secret", secret: true);
        provider.BindAuthParameter("token", token);

        var yaml = new OctoDnsConfigWriter().WriteConfigYaml(provider, ["contoso.com"]);

        Assert.Contains("env/DNS_TOKEN", yaml, StringComparison.Ordinal);
        Assert.Contains("octodns_cloudflare.CloudflareProvider", yaml, StringComparison.Ordinal);
        Assert.Contains("contoso.com.", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteConfigYaml_OvhIncludesLiteralEndpoint()
    {
        var provider = new OvhDomainOpsProviderResource("ovh");
        provider.SetLiteral("endpoint", "ovh-eu");
        provider.BindAuthParameter("application_key", new ParameterResource("ovh_application_key", _ => "k", secret: true));
        provider.BindAuthParameter("application_secret", new ParameterResource("ovh_application_secret", _ => "s", secret: true));
        provider.BindAuthParameter("consumer_key", new ParameterResource("ovh_consumer_key", _ => "c", secret: true));

        var yaml = new OctoDnsConfigWriter().WriteConfigYaml(provider, ["example.com"]);

        Assert.Contains("endpoint: ovh-eu", yaml, StringComparison.Ordinal);
        Assert.Contains("env/OVH_APPLICATION_KEY", yaml, StringComparison.Ordinal);
        Assert.Contains("octodns_ovh.OvhProvider", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain(": k", yaml, StringComparison.Ordinal);
    }
}
