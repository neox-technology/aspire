# Neox.Aspire.Hosting.Azure.EntraId

Aspire hosting helpers for **Microsoft Entra ID** app registrations.

This package is **Shipping**. App registrations are Aspire Azure resources (`AzureProvisioningResource`) compiled from [`Neox.Azure.Provisioning.Graph`](../../provisioning/Neox.Azure.Provisioning.Graph/README.md) constructs and deployed through the Microsoft Graph Bicep extension. There is no Graph SDK plan/provision pipeline or generic `WithAuth` helper (`WithMicrosoftIdentityWebApplication` injects Microsoft.Identity.Web env; `WithEntraIdSpaApplication` injects `ENTRA_*` for a public-client SPA).

## Install

```bash
dotnet add package Neox.Aspire.Hosting.Azure.EntraId
```

```xml
<PackageReference Include="Neox.Aspire.Hosting.Azure.EntraId" Version="1.0.0-preview.*" />
```

## AddAzureAppRegistration

```csharp
var api = builder.AddAzureAppRegistration("api")
    .WithDisplayName("My API")
    .WithDefaultIdentifierUri();

var swagger = api.AddWebApplication("swagger")
    .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));

builder.AddProject<Projects.Api>("api")
    .WithMicrosoftIdentityWebApplication(EntraIdInstance.Workforce, swagger);
```

`uniqueName` and `displayName` default to the Aspire resource name. `WithSupportedAccountType` sets Graph `signInAudience` (default `AzureADMyOrg`). `AzureADandPersonalMicrosoftAccount` and `PersonalMicrosoftAccount` also emit `api.requestedAccessTokenVersion: 2` on create (Graph requires it). `WithIdentifierUri` adds Application ID URIs; `WithDefaultIdentifierUri` sets `api://{appId}` in the same module via a second Graph application (same `uniqueName`) that interpolates `app.appId` after create and repeats `signInAudience` plus `api.requestedAccessTokenVersion: 2` (Graph rejects `0`). Outputs: `clientId` (`app.appId`), `objectId` (`app.id`). Customize Graph constructs with `.ConfigureInfrastructure`. `WithRedirectUri(string)` on the app registration still accumulates Graph `web.redirectUris`.

```csharp
var api = builder.AddAzureAppRegistration("api")
    .WithSupportedAccountType(SupportedAccountType.AzureADMultipleOrgs);
```

### Web applications

`AddWebApplication` registers an `AzureEntraIdWebApplicationResource` child (`IResourceWithParent`). The **app registration** emits `web.redirectUris` in its Bicep module (create path only). `WithRedirectUri(Uri)` on the child accumulates; the parent merges unique URIs from children and from `WithRedirectUri(string)` on the registration. When at least one URI is present, create Bicep also sets implicit grant (`enableAccessTokenIssuance` / `enableIdTokenIssuance`).

```csharp
var swagger = api.AddWebApplication("swagger")
    .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));
```

### SPA applications

`AddSpaApplication` registers an `AzureEntraIdSpaApplicationResource` child (`IResourceWithParent`). The **app registration** emits `spa.redirectUris` in its Bicep module (create path only, when at least one URI is present). `WithRedirectUri(Uri)` on the child accumulates. There is no implicit grant (auth code + PKCE).

```csharp
var spaApp = spa.AddSpaApplication("spa-app")
    .WithRedirectUri(new Uri("http://localhost/"));
```

### Microsoft.Identity.Web

`WithMicrosoftIdentityWebApplication` on an `IResourceWithEnvironment` injects `{sectionName}*` env keys (default `AzureAd__`) for `AddMicrosoftIdentityWebApi`. The first argument is an `EntraIdInstance`: `Workforce` (`https://login.microsoftonline.com/`) or `Ciam` (`https://{subdomain}.ciamlogin.com/` from a string or an Aspire parameter). It also injects `TenantId` (AppHost `Azure:TenantId` when set), `ClientId` (parent output), `Audience` (`api://{clientId}` when `WithDefaultIdentifierUri`), and `ClientCredentials` from each `WithKeyCredential`. The project `WaitFor`s the app registration and certs. The hosting package does not reference Microsoft.Identity.Web.

```csharp
builder.AddProject<Projects.Api>("api")
    .WithMicrosoftIdentityWebApplication(EntraIdInstance.Workforce, swagger);

var ciamSubdomain = builder.AddParameter("ciam-subdomain");
builder.AddProject<Projects.Api>("api")
    .WithMicrosoftIdentityWebApplication(EntraIdInstance.Ciam(ciamSubdomain), swagger);
```

### Public-client SPA (`ENTRA_*`)

`WithEntraIdSpaApplication` on an `IResourceWithEnvironment` injects `{sectionName}*` env keys (default `ENTRA_`) for MSAL. The first argument is an `EntraIdInstance` (`Workforce` or `Ciam`). It also injects `TenantId` (AppHost `Azure:TenantId` when set), `ClientId` (parent output), `Audience` (`api://{clientId}` when `WithDefaultIdentifierUri`), `LoginScopes` (`openid offline_access` for interactive login), and `Scope` (`api://{exposerClientId}/{value}` from the first in-model Scope permission, for token acquisition). It does not inject client credentials. The project `WaitFor`s the app registration (not certs). The hosting package does not reference MSAL. Vite apps should set `envPrefix` to include `ENTRA_`.

```csharp
builder.AddViteApp("spa", "../spa")
    .WithEntraIdSpaApplication(EntraIdInstance.Workforce, spaApp);

builder.AddViteApp("spa", "../spa")
    .WithEntraIdSpaApplication(EntraIdInstance.Ciam(ciamSubdomain), spaApp);
```

