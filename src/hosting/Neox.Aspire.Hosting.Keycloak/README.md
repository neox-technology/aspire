# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/)
with realm import helpers and (future) env projection for API and SPA resources.

## Quick Start

```csharp
var database = builder.AddPostgres("postgres").AddDatabase("keycloakdb");

builder.AddKeycloak("auth", adminPassword: password)
    .WithPostgres(database)
    .AddRealm(r =>
    {
        r.Clients = new ConcurrentBag<ClientRepresentation>
        {
            new() { ClientId = "neox-spa", PublicClient = true, Enabled = true }
        };
    }, realm: "neox");
```

`AddRealm` writes `.aspire/keycloak-realms/{keycloakName}/{realm}-realm.json` under the AppHost directory and wires upstream `WithRealmImport` once per Keycloak container.

`WithKeycloakWebApi` and `WithKeycloakSpa` are reserved for future env projection; they are currently no-ops.

## See also

- [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/)
