using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.Tests;

public sealed class WithOrganizationTests
{
    [Fact]
    public void WithOrganizations_enables_organizations_in_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-orgs");
        keycloak.AddRealm(realm: "neox").WithOrganizations();

        using var document = ReadRealmJson(builder, "auth-orgs", "neox");

        Assert.True(document.RootElement.GetProperty("organizationsEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("organizations").ValueKind);
    }

    [Fact]
    public void WithOrganization_adds_organization_and_domain()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-org-add");
        keycloak.AddRealm(realm: "neox").WithOrganization("Acme", "acme.com");

        using var document = ReadRealmJson(builder, "auth-org-add", "neox");

        Assert.True(document.RootElement.GetProperty("organizationsEnabled").GetBoolean());
        var organizations = document.RootElement.GetProperty("organizations");
        Assert.Equal(1, organizations.GetArrayLength());

        var organization = organizations[0];
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        Assert.True(organization.GetProperty("enabled").GetBoolean());

        var domains = organization.GetProperty("domains");
        Assert.Equal(1, domains.GetArrayLength());
        Assert.Equal("acme.com", domains[0].GetProperty("name").GetString());
    }

    [Fact]
    public void WithOrganization_is_idempotent_for_duplicate_calls()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-org-idempotent");
        var realm = keycloak.AddRealm(realm: "neox");
        realm.WithOrganization("Acme", "acme.com");
        realm.WithOrganization("Acme", "acme.com");

        using var document = ReadRealmJson(builder, "auth-org-idempotent", "neox");

        var organizations = document.RootElement.GetProperty("organizations");
        Assert.Equal(1, organizations.GetArrayLength());
        Assert.Equal(1, organizations[0].GetProperty("domains").GetArrayLength());
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
