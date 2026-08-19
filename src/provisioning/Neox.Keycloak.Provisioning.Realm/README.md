# Neox.Keycloak.Provisioning.Realm

Keycloak **realm representation** POCOs (`RealmRepresentation`, `ClientRepresentation`, …) source-generated from a checked-in snapshot of the [Keycloak Admin REST OpenAPI](https://www.keycloak.org/docs-api/26.2.5/rest-api/openapi.json) (Keycloak **26.2.5**).

This package is **Shipping**. It is not an Aspire hosting library; future [`Neox.Aspire.Hosting.Keycloak`](../../hosting/Neox.Aspire.Hosting.Keycloak/README.md) integration will consume it for realm JSON generation.

## Install

```bash
dotnet add package Neox.Keycloak.Provisioning.Realm
```

## Usage

```csharp
using System.Collections.Concurrent;
using System.Text.Json;
using Neox.Keycloak.Provisioning.Realm;

var realm = new RealmRepresentation
{
    Realm = "neox",
    Enabled = true,
    Clients = new ConcurrentBag<ClientRepresentation>
    {
        new()
        {
            ClientId = "neox-spa",
            PublicClient = true,
            StandardFlowEnabled = true,
            RedirectUris = new ConcurrentBag<string> { "http://localhost:5173/*" },
            WebOrigins = new ConcurrentBag<string> { "+" },
        },
    },
};

// Builders AppHost : maps via Attributes.GetOrAdd("key", _ => value)
// Listes : realm.Clients.GetOrAdd(c => c.ClientId == "spa", new() { ClientId = "spa", ... })
var json = JsonSerializer.Serialize(realm, KeycloakRealmJsonOptions.Default);
await File.WriteAllTextAsync("neox-realm.json", json);
```

Import file naming convention: `{realm}-realm.json` (for example `neox-realm.json`).

## Refresh OpenAPI snapshot

```bash
dotnet run --project tools/keycloak-realm-openapi-catalog
```

Builds stay offline from the checked-in `generated/openapi.json`.

## Spec

See [`specs/features/keycloak-provisioning-realm.md`](../../../specs/features/keycloak-provisioning-realm.md).
