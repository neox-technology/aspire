using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
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

    [Fact]
    public void AddJwtClient_writes_audience_protocol_mapper()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-mapper");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret);

        using var document = ReadRealmJson(builder, "auth-mapper", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        var mappers = client.GetProperty("protocolMappers");
        Assert.Equal(1, mappers.GetArrayLength());

        var audienceMapper = mappers[0];
        Assert.Equal("audience", audienceMapper.GetProperty("name").GetString());
        Assert.Equal("oidc-audience-mapper", audienceMapper.GetProperty("protocolMapper").GetString());
        Assert.Equal(
            "neox-api",
            audienceMapper.GetProperty("config").GetProperty("included.client.audience").GetString());
    }

    [Fact]
    public void WithRedirectUrl_writes_redirect_uri_to_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-redirect");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        var redirectUri = new Uri("https://localhost/swagger/oauth2-redirect.html");
        realm.AddJwtClient("api-client", "neox-api", secret)
            .WithRedirectUrl(redirectUri);

        using var document = ReadRealmJson(builder, "auth-redirect", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.Contains(
            redirectUri.AbsoluteUri,
            client.GetProperty("redirectUris").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public void WithRedirectUrl_writes_web_origin()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-origin");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret)
            .WithRedirectUrl(new Uri("https://localhost/swagger/oauth2-redirect.html"));

        using var document = ReadRealmJson(builder, "auth-origin", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.Contains(
            "https://localhost",
            client.GetProperty("webOrigins").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public void WithRedirectUrl_is_idempotent_for_duplicate_calls()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-redirect-idempotent");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        var redirectUri = new Uri("https://localhost/swagger/oauth2-redirect.html");
        var jwtClient = realm.AddJwtClient("api-client", "neox-api", secret);
        jwtClient.WithRedirectUrl(redirectUri);
        jwtClient.WithRedirectUrl(redirectUri);

        using var document = ReadRealmJson(builder, "auth-redirect-idempotent", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.Equal(1, client.GetProperty("redirectUris").GetArrayLength());
        var webOrigins = client.GetProperty("webOrigins").EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Contains("https://localhost", webOrigins);
        Assert.Equal(webOrigins.Length, webOrigins.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void WithRedirectUrl_does_not_add_dev_web_origin_wildcard()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        var keycloak = CreateKeycloak(builder, "auth-dev-no-wildcard");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret)
            .WithRedirectUrl(new Uri("http://localhost/swagger/oauth2-redirect.html"));

        using var document = ReadRealmJson(builder, "auth-dev-no-wildcard", "neox");
        var webOrigins = document.RootElement.GetProperty("clients")[0]
            .GetProperty("webOrigins")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("http://localhost", webOrigins);
        Assert.DoesNotContain("*", webOrigins);
    }

    [Fact]
    public void WithLocalRedirectUri_writes_loopback_redirects_in_development()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        var keycloak = CreateKeycloak(builder, "auth-local-redirect");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret)
            .WithLocalRedirectUri("/swagger/oauth2-redirect.html");

        using var document = ReadRealmJson(builder, "auth-local-redirect", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        var redirectUris = client.GetProperty("redirectUris").EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Contains("http://127.0.0.1/swagger/oauth2-redirect.html", redirectUris);
        Assert.Contains("http://localhost/swagger/oauth2-redirect.html", redirectUris);
        Assert.Contains("http://[::1]/swagger/oauth2-redirect.html", redirectUris);

        var webOrigins = client.GetProperty("webOrigins").EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Contains("*", webOrigins);
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
    }

    [Fact]
    public void WithLocalRedirectUri_is_noop_outside_development()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        var keycloak = CreateKeycloak(builder, "auth-local-prod");
        var realm = keycloak.AddRealm(realm: "neox");
        var secret = builder.AddParameter("api-secret", "secret", secret: true);
        realm.AddJwtClient("api-client", "neox-api", secret)
            .WithLocalRedirectUri("/swagger/oauth2-redirect.html");

        using var document = ReadRealmJson(builder, "auth-local-prod", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.False(client.TryGetProperty("redirectUris", out _));
        Assert.False(client.GetProperty("standardFlowEnabled").GetBoolean());
    }

    [Fact]
    public void AddOidcClient_registers_child_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-oidc");
        var realm = keycloak.AddRealm(realm: "neox");
        var oidcClient = realm.AddOidcClient("spa-client", "neox-spa");

        Assert.Equal("spa-client", oidcClient.Resource.Name);
        Assert.Same(realm.Resource, oidcClient.Resource.Parent);
        Assert.Equal("neox-spa", oidcClient.Resource.ClientId);
        Assert.Same(keycloak.Resource, oidcClient.Resource.Keycloak);
    }

    [Fact]
    public void AddOidcClient_writes_public_client_to_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-oidc-json");
        var realm = keycloak.AddRealm(realm: "neox");
        realm.AddOidcClient("spa-client", "neox-spa");

        using var document = ReadRealmJson(builder, "auth-oidc-json", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.Equal("neox-spa", client.GetProperty("clientId").GetString());
        Assert.True(client.GetProperty("publicClient").GetBoolean());
        Assert.Equal("openid-connect", client.GetProperty("protocol").GetString());
        Assert.False(client.TryGetProperty("secret", out _));
    }

    [Fact]
    public void WithApiAudience_writes_access_as_user_scope_and_default_client_scopes()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-oidc-audience");
        var realm = keycloak.AddRealm(realm: "neox");
        realm.AddOidcClient("spa-client", "neox-spa")
            .WithApiAudience("neox-api");

        using var document = ReadRealmJson(builder, "auth-oidc-audience", "neox");
        var scope = document.RootElement.GetProperty("clientScopes")[0];
        Assert.Equal("access_as_user", scope.GetProperty("name").GetString());
        var mappers = scope.GetProperty("protocolMappers").EnumerateArray().ToArray();
        Assert.Contains(
            mappers,
            mapper => mapper.GetProperty("name").GetString() == "audience"
                && mapper.GetProperty("config").GetProperty("included.client.audience").GetString() == "neox-api");
        Assert.Contains(
            mappers,
            mapper => mapper.GetProperty("name").GetString() == "preferred_username");

        var client = document.RootElement.GetProperty("clients")[0];
        var defaultScopes = client.GetProperty("defaultClientScopes")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("access_as_user", defaultScopes);
    }

    [Fact]
    public void WithRedirectUrl_on_oidc_client_writes_redirect_uri()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-oidc-redirect");
        var realm = keycloak.AddRealm(realm: "neox");
        var redirectUri = new Uri("http://localhost/");
        realm.AddOidcClient("spa-client", "neox-spa")
            .WithRedirectUrl(redirectUri);

        using var document = ReadRealmJson(builder, "auth-oidc-redirect", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.Contains(
            redirectUri.AbsoluteUri,
            client.GetProperty("redirectUris").EnumerateArray().Select(element => element.GetString()));
        Assert.Contains(
            "http://localhost",
            client.GetProperty("webOrigins").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public void WithLocalRedirectUri_on_oidc_client_writes_loopback_in_development()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        var keycloak = CreateKeycloak(builder, "auth-oidc-local");
        var realm = keycloak.AddRealm(realm: "neox");
        realm.AddOidcClient("spa-client", "neox-spa")
            .WithLocalRedirectUri("/");

        using var document = ReadRealmJson(builder, "auth-oidc-local", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        var redirectUris = client.GetProperty("redirectUris").EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Contains("http://127.0.0.1/", redirectUris);
        Assert.Contains("http://localhost/", redirectUris);

        var webOrigins = client.GetProperty("webOrigins").EnumerateArray().Select(element => element.GetString()).ToArray();
        Assert.Contains("*", webOrigins);
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
    }

    [Fact]
    public void WithLocalRedirectUri_on_oidc_client_is_noop_outside_development()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        var keycloak = CreateKeycloak(builder, "auth-oidc-prod");
        var realm = keycloak.AddRealm(realm: "neox");
        realm.AddOidcClient("spa-client", "neox-spa")
            .WithLocalRedirectUri("/");

        using var document = ReadRealmJson(builder, "auth-oidc-prod", "neox");
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.False(client.TryGetProperty("redirectUris", out _));
        Assert.False(client.GetProperty("standardFlowEnabled").GetBoolean());
    }

    [Fact]
    public async Task WithKeycloakSpa_sets_environment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-spa");
        var realm = keycloak.AddRealm(realm: "neox");
        var oidcClient = realm.AddOidcClient("spa-client", "neox-spa");
        var spa = builder.AddContainer("spa", "nginx").WithKeycloakSpa(oidcClient);

        var env = await ResolveEnvironmentAsync(builder, spa.Resource);
        Assert.Equal("neox", env["KEYCLOAK_REALM"]);
        Assert.Equal("neox-spa", env["KEYCLOAK_CLIENT_ID"]);
        Assert.True(env.ContainsKey("KEYCLOAK_URL"));
    }

    [Fact]
    public void WithKeycloakSpa_waits_for_keycloak_and_realm()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-spa-wait");
        var realm = keycloak.AddRealm(realm: "neox");
        var oidcClient = realm.AddOidcClient("spa-client", "neox-spa");
        var spa = builder.AddContainer("spa", "nginx").WithKeycloakSpa(oidcClient);

        Assert.Contains(
            spa.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, keycloak.Resource));
        Assert.Contains(
            spa.Resource.Annotations.OfType<WaitAnnotation>(),
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
