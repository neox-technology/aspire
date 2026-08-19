# Neox.Aspire.Hosting.Keycloak

`Neox.Aspire.Hosting.Keycloak` provides Aspire hosting extensions to inject
Keycloak settings into API and SPA resources from a single model resource.

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
