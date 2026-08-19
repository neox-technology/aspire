# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` provides Aspire hosting extensions to inject
Keycloak settings into API and SPA resources from a single model resource.

This package references upstream [`Aspire.Hosting.Keycloak`](https://aspire.dev/fr/integrations/security/keycloak/)
for future composition with the official Keycloak container resource. Neox helpers
focus on env projection (`AddKeycloakRealm`, `WithKeycloakWebApi`, `WithKeycloakSpa`).

## Quick Start

```csharp
var auth = builder.AddKeycloakRealm(
    name: "auth",
    realm: "neox",
    authServerUrl: "http://localhost:8080",
    clientId: "neox-spa");

builder.AddProject<Projects.Api>("api")
    .WithKeycloakWebApi(auth);
```

## See also

- [Keycloak integration (Aspire)](https://aspire.dev/fr/integrations/security/keycloak/)
