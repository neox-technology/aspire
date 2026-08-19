using System.Collections.Concurrent;
using System.Text.Json;
using Neox.Keycloak.Provisioning.Realm;
using Xunit;

namespace Neox.Keycloak.Provisioning.Realm.Tests;

public sealed class KeycloakRealmSerializationTests
{
    [Fact]
    public void RoundTrip_minimal_realm_preserves_realm_and_clients()
    {
        var original = new RealmRepresentation
        {
            Realm = "neox",
            Enabled = true,
            Clients = new ConcurrentBag<ClientRepresentation>
            {
                new()
                {
                    ClientId = "spa",
                    PublicClient = true,
                    RedirectUris = new ConcurrentBag<string> { "http://localhost:5173/*" },
                },
            },
        };

        var json = JsonSerializer.Serialize(original, KeycloakRealmJsonOptions.Default);
        var restored = JsonSerializer.Deserialize<RealmRepresentation>(json, KeycloakRealmJsonOptions.Default);

        Assert.NotNull(restored);
        Assert.Equal("neox", restored.Realm);
        Assert.True(restored.Enabled);
        Assert.NotNull(restored.Clients);
        Assert.Single(restored.Clients);
        Assert.Equal("spa", restored.Clients.First().ClientId);
    }

    [Fact]
    public void ConcurrentDictionary_additional_properties_round_trip()
    {
        var original = new ClientRepresentation
        {
            ClientId = "api",
            Attributes = new ConcurrentDictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["pkce.code.challenge.method"] = "S256",
            },
        };

        var json = JsonSerializer.Serialize(original, KeycloakRealmJsonOptions.Default);
        var restored = JsonSerializer.Deserialize<ClientRepresentation>(json, KeycloakRealmJsonOptions.Default);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Attributes);
        Assert.Equal("S256", restored.Attributes["pkce.code.challenge.method"]);
    }
}
