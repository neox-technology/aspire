# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/)
with realm import helpers and env projection for API and SPA resources.

## Quick Start

```csharp
var apiClientSecret = builder.AddParameter("keycloak-api-client-secret", "secret", secret: true);

var keycloak = builder.AddKeycloak("auth", adminPassword: password);

var apiClient = keycloak
    .AddRealm(r =>
    {
        r.Clients = new ConcurrentBag<ClientRepresentation>
        {
            new() { ClientId = "neox-spa", PublicClient = true, Enabled = true }
        };
    }, realm: "neox")
    .WithOrganization("Acme", "acme.com")
    .AddJwtClient("api-client", "neox-api", apiClientSecret)
    .WithLocalRedirectUri("/swagger/oauth2-redirect.html");

builder.AddProject<Projects.Api>("api")
    .WithReference(keycloak)
    .WithKeycloakJwtBearer(apiClient);
```

In the API project, override `Authority` when using `Aspire.Keycloak.Authentication.AddKeycloakJwtBearer` (upstream defaults to `https+http://{serviceName}/realms/{realm}`):

```csharp
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: builder.Configuration["Keycloak:ServiceName"]!,
        realm: builder.Configuration["Keycloak:Realm"]!,
        options =>
        {
            options.Authority = builder.Configuration["Keycloak:Authority"];
            options.Audience = builder.Configuration["Keycloak:Audience"];
        });
```

`AddRealm` writes `.aspire/keycloak-realms/{keycloakName}/{realm}-realm.json` under the AppHost directory and wires upstream `WithRealmImport` once per Keycloak container.

`WithOrganizations` enables the organizations feature on the realm JSON. `WithOrganization(name, domain)` adds an organization and domain idempotently (can be chained after `AddRealm`).

`AddJwtClient` registers a confidential JWT client on the realm (realm JSON + child `KeycloakJwtClientResource`) with an `oidc-audience-mapper` so access tokens include the configured audience.

`WithRedirectUrl(Uri)` accumulates exact OAuth2 redirect URIs and matching `webOrigins` (production or fixed-port dev URLs).

`WithLocalRedirectUri(path)` (Development only) registers HTTP loopback redirect URIs without port (`127.0.0.1`, `localhost`, `[::1]`) so Keycloak accepts Aspire ephemeral ports, and adds `webOrigins: *` for Swagger CORS.

`WithKeycloakJwtBearer` projects `Keycloak__AuthServerUrl`, `Keycloak__Authority`, and related env vars from the Keycloak **HTTPS** endpoint (fallback HTTP), and `WaitFor`s Keycloak and the realm.

`WithKeycloakSpa` is reserved for future SPA env projection; it is currently a no-op.

## See also

- [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/)
