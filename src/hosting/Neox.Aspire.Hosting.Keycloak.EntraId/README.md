# Neox.Aspire.Hosting.Keycloak.EntraId

Bridge package that registers a Microsoft Entra ID **app registration** as a Keycloak
OIDC **identity provider** on a Neox Keycloak realm.

## Quick Start

```csharp
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Keycloak;
using Neox.Aspire.Hosting.Keycloak.EntraId;

var entraSecret = builder.AddParameter("entra-client-secret", secret: true);
var entraApp = builder.AddAzureAppRegistration("saas-auth-entra")
    .WithSecret(entraSecret);

var keycloak = builder.AddKeycloak("auth", adminPassword: password);
var realm = keycloak.AddRealm(realm: "master");

realm.AddEntraIdIdentityProvider(
    EntraIdInstance.Workforce,
    entraApp,
    alias: "microsoft",
    displayName: "Microsoft",
    brokerRedirectUri: new Uri("https://localhost/realms/master/broker/microsoft/endpoint"));
```

`AddEntraIdIdentityProvider`:

- Requires at least one `WithSecret` on the app registration.
- Calls Keycloak `AddIdentityProvider` with `providerId` `oidc` (realm child / `IResourceWithParent`).
- Creates a non-parented config gate (`{idpName}-config`) that `WaitFor`s the registration and
  password credentials, then at initialize fetches Entra OIDC discovery metadata and writes
  `clientId`, `clientSecret`, discovery URL, and resolved endpoint URLs (`authorizationUrl`,
  `tokenUrl`, `jwksUrl`, `issuer`, …) into realm JSON.
- Writes realm `identityProviderMappers` for the IdP alias so `email` and `preferred_username`
  claims map to the user `email` attribute (`oidc-user-attribute-idp-mapper`, `syncMode=INHERIT`).
- Makes the Keycloak container `WaitFor` the config gate (not the IdP child — Aspire forbids
  waiting on a parented descendant) so realm import sees the final file.
- When `brokerRedirectUri` is set, registers an Entra web application `{alias}-broker` with that URI.

Requires AppHost configuration `Azure:TenantId` for the discovery endpoint path.
