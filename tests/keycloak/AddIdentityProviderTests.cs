using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.Tests;

public sealed class AddIdentityProviderTests
{
    [Fact]
    public void AddIdentityProvider_registers_child_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-idp");
        var realm = keycloak.AddRealm(realm: "neox");
        var idp = realm.AddIdentityProvider("microsoft", "oidc");

        Assert.Equal("auth-idp-neox-idp-microsoft", idp.Resource.Name);
        Assert.Same(realm.Resource, idp.Resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<KeycloakRealmResource>>(idp.Resource);
        Assert.Equal("microsoft", idp.Resource.Alias);
        Assert.Equal("oidc", idp.Resource.ProviderId);
        Assert.Same(keycloak.Resource, idp.Resource.Keycloak);
    }

    [Fact]
    public void AddIdentityProvider_writes_identity_provider_in_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-idp-json");
        keycloak.AddRealm(realm: "neox")
            .AddIdentityProvider("microsoft", "oidc", idp =>
            {
                idp.DisplayName = "Microsoft";
                idp.Config = new System.Collections.Concurrent.ConcurrentDictionary<string, string>
                {
                    ["clientId"] = "app-id",
                };
            });

        using var document = ReadRealmJson(builder, "auth-idp-json", "neox");

        var providers = document.RootElement.GetProperty("identityProviders");
        Assert.Equal(1, providers.GetArrayLength());
        var provider = providers[0];
        Assert.Equal("microsoft", provider.GetProperty("alias").GetString());
        Assert.Equal("oidc", provider.GetProperty("providerId").GetString());
        Assert.True(provider.GetProperty("enabled").GetBoolean());
        Assert.Equal("Microsoft", provider.GetProperty("displayName").GetString());
        Assert.Equal("app-id", provider.GetProperty("config").GetProperty("clientId").GetString());
    }

    [Fact]
    public void AddIdentityProvider_is_idempotent_for_duplicate_alias()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-idp-idempotent");
        var realm = keycloak.AddRealm(realm: "neox");
        var first = realm.AddIdentityProvider("microsoft", "oidc", idp => idp.DisplayName = "First");
        var second = realm.AddIdentityProvider("microsoft", "oidc", idp => idp.DisplayName = "Second");

        Assert.Same(first.Resource, second.Resource);

        using var document = ReadRealmJson(builder, "auth-idp-idempotent", "neox");

        var providers = document.RootElement.GetProperty("identityProviders");
        Assert.Equal(1, providers.GetArrayLength());
        Assert.Equal("Second", providers[0].GetProperty("displayName").GetString());
    }

    private static IResourceBuilder<KeycloakResource> CreateKeycloak(
        IDistributedApplicationBuilder builder,
        string name)
    {
        var password = builder.AddParameter($"{name}-password", "admin", secret: true);
        return builder.AddKeycloak(name, adminPassword: password);
    }

    private static JsonDocument ReadRealmJson(
        IDistributedApplicationBuilder builder,
        string keycloakName,
        string realm)
    {
        var importDirectory = Path.GetFullPath(
            Path.Combine(".aspire", "keycloak-realms", keycloakName),
            builder.AppHostDirectory);
        var json = File.ReadAllText(Path.Combine(importDirectory, $"{realm}-realm.json"));
        return JsonDocument.Parse(json);
    }
}
