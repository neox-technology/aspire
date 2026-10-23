using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Neox.Azure.Provisioning.Graph;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire hosting helpers for Microsoft Entra ID app registrations.
/// </summary>
public static class AzureEntraIdHostingExtensions {

    /// <summary>
    /// Space-separated MSAL interactive login scopes injected as <c>{sectionName}LoginScopes</c>
    /// for a public-client SPA. Scopes of other app registrations are injected as
    /// <c>{sectionName}Scopes__n</c>.
    /// </summary>
    public const string SpaLoginScopes = "openid profile offline_access";

    /// <summary>
    /// Exposes an app role on the Entra application (Graph <c>appRoles</c>).
    /// The parent app registration emits the collection in its Bicep module.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRoleResource> AddAppRole(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name,
        AllowedMemberTypes allowedMemberTypes,
        string value,
        string displayName,
        string description) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        AzureEntraIdAppRoleResource.ThrowIfInvalidMemberTypes(allowedMemberTypes);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentException.ThrowIfNullOrEmpty(description);

        if (builder.Resource.AppRoles.Exists(role => string.Equals(role.Value, value, StringComparison.Ordinal))) {
            throw new ArgumentException(
                $"An app role with value '{value}' is already defined on '{builder.Resource.Name}'.",
                nameof(value));
        }

        var role = new AzureEntraIdAppRoleResource(
            name,
            builder.Resource,
            allowedMemberTypes,
            value,
            displayName,
            description);

        builder.Resource.AppRoles.Add(role);
        return builder.ApplicationBuilder.AddResource(role);
    }

    /// <summary>
    /// Adds an Entra ID app registration provisioned through Microsoft Graph Bicep.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Aspire resource name; also the default Graph <c>uniqueName</c> and display name.</param>
    /// <returns>A resource builder for the app registration.</returns>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> AddAzureAppRegistration(
        this IDistributedApplicationBuilder builder,
        string name) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddAzureProvisioning();

        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(AzureEntraIdDeletedApplicationRestorer))) {
            builder.Services.AddSingleton<AzureEntraIdDeletedApplicationRestorer>();
            builder.OnBeforeStart(async (start, cancellationToken) => {
                await start.Services.GetRequiredService<AzureEntraIdDeletedApplicationRestorer>()
                    .RestoreAsync(start.Services, start.Model, cancellationToken)
                    .ConfigureAwait(false);
            });
        }

        var resource = new AzureEntraIdAppRegistrationResource(name);
        var identity = new AzureEntraIdApplicationIdentityResource(resource);

        resource.IdentityResource = identity;

        var registration = builder.AddResource(resource)
            .WithManifestPublishingCallback(resource.WriteToManifest)
            .WithParameter(AzureEntraIdAppRegistrationResource.UniqueNameParameter, name)
            .WithParameter(AzureEntraIdAppRegistrationResource.DisplayNameParameter, name)
            .WithParameter(AzureEntraIdAppRegistrationResource.RedirectUrisParameter, Array.Empty<string>());

        builder.AddResource(identity)
            .WaitFor(registration);

        return registration;
    }

    /// <summary>
    /// Exposes an OAuth2 permission scope that requires admin consent (Graph <c>type: Admin</c>).
    /// The parent app registration emits <c>api.oauth2PermissionScopes</c> in its Bicep module.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdScopeResource> AddScope(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name,
        string value,
        string adminConsentDisplayName,
        string adminConsentDescription) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDescription);

        return AddScopeCore(
            builder,
            name,
            value,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName: null,
            userConsentDescription: null);
    }

    /// <summary>
    /// Exposes an OAuth2 permission scope that allows user consent (Graph <c>type: User</c>).
    /// The parent app registration emits <c>api.oauth2PermissionScopes</c> in its Bicep module.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdScopeResource> AddScope(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name,
        string value,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string userConsentDisplayName,
        string userConsentDescription) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDescription);
        ArgumentException.ThrowIfNullOrEmpty(userConsentDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(userConsentDescription);

        return AddScopeCore(
            builder,
            name,
            value,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName,
            userConsentDescription);
    }

    /// <summary>
    /// Registers a Graph SPA platform on the app registration. The parent emits
    /// <c>spa.redirectUris</c> in its Bicep module. Also requests the delegated
    /// Graph sign-in permissions <c>openid</c>, <c>profile</c> and <c>offline_access</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdSpaApplicationResource> AddSpaApplication(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddWellKnownPermissionIfMissing(builder, MicrosoftGraph.Delegated.Openid);
        AddWellKnownPermissionIfMissing(builder, MicrosoftGraph.Delegated.Profile);
        AddWellKnownPermissionIfMissing(builder, MicrosoftGraph.Delegated.OfflineAccess);

        var spa = new AzureEntraIdSpaApplicationResource(name, builder.Resource);

        builder.Resource.SpaApplications.Add(spa);
        return builder.ApplicationBuilder.AddResource(spa);
    }

    /// <summary>
    /// Registers a Graph web platform on the app registration. The parent emits
    /// <c>web.redirectUris</c> in its Bicep module (create path).
    /// </summary>
    public static IResourceBuilder<AzureEntraIdWebApplicationResource> AddWebApplication(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var web = new AzureEntraIdWebApplicationResource(name, builder.Resource);

        builder.Resource.WebApplications.Add(web);
        return builder.ApplicationBuilder.AddResource(web);
    }

    /// <summary>
    /// Sets the Application ID URI to <c>api://{appId}</c> in a follow-up module.
    /// The Graph extension does not return <c>appId</c> from <c>reference()</c> inside the
    /// deployment that creates the application.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithDefaultIdentifierUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _)) {
            builder.WithAnnotation(new AzureEntraIdDefaultIdentifierUriAnnotation());
        }

        if (builder.Resource.IdentifierUriResource is null) {
            var identifierUri = new AzureEntraIdIdentifierUriResource(builder.Resource);

            builder.Resource.IdentifierUriResource = identifierUri;
            builder.ApplicationBuilder.AddResource(identifierUri)
                .WaitFor(builder);
        }

        return builder;
    }

    /// <summary>
    /// Sets the Entra application display name (create path).
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithDisplayName(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string displayName) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(displayName);

        return builder.WithParameter(AzureEntraIdAppRegistrationResource.DisplayNameParameter, displayName);
    }

    /// <summary>
    /// Injects public-client SPA configuration (<c>ENTRA_*</c> by default) from
    /// an in-model SPA application and its parent app registration.
    /// </summary>
    public static IResourceBuilder<TResource> WithEntraIdSpaApplication<TResource>(
        this IResourceBuilder<TResource> builder,
        EntraIdInstance instance,
        IResourceBuilder<AzureEntraIdSpaApplicationResource> spaApplication,
        string sectionName = "ENTRA_")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(spaApplication);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        var spa = spaApplication.Resource;
        var parent = spa.Parent;

        instance.ApplyInstanceEnvironment(builder, sectionName);
        builder.WithEnvironment($"{sectionName}ClientId", parent.ClientId);
        builder.WithEnvironment($"{sectionName}LoginScopes", SpaLoginScopes);

        var tenantId = builder.ApplicationBuilder.Configuration["Azure:TenantId"];

        if (!string.IsNullOrEmpty(tenantId)) {
            builder.WithEnvironment($"{sectionName}TenantId", tenantId);
        }

        if (parent.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _)) {
            builder.WithEnvironment(
                $"{sectionName}Audience",
                ReferenceExpression.Create($"api://{parent.ClientId}"));

            if (parent.IdentifierUriResource is { } identifierUri &&
                !builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, identifierUri))) {
                builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(identifierUri));
            }
        }

        var index = 0;

        foreach (var scope in parent.RequiredPermissions.OfType<AzureEntraIdScopeResource>()) {
            if (ReferenceEquals(scope.Parent, parent)) {
                continue;
            }

            builder.WithEnvironment(
                $"{sectionName}Scopes__{index}",
                ReferenceExpression.Create($"api://{scope.Parent.ClientId}/{scope.Value}"));
            index++;
        }

        var parentBuilder = builder.ApplicationBuilder.CreateResourceBuilder(parent);

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent))) {
            builder.WaitFor(parentBuilder);
        }

        return builder;
    }

    /// <summary>
    /// Adds an Application ID URI (<c>identifierUris</c>, create path). Call multiple times to accumulate URIs.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithIdentifierUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        Uri identifierUri) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identifierUri);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdIdentifierUrisAnnotation>(out var annotation)) {
            annotation = new AzureEntraIdIdentifierUrisAnnotation();
            builder.WithAnnotation(annotation);
        }

        var uriString = identifierUri.AbsoluteUri;

        if (uriString.EndsWith("/")) {
            uriString = uriString.TrimEnd('/');
        }

        annotation.IdentifierUris.Add(uriString);

        return builder.WithParameter(
            AzureEntraIdAppRegistrationResource.IdentifierUrisParameter,
            annotation.IdentifierUris.ToArray());
    }

    /// <summary>
    /// Injects Microsoft.Identity.Web configuration (<c>AzureAd__*</c> by default) from
    /// an in-model web application and its parent app registration.
    /// <paramref name="clientSecret"/> is injected as <c>{sectionName}ClientSecret</c>.
    /// </summary>
    public static IResourceBuilder<TResource> WithMicrosoftIdentityWebApplication<TResource>(
        this IResourceBuilder<TResource> builder,
        EntraIdInstance instance,
        IResourceBuilder<AzureEntraIdWebApplicationResource> webApplication,
        IResourceBuilder<ParameterResource> clientSecret,
        string sectionName = "AzureAd__")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(webApplication);
        ArgumentNullException.ThrowIfNull(clientSecret);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        var web = webApplication.Resource;
        var parent = web.Parent;

        instance.ApplyInstanceEnvironment(builder, sectionName);
        builder.WithEnvironment($"{sectionName}ClientId", parent.ClientId);
        builder.WithEnvironment($"{sectionName}ClientSecret", clientSecret);

        var tenantId = builder.ApplicationBuilder.Configuration["Azure:TenantId"];

        if (!string.IsNullOrEmpty(tenantId)) {
            builder.WithEnvironment($"{sectionName}TenantId", tenantId);
        }

        if (parent.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _)) {
            builder.WithEnvironment(
                $"{sectionName}Audience",
                ReferenceExpression.Create($"api://{parent.ClientId}"));

            if (parent.IdentifierUriResource is { } identifierUri &&
                !builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, identifierUri))) {
                builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(identifierUri));
            }
        }

        var parentBuilder = builder.ApplicationBuilder.CreateResourceBuilder(parent);

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent))) {
            builder.WaitFor(parentBuilder);
        }

        return builder;
    }

    /// <summary>
    /// (create path). Waits for the exposer app registration unless the permission is on this resource.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithPermission<TAzureEntraIdPermissibleResource>(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        IResourceBuilder<TAzureEntraIdPermissibleResource> permission)
        where TAzureEntraIdPermissibleResource : IAzureEntraIdPermissibleResource {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        var resource = permission.Resource;

        if (builder.Resource.HasRequiredPermission(resource.Id)) {
            throw new ArgumentException(
                $"A required permission with id '{resource.Id:D}' is already defined on '{builder.Resource.Name}'.",
                nameof(permission));
        }

        builder.Resource.RequiredPermissions.Add(resource);

        var parent = resource.Parent;

        if (ReferenceEquals(parent, builder.Resource)) {
            return builder;
        }

        var parameterName = AzureEntraIdAppRegistrationResource.ResourceAppIdParameterName(parent.Name);

        if (!builder.Resource.Parameters.ContainsKey(parameterName)) {
            builder.WithParameter(parameterName, parent.ClientId);
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent))) {
            builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(parent));
        }

        return builder;
    }

    /// <summary>
    /// Requests a first-party API permission (create path) as Graph <c>requiredResourceAccess</c>.
    /// Permissions that share a resource application id are grouped. Does not <c>WaitFor</c> another resource.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithPermission(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        AzureEntraIdWellKnownPermission permission) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        if (builder.Resource.HasRequiredPermission(permission.Id)) {
            throw new ArgumentException(
                $"A required permission with id '{permission.Id:D}' is already defined on '{builder.Resource.Name}'.",
                nameof(permission));
        }

        builder.Resource.WellKnownPermissions.Add(permission);
        return builder;
    }

    /// <summary>
    /// Adds a web platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string uri) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(uri);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdRedirectUrisAnnotation>(out var annotation)) {
            annotation = new AzureEntraIdRedirectUrisAnnotation();
            builder.WithAnnotation(annotation);
        }

        annotation.RedirectUris.Add(uri);
        return builder.WithParameter(
            AzureEntraIdAppRegistrationResource.RedirectUrisParameter,
            builder.Resource.CollectRedirectUris());
    }

    /// <summary>
    /// Adds a web platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// The parent app registration merges this URI into Graph <c>web.redirectUris</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdWebApplicationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdWebApplicationResource> builder,
        Uri uri) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.RedirectUris.Add(uri);

        var parent = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);

        return SyncParentRedirectUris(parent, builder);
    }

    /// <summary>
    /// Adds a SPA platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// The parent app registration merges this URI into Graph <c>spa.redirectUris</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdSpaApplicationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdSpaApplicationResource> builder,
        Uri uri) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.RedirectUris.Add(uri);

        var parent = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);

        return SyncParentSpaRedirectUris(parent, builder);
    }

    /// <summary>
    /// Records this registration as a known client of <paramref name="api"/>.
    /// The client id is written to <c>api.knownClientApplications</c> by a follow-up module
    /// that runs after both registrations exist. Aspire 13.6 waits for every Bicep output
    /// referenced by a module, so placing that output on the API module deadlocks when the
    /// client also waits for the API.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> AsKnownClientOf(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> client,
        IResourceBuilder<AzureEntraIdAppRegistrationResource> api) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(api);

        if (ReferenceEquals(client.Resource, api.Resource)) {
            throw new InvalidOperationException(
                $"App registration '{client.Resource.Name}' cannot be a known client of itself.");
        }

        var knownClients = EnsureKnownClientsResource(api);

        if (knownClients.Resource.Clients.Contains(client.Resource)) {
            throw new InvalidOperationException(
                $"App registration '{client.Resource.Name}' is already a known client of '{api.Resource.Name}'.");
        }

        knownClients.Resource.Clients.Add(client.Resource);
        knownClients.WaitFor(client);

        if (!client.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, api.Resource))) {
            client.WaitFor(api);
        }

        api.Resource.KnownClientCallbacks.Add(infrastructure => {
            var application = infrastructure.GetProvisionableResources()
                .OfType<GraphApplication>()
                .Single(app => app.BicepIdentifier == AzureEntraIdAppRegistrationResource.GraphApplicationBicepIdentifier);

            application.Api ??= new GraphApiApplication();

            var parameterName = "knownClient_" + client.Resource.Name.Replace("-", "_", StringComparison.Ordinal);
            application.Api.KnownClientApplications.Add(
                client.Resource.ClientId.AsProvisioningParameter(infrastructure, parameterName));
        });

        return client;
    }

    /// <summary>
    /// Sets Graph <c>signInAudience</c> (create path). Default is
    /// <see cref="SupportedAccountType.AzureADMyOrg"/> when this method is not called.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithSupportedAccountType(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        SupportedAccountType supportedAccountType) {
        ArgumentNullException.ThrowIfNull(builder);

        if (!Enum.IsDefined(supportedAccountType)) {
            throw new ArgumentOutOfRangeException(nameof(supportedAccountType), supportedAccountType, null);
        }

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdSupportedAccountTypeAnnotation>(out var annotation)) {
            annotation = new AzureEntraIdSupportedAccountTypeAnnotation();
            builder.WithAnnotation(annotation);
        }

        annotation.SupportedAccountType = supportedAccountType;
        return builder;
    }

    private static IResourceBuilder<AzureEntraIdKnownClientsResource> EnsureKnownClientsResource(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> api) {
        if (api.Resource.KnownClientsResource is { } existing) {
            return api.ApplicationBuilder.CreateResourceBuilder(existing);
        }

        var resource = new AzureEntraIdKnownClientsResource(api.Resource);

        api.Resource.KnownClientsResource = resource;

        return api.ApplicationBuilder.AddResource(resource)
            .WaitFor(api);
    }

    private static void AddWellKnownPermissionIfMissing(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        AzureEntraIdWellKnownPermission permission) {
        if (builder.Resource.HasRequiredPermission(permission.Id)) {
            return;
        }

        builder.WithPermission(permission);
    }

    private static IResourceBuilder<AzureEntraIdScopeResource> AddScopeCore(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name,
        string value,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName,
        string? userConsentDescription) {
        if (builder.Resource.Scopes.Exists(scope => string.Equals(scope.Value, value, StringComparison.Ordinal))) {
            throw new ArgumentException(
                $"An OAuth2 permission scope with value '{value}' is already defined on '{builder.Resource.Name}'.",
                nameof(value));
        }

        var scope = new AzureEntraIdScopeResource(
            name,
            builder.Resource,
            value,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName,
            userConsentDescription);

        builder.Resource.Scopes.Add(scope);
        return builder.ApplicationBuilder.AddResource(scope);
    }

    private static IResourceBuilder<AzureEntraIdWebApplicationResource> SyncParentRedirectUris(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> parent,
        IResourceBuilder<AzureEntraIdWebApplicationResource> web) {
        parent.WithParameter(
            AzureEntraIdAppRegistrationResource.RedirectUrisParameter,
            parent.Resource.CollectRedirectUris());
        return web;
    }

    private static IResourceBuilder<AzureEntraIdSpaApplicationResource> SyncParentSpaRedirectUris(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> parent,
        IResourceBuilder<AzureEntraIdSpaApplicationResource> spa) {
        parent.WithParameter(
            AzureEntraIdAppRegistrationResource.SpaRedirectUrisParameter,
            parent.Resource.CollectSpaRedirectUris());
        return spa;
    }
}

internal sealed class AzureEntraIdDefaultIdentifierUriAnnotation : IResourceAnnotation {
}

internal sealed class AzureEntraIdIdentifierUrisAnnotation : IResourceAnnotation {
    public List<string> IdentifierUris { get; } = [];
}

internal sealed class AzureEntraIdRedirectUrisAnnotation : IResourceAnnotation {
    public List<string> RedirectUris { get; } = [];
}

internal sealed class AzureEntraIdSupportedAccountTypeAnnotation : IResourceAnnotation {
    public SupportedAccountType SupportedAccountType { get; set; } = SupportedAccountType.AzureADMyOrg;
}