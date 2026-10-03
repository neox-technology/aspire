using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Xunit;

namespace Neox.Aspire.Hosting.Keycloak.Tests;

public sealed class AddUserTests
{
    [Fact]
    public void AddUser_registers_child_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var user = keycloak.AddUser("root", password, representation => representation.Email = "root@root.com");

        Assert.Equal("auth-user-user-root", user.Resource.Name);
        Assert.Same(keycloak.Resource, user.Resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<KeycloakResource>>(user.Resource);
        Assert.Equal("root", user.Resource.Username);
        Assert.Same(password.Resource, user.Resource.Password);
        Assert.Equal("root@root.com", user.Resource.Representation.Email);
        Assert.Equal("root", user.Resource.Representation.Username);
    }

    [Fact]
    public void AddUser_is_idempotent_for_duplicate_username()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user-idempotent");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var first = keycloak.AddUser("root", password, representation => representation.Email = "first@root.com");
        var second = keycloak.AddUser("root", password, representation => representation.Email = "second@root.com");

        Assert.Same(first.Resource, second.Resource);
        Assert.Equal("second@root.com", second.Resource.Representation.Email);
        Assert.Equal("root", second.Resource.Representation.Username);
    }

    [Fact]
    public void WithUser_writes_user_in_realm_json()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user-json");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var user = keycloak.AddUser("root", password, representation =>
        {
            representation.Email = "root@root.com";
            representation.EmailVerified = true;
        });
        keycloak.AddRealm(realm: "hub").WithUser(user);

        using var document = ReadRealmJson(builder, "auth-user-json", "hub");

        var users = document.RootElement.GetProperty("users");
        Assert.Equal(1, users.GetArrayLength());
        var imported = users[0];
        Assert.Equal("root", imported.GetProperty("username").GetString());
        Assert.True(imported.GetProperty("enabled").GetBoolean());
        Assert.Equal("root@root.com", imported.GetProperty("email").GetString());
        Assert.True(imported.GetProperty("emailVerified").GetBoolean());

        var credentials = imported.GetProperty("credentials");
        Assert.Equal(1, credentials.GetArrayLength());
        Assert.Equal("password", credentials[0].GetProperty("type").GetString());
        Assert.Equal("root", credentials[0].GetProperty("value").GetString());
        Assert.False(credentials[0].GetProperty("temporary").GetBoolean());
    }

    [Fact]
    public void WithRealmRole_writes_realm_role_and_is_idempotent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user-realm-role");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var user = keycloak.AddUser("root", password);
        user.WithRealmRole("RootAdministrator");
        user.WithRealmRole("RootAdministrator");
        keycloak.AddRealm(realm: "hub").WithUser(user);

        using var document = ReadRealmJson(builder, "auth-user-realm-role", "hub");

        var roles = document.RootElement.GetProperty("users")[0].GetProperty("realmRoles");
        Assert.Equal(1, roles.GetArrayLength());
        Assert.Equal("RootAdministrator", roles[0].GetString());
    }

    [Fact]
    public void WithUser_is_idempotent_for_duplicate_calls()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user-json-idempotent");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var user = keycloak.AddUser("root", password);
        var realm = keycloak.AddRealm(realm: "hub");
        realm.WithUser(user);
        realm.WithUser(user);

        using var document = ReadRealmJson(builder, "auth-user-json-idempotent", "hub");

        var users = document.RootElement.GetProperty("users");
        Assert.Equal(1, users.GetArrayLength());
        Assert.Equal(1, users[0].GetProperty("credentials").GetArrayLength());
    }

    [Fact]
    public void WithUser_rejects_user_from_another_keycloak()
    {
        var builder = DistributedApplication.CreateBuilder();
        var keycloak = CreateKeycloak(builder, "auth-user-owner");
        var other = CreateKeycloak(builder, "auth-user-other");
        var password = builder.AddParameter("root-password", "root", secret: true);
        var user = other.AddUser("root", password);
        var realm = keycloak.AddRealm(realm: "hub");

        var exception = Assert.Throws<InvalidOperationException>(() => realm.WithUser(user));

        Assert.Contains("auth-user-owner", exception.Message, StringComparison.Ordinal);
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
