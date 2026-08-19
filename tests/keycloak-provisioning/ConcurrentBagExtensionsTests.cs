using System.Collections.Concurrent;
using Neox.Keycloak.Provisioning.Realm;
using Xunit;

namespace Neox.Keycloak.Provisioning.Realm.Tests;

public sealed class ConcurrentBagExtensionsTests
{
    [Fact]
    public void GetOrAdd_returns_existing_when_predicate_matches()
    {
        var existing = new ClientRepresentation { ClientId = "spa" };
        var bag = new ConcurrentBag<ClientRepresentation> { existing };

        var result = bag.GetOrAdd(c => c.ClientId == "spa", new ClientRepresentation { ClientId = "spa" });

        Assert.Same(existing, result);
        Assert.Single(bag);
    }

    [Fact]
    public void GetOrAdd_adds_when_no_match()
    {
        var bag = new ConcurrentBag<ClientRepresentation>();
        var value = new ClientRepresentation { ClientId = "api" };

        var result = bag.GetOrAdd(c => c.ClientId == "api", value);

        Assert.Same(value, result);
        Assert.Single(bag);
        Assert.Equal("api", bag.First().ClientId);
    }

    [Fact]
    public void GetOrAdd_is_idempotent_for_duplicate_calls()
    {
        var bag = new ConcurrentBag<ClientRepresentation>();

        var first = bag.GetOrAdd(c => c.ClientId == "spa", new ClientRepresentation { ClientId = "spa" });
        var second = bag.GetOrAdd(c => c.ClientId == "spa", new ClientRepresentation { ClientId = "spa" });

        Assert.Same(first, second);
        Assert.Single(bag);
    }
}