### OAuth2 permission scopes

`AddScope` registers an `AzureEntraIdScopeResource` child (`IResourceWithParent`). The **app registration** emits `api.oauth2PermissionScopes` in its Bicep module (create path only). The four-argument overload is admin consent (`type: Admin`); the six-argument overload adds user-consent strings (`type: User`). Graph `id` is a stable GUID derived from `{appName}:{value}`.

```csharp
var web = builder.AddAzureAppRegistration("web")
    .WithDefaultIdentifierUri();

var access = web.AddScope(
    "access",
    "access_as_user",
    "Access API",
    "Allows the app to access the API");
```

### App roles

`AddAppRole` registers an `AzureEntraIdAppRoleResource` child (`IResourceWithParent`). The **app registration** emits `appRoles` in its Bicep module (create path only). `AllowedMemberTypes` is a `[Flags]` enum (`User`, `Application`, or both). Graph `id` is a stable GUID derived from `{appName}:{value}` (distinct from scope ids).

```csharp
var writer = web.AddAppRole(
    "writer",
    AllowedMemberTypes.User,
    "Writer",
    "Writer",
    "Can write data");

var daemon = web.AddAppRole(
    "daemon",
    AllowedMemberTypes.Application,
    "Daemon.Access",
    "Daemon access",
    "App-only access");
```

### API permissions

`WithPermission` requests an in-model scope or app role as Graph `requiredResourceAccess` on the **consumer** app registration (create path only). Pass the `IResourceBuilder` returned by `AddScope` / `AddAppRole`. Permissions against the same exposer are grouped; `resourceAppId` is the exposer’s `clientId` output. The consumer `WaitFor`s the exposer app registration (not the child).

```csharp
var api = builder.AddAzureAppRegistration("api")
    .WithDefaultIdentifierUri();

var access = api.AddScope(
    "access",
    "access_as_user",
    "Access API",
    "Allows the app to access the API");

var spa = builder.AddAzureAppRegistration("spa")
    .WithDefaultIdentifierUri()
    .WithPermission(access);

var spaApp = spa.AddSpaApplication("spa-app")
    .WithRedirectUri(new Uri("http://localhost/"));

builder.AddViteApp("frontend", "../spa")
    .WithEntraIdSpaApplication(EntraIdInstance.Workforce, spaApp);
```

### Certificates

`AddCertificate` registers an `AsymmetricX509CertResource` that checks `StoreName.My` at `StoreLocation.CurrentUser` or `LocalMachine` by thumbprint. The thumbprint comes from an Aspire `ParameterResource` (typically `secret: true`) and is resolved at initialize time. It does not create or upload the certificate. `WithKeyCredential` emits Graph `keyCredentials` (`type: AsymmetricX509Cert`, `usage: Verify`) on the **create** path and `WaitFor`s the cert. The public CER bytes are a deferred secure Bicep parameter.

```csharp
var thumbprint = builder.AddParameter("api-cert-thumbprint", secret: true);
var cert = builder.AddCertificate("api-cert", thumbprint, StoreLocation.CurrentUser);

var api = builder.AddAzureAppRegistration("api")
    .WithKeyCredential(cert);
```

Set the secret locally with `dotnet user-secrets set "Parameters:api-cert-thumbprint" "<thumbprint>"`.

Never upload the PFX or private key to Entra. Existing applications omit `keyCredentials` (same as scopes / roles). Graph `keyCredentials` replaces the whole collection: every `WithKeyCredential` on the model is emitted together.

### Client secrets (`WithSecret`)

Graph Bicep cannot create `passwordCredentials`. `WithSecret` creates an `EntraIdPasswordCredentialResource` child (`IResourceWithParent`). Aspire forbids `WaitFor` on a parent; run-mode initialize waits for the registration via `ResourceNotificationService`, then calls Graph REST `addPassword`, fills the parameter, and persists `Parameters:{name}` to AppHost user secrets when missing. Re-runs reuse an existing config/user-secret value. `WithMicrosoftIdentityWebApplication` and `WithEntraIdSpaApplication` inject `{sectionName}ClientSecret` from the first password credential and `WaitFor` each credential resource.

```csharp
var secret = builder.AddParameter("api-client-secret", secret: true);
var api = builder.AddAzureAppRegistration("api")
    .WithSecret(secret);

builder.AddProject<Projects.Api>("api")
    .WithMicrosoftIdentityWebApplication(EntraIdInstance.Workforce, swagger);
```

Requires Microsoft Graph **`Application.ReadWrite.All`** (same as app registration deploy).

### Existing applications

Aspire 13.4.6 `RunAsExisting` / `PublishAsExisting` / `AsExisting` apply. The annotation **name** is the Graph `uniqueName` (not an ARM resource name). Omit resource group (tenant object; ARM resource-group deployment is only the vehicle).

```csharp
var existingName = builder.AddParameter("existingAppUniqueName");
builder.AddAzureAppRegistration("web")
    .AsExisting(existingName, resourceGroupParameter: null);
```

## Azure configuration and Graph permissions

Same local provisioning settings as other Aspire Azure resources:

- `Azure:SubscriptionId`
- `Azure:Location`
- `Azure:ResourceGroup`

The deploying identity also needs Microsoft Graph **`Application.ReadWrite.All`**. Resource-group Contributor is not enough. The same Graph permission covers run-mode `WithSecret` (`addPassword`).

## Sample AppHost

See [`tests/azure-entraid/`](../../../tests/azure-entraid/) (Auth group + stub Web API + Vite React SPA).

## Spec

See [`specs/features/azure-entra-id.md`](../../../specs/features/azure-entra-id.md).
