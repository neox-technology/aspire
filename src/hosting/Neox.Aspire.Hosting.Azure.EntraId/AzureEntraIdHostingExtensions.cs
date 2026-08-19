using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire hosting helpers for Microsoft Entra ID app registrations.
/// </summary>
public static class AzureEntraIdHostingExtensions
{
    /// <summary>
    /// Space-separated MSAL interactive login scopes injected as <c>{sectionName}LoginScopes</c>
    /// for a public-client SPA. API resource scopes stay on <c>{sectionName}Scope</c>
    /// (requesting them at login fails with AADSTS500207 on External ID).
    /// </summary>
    public const string SpaLoginScopes = "openid offline_access";

    /// <summary>
    /// Adds an Entra ID app registration provisioned through Microsoft Graph Bicep.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Aspire resource name; also the default Graph <c>uniqueName</c> and display name.</param>
    /// <returns>A resource builder for the app registration.</returns>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> AddAzureAppRegistration(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddAzureProvisioning();

        var resource = new AzureEntraIdAppRegistrationResource(name);
        return builder.AddResource(resource)
            .WithManifestPublishingCallback(resource.WriteToManifest)
            .WithParameter(AzureEntraIdAppRegistrationResource.UniqueNameParameter, name)
            .WithParameter(AzureEntraIdAppRegistrationResource.DisplayNameParameter, name)
            .WithParameter(AzureEntraIdAppRegistrationResource.RedirectUrisParameter, Array.Empty<string>());
    }

    /// <summary>
    /// Sets Graph <c>signInAudience</c> (create path). Default is
    /// <see cref="SupportedAccountType.AzureADMyOrg"/> when this method is not called.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithSupportedAccountType(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        SupportedAccountType supportedAccountType)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(supportedAccountType))
        {
            throw new ArgumentOutOfRangeException(nameof(supportedAccountType), supportedAccountType, null);
        }

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdSupportedAccountTypeAnnotation>(out var annotation))
        {
            annotation = new AzureEntraIdSupportedAccountTypeAnnotation();
            builder.WithAnnotation(annotation);
        }

        annotation.SupportedAccountType = supportedAccountType;
        return builder;
    }

    /// <summary>
    /// Sets the Entra application display name (create path).
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithDisplayName(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(displayName);

        return builder.WithParameter(AzureEntraIdAppRegistrationResource.DisplayNameParameter, displayName);
    }

    /// <summary>
    /// Adds a web platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string uri)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(uri);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdRedirectUrisAnnotation>(out var annotation))
        {
            annotation = new AzureEntraIdRedirectUrisAnnotation();
            builder.WithAnnotation(annotation);
        }

        annotation.RedirectUris.Add(uri);
        return builder.WithParameter(
            AzureEntraIdAppRegistrationResource.RedirectUrisParameter,
            builder.Resource.CollectRedirectUris());
    }

    /// <summary>
    /// Adds an Application ID URI (<c>identifierUris</c>, create path). Call multiple times to accumulate URIs.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithIdentifierUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        Uri identifierUri)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identifierUri);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdIdentifierUrisAnnotation>(out var annotation))
        {
            annotation = new AzureEntraIdIdentifierUrisAnnotation();
            builder.WithAnnotation(annotation);
        }

        var uriString = identifierUri.AbsoluteUri;
        if (uriString.EndsWith("/"))
        {
            uriString = uriString.TrimEnd('/');
        }
        annotation.IdentifierUris.Add(uriString);
   
        return builder.WithParameter(
            AzureEntraIdAppRegistrationResource.IdentifierUrisParameter,
            annotation.IdentifierUris.ToArray());
    }

    /// <summary>
    /// Sets the Application ID URI to <c>api://{appId}</c> via a second Graph application
    /// in the same module (upsert on <c>uniqueName</c>) so Bicep can interpolate <c>app.appId</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithDefaultIdentifierUri(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Resource.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _))
        {
            builder.WithAnnotation(new AzureEntraIdDefaultIdentifierUriAnnotation());
        }

        return builder;
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
        string adminConsentDescription)
    {
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
        string userConsentDescription)
    {
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

    private static IResourceBuilder<AzureEntraIdScopeResource> AddScopeCore(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name,
        string value,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName,
        string? userConsentDescription)
    {
        if (builder.Resource.Scopes.Exists(scope => string.Equals(scope.Value, value, StringComparison.Ordinal)))
        {
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
        string description)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        AzureEntraIdAppRoleResource.ThrowIfInvalidMemberTypes(allowedMemberTypes);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentException.ThrowIfNullOrEmpty(description);

        if (builder.Resource.AppRoles.Exists(role => string.Equals(role.Value, value, StringComparison.Ordinal)))
        {
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
    /// Registers a Graph web platform on the app registration. The parent emits
    /// <c>web.redirectUris</c> in its Bicep module (create path).
    /// </summary>
    public static IResourceBuilder<AzureEntraIdWebApplicationResource> AddWebApplication(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var web = new AzureEntraIdWebApplicationResource(name, builder.Resource);
        builder.Resource.WebApplications.Add(web);
        return builder.ApplicationBuilder.AddResource(web);
    }

    /// <summary>
    /// Adds a web platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// The parent app registration merges this URI into Graph <c>web.redirectUris</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdWebApplicationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdWebApplicationResource> builder,
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.RedirectUris.Add(uri);
        var parent = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);
        return SyncParentRedirectUris(parent, builder);
    }

    private static IResourceBuilder<AzureEntraIdWebApplicationResource> SyncParentRedirectUris(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> parent,
        IResourceBuilder<AzureEntraIdWebApplicationResource> web)
    {
        parent.WithParameter(
            AzureEntraIdAppRegistrationResource.RedirectUrisParameter,
            parent.Resource.CollectRedirectUris());
        return web;
    }

    /// <summary>
    /// Injects Microsoft.Identity.Web configuration (<c>AzureAd__*</c> by default) from
    /// an in-model web application and its parent app registration.
    /// </summary>
    public static IResourceBuilder<TResource> WithMicrosoftIdentityWebApplication<TResource>(
        this IResourceBuilder<TResource> builder,
        EntraIdInstance instance,
        IResourceBuilder<AzureEntraIdWebApplicationResource> webApplication,
        string sectionName = "AzureAd__")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(webApplication);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        var web = webApplication.Resource;
        var parent = web.Parent;

        instance.ApplyInstanceEnvironment(builder, sectionName);
        builder.WithEnvironment($"{sectionName}ClientId", parent.ClientId);

        var tenantId = builder.ApplicationBuilder.Configuration["Azure:TenantId"];
        if (!string.IsNullOrEmpty(tenantId))
        {
            builder.WithEnvironment($"{sectionName}TenantId", tenantId);
        }

        if (parent.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _))
        {
            builder.WithEnvironment(
                $"{sectionName}Audience",
                ReferenceExpression.Create($"api://{parent.ClientId}"));
        }

        for (var i = 0; i < parent.KeyCredentials.Count; i++)
        {
            var cert = parent.KeyCredentials[i];
            var storePath = cert.StoreLocation == StoreLocation.CurrentUser
                ? "CurrentUser/My"
                : "LocalMachine/My";
            builder.WithEnvironment($"{sectionName}ClientCredentials__{i}__SourceType", "StoreWithThumbprint");
            builder.WithEnvironment($"{sectionName}ClientCredentials__{i}__CertificateStorePath", storePath);
            builder.WithEnvironment($"{sectionName}ClientCredentials__{i}__CertificateThumbprint", cert.ThumbprintParameter);
        }

        var parentBuilder = builder.ApplicationBuilder.CreateResourceBuilder(parent);
        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent)))
        {
            builder.WaitFor(parentBuilder);
        }

        foreach (var cert in parent.KeyCredentials)
        {
            if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, cert)))
            {
                builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(cert));
            }
        }

        return builder;
    }

    /// <summary>
    /// Registers a Graph SPA platform on the app registration. The parent emits
    /// <c>spa.redirectUris</c> in its Bicep module (create path).
    /// </summary>
    public static IResourceBuilder<AzureEntraIdSpaApplicationResource> AddSpaApplication(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var spa = new AzureEntraIdSpaApplicationResource(name, builder.Resource);
        builder.Resource.SpaApplications.Add(spa);
        return builder.ApplicationBuilder.AddResource(spa);
    }

    /// <summary>
    /// Adds a SPA platform redirect URI (create path). Call multiple times to accumulate URIs.
    /// The parent app registration merges this URI into Graph <c>spa.redirectUris</c>.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdSpaApplicationResource> WithRedirectUri(
        this IResourceBuilder<AzureEntraIdSpaApplicationResource> builder,
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.RedirectUris.Add(uri);
        var parent = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);
        return SyncParentSpaRedirectUris(parent, builder);
    }

    private static IResourceBuilder<AzureEntraIdSpaApplicationResource> SyncParentSpaRedirectUris(
        IResourceBuilder<AzureEntraIdAppRegistrationResource> parent,
        IResourceBuilder<AzureEntraIdSpaApplicationResource> spa)
    {
        parent.WithParameter(
            AzureEntraIdAppRegistrationResource.SpaRedirectUrisParameter,
            parent.Resource.CollectSpaRedirectUris());
        return spa;
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
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
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
        if (!string.IsNullOrEmpty(tenantId))
        {
            builder.WithEnvironment($"{sectionName}TenantId", tenantId);
        }

        if (parent.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _))
        {
            builder.WithEnvironment(
                $"{sectionName}Audience",
                ReferenceExpression.Create($"api://{parent.ClientId}"));
        }

        var scope = parent.RequiredPermissions.Find(permission => permission.Type == AzureEntraIdPermissionType.Scope);
        if (scope is AzureEntraIdScopeResource scopeResource)
        {
            builder.WithEnvironment(
                $"{sectionName}Scope",
                ReferenceExpression.Create($"api://{scopeResource.Parent.ClientId}/{scopeResource.Value}"));
        }

        var parentBuilder = builder.ApplicationBuilder.CreateResourceBuilder(parent);
        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent)))
        {
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
        where TAzureEntraIdPermissibleResource : IAzureEntraIdPermissibleResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        var resource = permission.Resource;
        if (builder.Resource.RequiredPermissions.Exists(existing => existing.Id == resource.Id))
        {
            throw new ArgumentException(
                $"A required permission with id '{resource.Id:D}' is already defined on '{builder.Resource.Name}'.",
                nameof(permission));
        }

        builder.Resource.RequiredPermissions.Add(resource);

        var parent = resource.Parent;
        if (ReferenceEquals(parent, builder.Resource))
        {
            return builder;
        }

        var parameterName = AzureEntraIdAppRegistrationResource.ResourceAppIdParameterName(parent.Name);
        if (!builder.Resource.Parameters.ContainsKey(parameterName))
        {
            builder.WithParameter(parameterName, parent.ClientId);
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, parent)))
        {
            builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(parent));
        }

        return builder;
    }

    /// <summary>
    /// Adds a check-only resource that verifies an X.509 certificate exists in
    /// <c>StoreName.My</c> at <paramref name="storeLocation"/>.
    /// The thumbprint is read from <paramref name="thumbprint"/> at initialize time.
    /// </summary>
    public static IResourceBuilder<AsymmetricX509CertResource> AddCertificate(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource> thumbprint,
        StoreLocation storeLocation)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(thumbprint);

        var resource = new AsymmetricX509CertResource(name, thumbprint.Resource, storeLocation);
        return builder.AddResource(resource)
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AsymmetricX509Cert",
                State = KnownResourceStates.NotStarted,
                Properties = []
            })
            .OnInitializeResource(async (cert, evt, ct) =>
            {
                var resolved = await cert.TryResolveNormalizedThumbprintAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(resolved))
                {
                    evt.Logger.LogError(
                        "Certificate thumbprint parameter {ParameterName} is empty.",
                        cert.ThumbprintParameter.Name);
                    await evt.Notifications.PublishUpdateAsync(cert, snapshot => snapshot with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error)
                    }).ConfigureAwait(false);
                    return;
                }

                if (AsymmetricX509CertResource.TryFind(resolved, cert.StoreLocation, out var found))
                {
                    found?.Dispose();
                    evt.Logger.LogInformation(
                        "Certificate {Thumbprint} found in StoreName.My at {StoreLocation}.",
                        resolved,
                        cert.StoreLocation);
                    await evt.Notifications.PublishUpdateAsync(cert, snapshot => snapshot with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success)
                    }).ConfigureAwait(false);
                    return;
                }

                evt.Logger.LogError(
                    "Certificate {Thumbprint} was not found in StoreName.My at {StoreLocation}.",
                    resolved,
                    cert.StoreLocation);
                await evt.Notifications.PublishUpdateAsync(cert, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error)
                }).ConfigureAwait(false);
            });
    }

    /// <summary>
    /// Adds a Graph <c>keyCredentials</c> entry from a store certificate (create path)
    /// and <c>WaitFor</c>s the certificate resource.
    /// </summary>
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> WithKeyCredential(
        this IResourceBuilder<AzureEntraIdAppRegistrationResource> builder,
        IResourceBuilder<AsymmetricX509CertResource> certificate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(certificate);

        var cert = certificate.Resource;
        if (builder.Resource.KeyCredentials.Exists(existing =>
            ReferenceEquals(existing, cert)
            || ReferenceEquals(existing.ThumbprintParameter, cert.ThumbprintParameter)))
        {
            throw new ArgumentException(
                $"A key credential for certificate '{cert.Name}' is already defined on '{builder.Resource.Name}'.",
                nameof(certificate));
        }

        builder.Resource.KeyCredentials.Add(cert);

        var parameterName = AsymmetricX509CertResource.KeyCredentialParameterName(cert.Name);
        if (!builder.Resource.Parameters.ContainsKey(parameterName))
        {
            builder.WithParameter(parameterName, () => (object)cert.ExportPublicKeyBase64());
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, cert)))
        {
            builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(cert));
        }

        return builder;
    }
}

internal sealed class AzureEntraIdRedirectUrisAnnotation : IResourceAnnotation
{
    public List<string> RedirectUris { get; } = [];
}

internal sealed class AzureEntraIdIdentifierUrisAnnotation : IResourceAnnotation
{
    public List<string> IdentifierUris { get; } = [];
}

internal sealed class AzureEntraIdDefaultIdentifierUriAnnotation : IResourceAnnotation
{
}

internal sealed class AzureEntraIdSupportedAccountTypeAnnotation : IResourceAnnotation
{
    public SupportedAccountType SupportedAccountType { get; set; } = SupportedAccountType.AzureADMyOrg;
}
