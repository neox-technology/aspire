using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.Tests;

public sealed class AddKeycloakRealmTests
{
    [Fact]
    public void AddKeycloakRealm_registers_resource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var realm = builder.AddKeycloakRealm("auth", "neox", "http://localhost:8080", "spa-client");

        Assert.Equal("auth", realm.Resource.Name);
        Assert.Equal("neox", realm.Resource.Realm);
        Assert.Equal("http://localhost:8080", realm.Resource.AuthServerUrl);
        Assert.Equal("spa-client", realm.Resource.ClientId);
    }

    [Fact]
    public async Task WithKeycloakWebApi_sets_environment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var realm = builder.AddKeycloakRealm("auth", "neox", "http://localhost:8080", "api-client", "secret");
        var api = builder.AddContainer("api", "nginx").WithKeycloakWebApi(realm);

        var env = await ResolveEnvironmentAsync(builder, api.Resource);
        Assert.Equal("http://localhost:8080", env["Keycloak__AuthServerUrl"]);
        Assert.Equal("neox", env["Keycloak__Realm"]);
        Assert.Equal("http://localhost:8080/realms/neox", env["Keycloak__Authority"]);
        Assert.Equal("api-client", env["Keycloak__ClientId"]);
        Assert.Equal("api-client", env["Keycloak__Audience"]);
        Assert.Equal("secret", env["Keycloak__ClientSecret"]);
    }

    [Fact]
    public async Task WithKeycloakSpa_sets_environment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var realm = builder.AddKeycloakRealm("auth", "neox", "http://localhost:8080", "spa-client");
        var spa = builder.AddContainer("spa", "nginx").WithKeycloakSpa(realm);

        var env = await ResolveEnvironmentAsync(builder, spa.Resource);
        Assert.Equal("http://localhost:8080", env["KEYCLOAK_URL"]);
        Assert.Equal("neox", env["KEYCLOAK_REALM"]);
        Assert.Equal("spa-client", env["KEYCLOAK_CLIENT_ID"]);
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
