using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Keycloak;
using Neox.Aspire.Hosting.Keycloak.EntraId;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.EntraId.Tests;

public sealed class AddEntraIdIdentityProviderTests
{
    [Fact]
    public void AddEntraIdIdentityProvider_throws_without_secret()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-no-secret");
        var realm = keycloak.AddRealm(realm: "neox");
        var app = builder.AddAzureAppRegistration("entra-app");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            realm.AddEntraIdIdentityProvider(EntraIdInstance.Workforce, app));

        Assert.Contains("WithSecret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEntraIdIdentityProvider_registers_oidc_stub_and_config_gate()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["Azure:TenantId"] = "tenant-guid";

        var keycloak = CreateKeycloak(builder, "auth-entra-idp");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("entra-secret", "placeholder", secret: true);
        var app = builder.AddAzureAppRegistration("entra-app").WithSecret(secret);

        var idp = realm.AddEntraIdIdentityProvider(
            EntraIdInstance.Workforce,
            app,
            alias: "microsoft",
            displayName: "Microsoft Entra ID");

        Assert.Equal("microsoft", idp.Resource.Alias);
        Assert.Equal("oidc", idp.Resource.ProviderId);
        Assert.Same(realm.Resource, idp.Resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<KeycloakRealmResource>>(idp.Resource);

        var config = Assert.Single(builder.Resources.OfType<KeycloakEntraIdConfigResource>());
        Assert.Equal($"{idp.Resource.Name}-config", config.Name);
        Assert.Same(idp.Resource, config.IdentityProvider);
        Assert.Contains(
            config.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, app.Resource));
        Assert.Contains(
            config.Annotations.OfType<WaitAnnotation>(),
            w => w.Resource is EntraIdPasswordCredentialResource);
        Assert.Contains(
            keycloak.Resource.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, config));
        Assert.DoesNotContain(
            keycloak.Resource.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, idp.Resource));

        using var document = ReadRealmJson(builder, "auth-entra-idp", "neox");
        var providers = document.RootElement.GetProperty("identityProviders");
        Assert.Equal(1, providers.GetArrayLength());
        Assert.Equal("microsoft", providers[0].GetProperty("alias").GetString());
        Assert.Equal("oidc", providers[0].GetProperty("providerId").GetString());
        Assert.Equal("Microsoft Entra ID", providers[0].GetProperty("displayName").GetString());

        var mappers = document.RootElement.GetProperty("identityProviderMappers");
        Assert.Equal(2, mappers.GetArrayLength());

        var emailMapper = Assert.Single(
            mappers.EnumerateArray(),
            mapper => mapper.GetProperty("name").GetString() == "email");
        Assert.Equal("microsoft", emailMapper.GetProperty("identityProviderAlias").GetString());
        Assert.Equal(
            "oidc-user-attribute-idp-mapper",
            emailMapper.GetProperty("identityProviderMapper").GetString());
        Assert.Equal("email", emailMapper.GetProperty("config").GetProperty("claim").GetString());
        Assert.Equal("email", emailMapper.GetProperty("config").GetProperty("user.attribute").GetString());
        Assert.Equal("INHERIT", emailMapper.GetProperty("config").GetProperty("syncMode").GetString());

        var preferredUsernameMapper = Assert.Single(
            mappers.EnumerateArray(),
            mapper => mapper.GetProperty("name").GetString() == "preferred_username-email");
        Assert.Equal("microsoft", preferredUsernameMapper.GetProperty("identityProviderAlias").GetString());
        Assert.Equal(
            "preferred_username",
            preferredUsernameMapper.GetProperty("config").GetProperty("claim").GetString());
        Assert.Equal(
            "email",
            preferredUsernameMapper.GetProperty("config").GetProperty("user.attribute").GetString());
    }

    [Fact]
    public void AddEntraIdIdentityProvider_registers_broker_web_redirect_when_uri_provided()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-broker");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("entra-secret", "placeholder", secret: true);
        var app = builder.AddAzureAppRegistration("entra-app").WithSecret(secret);
        var brokerUri = new Uri("https://localhost/realms/neox/broker/microsoft/endpoint");

        realm.AddEntraIdIdentityProvider(
            EntraIdInstance.Workforce,
            app,
            brokerRedirectUri: brokerUri);

        Assert.Contains(
            builder.Resources.OfType<AzureEntraIdWebApplicationResource>(),
            web => web.Name == "microsoft-broker");
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
