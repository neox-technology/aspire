using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.Tests;

public sealed class AddRealmTests
{
    [Fact]
    public void AddRealm_registers_child_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm(realm: "neox");

        Assert.Equal("auth-neox", realm.Resource.Name);
        Assert.Same(keycloak.Resource, realm.Resource.Parent);
        Assert.Equal("neox", realm.Resource.Realm);
    }

    [Fact]
    public void AddRealm_writes_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        keycloak.AddRealm(realm: "neox");

        var importDirectory = Path.GetFullPath(
            Path.Combine(".aspire", "keycloak-realms", "auth"),
            builder.AppHostDirectory);
        var realmFilePath = Path.Combine(importDirectory, "neox-realm.json");

        Assert.True(File.Exists(realmFilePath));
    }

    [Fact]
    public void AddRealm_invokes_callback()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        keycloak.AddRealm(r => r.DisplayName = "Neox Test", realm: "neox");

        var importDirectory = Path.GetFullPath(
            Path.Combine(".aspire", "keycloak-realms", "auth"),
            builder.AppHostDirectory);
        var json = File.ReadAllText(Path.Combine(importDirectory, "neox-realm.json"));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Neox Test", document.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("neox", document.RootElement.GetProperty("realm").GetString());
    }

    [Fact]
    public void AddRealm_default_realm_is_master()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm();

        Assert.Equal("master", realm.Resource.Realm);
        Assert.True(File.Exists(Path.Combine(
            Path.GetFullPath(Path.Combine(".aspire", "keycloak-realms", "auth"), builder.AppHostDirectory),
            "master-realm.json")));
    }

    [Fact]
    public async Task WithKeycloakWebApi_is_stub()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm(realm: "neox");
        var api = builder.AddContainer("api", "nginx").WithKeycloakWebApi(realm);

        var env = await ResolveEnvironmentAsync(builder, api.Resource);
        Assert.DoesNotContain(env.Keys, key => key.StartsWith("Keycloak__", StringComparison.Ordinal));
    }

    private static IResourceBuilder<KeycloakResource> CreateKeycloak(
        IDistributedApplicationBuilder builder,
        string name)
    {
        var password = builder.AddParameter($"{name}-password", "admin", secret: true);
        return builder.AddKeycloak(name, adminPassword: password);
    }

    private static async Task<Dictionary<string, string>> ResolveEnvironmentAsync(
        IDistributedApplicationBuilder builder,
        IResource resource)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource);
            await annotation.Callback(context).ConfigureAwait(false);
            foreach (var pair in context.EnvironmentVariables)
            {
                values[pair.Key] = pair.Value switch
                {
                    ReferenceExpression expression => expression.ValueExpression,
                    _ => pair.Value?.ToString() ?? string.Empty
                };
            }
        }

        return values;
    }
}
