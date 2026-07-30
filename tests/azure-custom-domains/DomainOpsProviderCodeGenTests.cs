using Neox.Aspire.Hosting.Azure.CustomDomains.Generators;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsProviderCodeGenTests
{
    private const string FixtureCatalog = """
        {
          "providers": [
            {
              "slug": "cloudflare",
              "methodName": "Cloudflare",
              "providerClass": "octodns_cloudflare.CloudflareProvider",
              "dockerImage": "octodns/cloudflare",
              "settings": [
                {
                  "yamlName": "token",
                  "kind": "secret",
                  "required": true,
                  "propertyName": "Token"
                },
                {
                  "yamlName": "account_id",
                  "kind": "secret",
                  "required": false,
                  "propertyName": "AccountId"
                }
              ]
            },
            {
              "slug": "route53",
              "methodName": "Route53",
              "providerClass": "octodns_route53.Route53Provider",
              "dockerImage": "octodns/route53",
              "settings": [
                {
                  "yamlName": "access_key_id",
                  "kind": "secret",
                  "required": true,
                  "propertyName": "AccessKeyId"
                },
                {
                  "yamlName": "secret_access_key",
                  "kind": "secret",
                  "required": true,
                  "propertyName": "SecretAccessKey"
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void ParseCatalog_ReadsProvidersAndSettings()
    {
        var providers = DomainOpsProviderCodeGen.ParseCatalog(FixtureCatalog);

        Assert.Equal(2, providers.Count);
        Assert.Equal("Cloudflare", providers[0].MethodName);
        Assert.Equal(2, providers[0].Settings.Count);
        Assert.True(providers[0].Settings[0].Required);
        Assert.False(providers[0].Settings[1].Required);
        Assert.Equal("Route53", providers[1].MethodName);
    }

    [Fact]
    public void GenerateInterface_EmitsFluentMethods()
    {
        var providers = DomainOpsProviderCodeGen.ParseCatalog(FixtureCatalog);
        var source = DomainOpsProviderCodeGen.GenerateInterface(providers);

        Assert.Contains("IResourceBuilder<CloudflareDomainOpsProviderResource> Cloudflare(", source, StringComparison.Ordinal);
        Assert.Contains("IResourceBuilder<Route53DomainOpsProviderResource> Route53(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuilderMethods_BindsRequiredSecretsAndOptionalParams()
    {
        var providers = DomainOpsProviderCodeGen.ParseCatalog(FixtureCatalog);
        var source = DomainOpsProviderCodeGen.GenerateBuilderMethods(providers);

        Assert.Contains("BindSecret(resource, \"token\", options.Token, secret: true);", source, StringComparison.Ordinal);
        Assert.Contains("if (options.AccountId is not null)", source, StringComparison.Ordinal);
        Assert.Contains("BindSecret(resource, \"access_key_id\", options.AccessKeyId, secret: true);", source, StringComparison.Ordinal);
        Assert.Contains("return AddProviderResource(resource);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateProviderTypes_EmitsResourceMetadata()
    {
        var providers = DomainOpsProviderCodeGen.ParseCatalog(FixtureCatalog);
        var source = DomainOpsProviderCodeGen.GenerateProviderTypes(providers[1]);

        Assert.Contains("class Route53DomainOpsProviderResource", source, StringComparison.Ordinal);
        Assert.Contains("octodns_route53.Route53Provider", source, StringComparison.Ordinal);
        Assert.Contains("octodns/route53", source, StringComparison.Ordinal);
        Assert.Contains("ProviderSlug => \"route53\"", source, StringComparison.Ordinal);
        Assert.Contains("AccessKeyId", source, StringComparison.Ordinal);
        Assert.Contains("SecretAccessKey", source, StringComparison.Ordinal);
    }
}
