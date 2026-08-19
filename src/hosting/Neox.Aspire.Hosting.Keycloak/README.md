# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/)
with realm import helpers and env projection for API and SPA resources.

## Quick Start

```csharp
var apiClientSecret = builder.AddParameter("keycloak-api-client-secret", "secret", secret: true);

builder.AddKeycloak("auth", adminPassword: password)
    .AddRealm(r =>
    {
        r.Clients = new ConcurrentBag<ClientRepresentation>
        {
            new() { ClientId = "neox-spa", PublicClient = true, Enabled = true }
        };
    }, realm: "neox")
    .WithOrganization("Acme", "acme.com")
    .AddJwtClient("api-client", "neox-api", apiClientSecret)
    .WithRedirectUrl(new Uri("https://localhost/swagger/oauth2-redirect.html"));

builder.AddProject<Projects.Api>("api")
    .WithReference(keycloak)
    .WithKeycloakJwtBearer(apiClient);
```

`AddRealm` writes `.aspire/keycloak-realms/{keycloakName}/{realm}-realm.json` under the AppHost directory and wires upstream `WithRealmImport` once per Keycloak container.

`WithOrganizations` enables the organizations feature on the realm JSON. `WithOrganization(name, domain)` adds an organization and domain idempotently (can be chained after `AddRealm`).

`AddJwtClient` registers a confidential JWT client on the realm (realm JSON + child `KeycloakJwtClientResource`) with an `oidc-audience-mapper` so access tokens include the configured audience. `WithRedirectUrl` accumulates OAuth2 redirect URIs and web origins for Swagger or web clients (enables standard flow). `WithKeycloakJwtBearer` projects `Keycloak__*` environment variables and `WaitFor`s Keycloak and the realm.

`WithKeycloakSpa` is reserved for future SPA env projection; it is currently a no-op.

## See also

- [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/)
