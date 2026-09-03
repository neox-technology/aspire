# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` extends upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/)
with realm import helpers and env projection for API and SPA resources.

## Quick Start

```csharp
var apiClientSecret = builder.AddParameter("keycloak-api-client-secret", "secret", secret: true);

var keycloak = builder.AddKeycloak("auth", adminPassword: password);

var realm = keycloak
    .AddRealm(realm: "neox")
    .WithOrganization("Acme", "acme.com");

var apiClient = realm
    .AddJwtClient("api-client", "neox-api", apiClientSecret)
    .WithLocalRedirectUri("/swagger/oauth2-redirect.html");

var spaClient = realm
    .AddOidcClient("spa-client", "neox-spa")
    .WithLocalRedirectUri("/");

realm.AddIdentityProvider("microsoft", "oidc", idp =>
{
    idp.DisplayName = "Microsoft";
});

builder.AddProject<Projects.Api>("api")
    .WithReference(keycloak)
    .WithKeycloakJwtBearer(apiClient);

builder.AddViteApp("spa", "../spa")
    .WithReference(keycloak)
    .WithKeycloakSpa(spaClient);
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

`AddOidcClient` registers a public OIDC client on the realm (realm JSON + child `KeycloakOidcClientResource`) for browser/SPA auth code + PKCE flows. No client secret or audience mapper.

`AddIdentityProvider(alias, providerId, configure?)` registers a Keycloak identity provider in realm JSON (`identityProviders`) and a child `KeycloakIdentityProviderResource`. Upserts by alias. Optional `configure` mutates `IdentityProviderRepresentation` (including `config`).

`WithRedirectUrl(Uri)` on JWT or OIDC clients accumulates exact OAuth2 redirect URIs and matching `webOrigins` (production or fixed-port dev URLs).

`WithLocalRedirectUri(path)` (Development only) registers HTTP loopback redirect URIs without port (`127.0.0.1`, `localhost`, `[::1]`) so Keycloak accepts Aspire ephemeral ports, and adds `webOrigins: *` for browser CORS.

`WithKeycloakJwtBearer` projects `Keycloak__AuthServerUrl`, `Keycloak__Authority`, and related env vars from the Keycloak **HTTPS** endpoint (fallback HTTP), and `WaitFor`s Keycloak and the realm.

`WithKeycloakSpa` projects `KEYCLOAK_URL`, `KEYCLOAK_REALM`, and `KEYCLOAK_CLIENT_ID` from the OIDC client and Keycloak endpoint, and `WaitFor`s Keycloak and the realm.

## See also

- [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/)
