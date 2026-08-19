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
    public void AddJwtClient_registers_child_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        var jwtClient = realm.AddJwtClient("api-client", "neox-api", secret);

        Assert.Equal("api-client", jwtClient.Resource.Name);
        Assert.Same(realm.Resource, jwtClient.Resource.Parent);
        Assert.Equal("neox-api", jwtClient.Resource.ClientId);
        Assert.Equal("neox-api", jwtClient.Resource.Audience);
        Assert.Same(keycloak.Resource, jwtClient.Resource.Keycloak);
    }

    [Fact]
    public void AddJwtClient_writes_client_to_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-jwt");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret);

        using var document = ReadRealmJson(builder, "auth-jwt", "neox");
        var clients = document.RootElement.GetProperty("clients");
        Assert.Equal(1, clients.GetArrayLength());
        Assert.Equal("neox-api", clients[0].GetProperty("clientId").GetString());
        Assert.Equal("client-secret", clients[0].GetProperty("clientAuthenticatorType").GetString());
        Assert.Equal("secret", clients[0].GetProperty("secret").GetString());
    }

    [Fact]
    public async Task WithKeycloakJwtBearer_sets_environment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        var jwtClient = realm.AddJwtClient("api-client", "neox-api", secret);
        var api = builder.AddContainer("api", "nginx").WithKeycloakJwtBearer(jwtClient);

        var env = await ResolveEnvironmentAsync(builder, api.Resource);
        Assert.Equal("neox", env["Keycloak__Realm"]);
        Assert.Equal("neox-api", env["Keycloak__ClientId"]);
        Assert.Equal("neox-api", env["Keycloak__Audience"]);
        Assert.Equal("auth", env["Keycloak__ServiceName"]);
        Assert.True(env.ContainsKey("Keycloak__AuthServerUrl"));
        Assert.Contains("/realms/neox", env["Keycloak__Authority"], StringComparison.Ordinal);
        Assert.Contains("{api-secret.value}", env["Keycloak__ClientSecret"], StringComparison.Ordinal);
    }

    [Fact]
    public void WithKeycloakJwtBearer_waits_for_keycloak()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        var jwtClient = realm.AddJwtClient("api-client", "neox-api", secret);
        var api = builder.AddContainer("api", "nginx").WithKeycloakJwtBearer(jwtClient);

        Assert.Contains(
            api.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, keycloak.Resource));
        Assert.Contains(
            api.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, realm.Resource));
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
                    ParameterResource parameter => parameter.ValueExpression,
                    _ => pair.Value?.ToString() ?? string.Empty
                };
            }
        }

        return values;
    }
}
