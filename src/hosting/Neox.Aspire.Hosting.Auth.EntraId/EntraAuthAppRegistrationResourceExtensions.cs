using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific fluent configuration for <see cref="EntraAuthAppRegistrationResource"/>.
/// </summary>
public static class EntraAuthAppRegistrationResourceExtensions
{
    /// <summary>
    /// Opts into workload client-secret bind (<c>AzureAd__ClientSecret</c>) and AppHost UI create
    /// (Graph <c>addPassword</c>). Adds a child resource <c>{app}-clientsecret</c>.
    /// When <paramref name="param"/> is null, uses the auto <c>{provider}-{app}-client-secret</c> parameter;
    /// otherwise requires a secret parameter and overrides it.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithClientSecret(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        IResourceBuilder<ParameterResource>? param = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<ClientSecretAnnotation>().Any())
        {
            return builder;
        }

        IResourceBuilder<ParameterResource> secretParamBuilder;
        if (param is not null)
        {
            if (!param.Resource.Secret)
            {
                throw new ArgumentException(
                    $"Parameter '{param.Resource.Name}' must be created with secret: true for WithClientSecret.",
                    nameof(param));
            }

            builder.Resource.ClientSecretParameter = param.Resource;
            secretParamBuilder = param;
        }
        else
        {
            secretParamBuilder = builder.ApplicationBuilder.CreateResourceBuilder(
                builder.Resource.ClientSecretParameter);
        }

        var app = builder.Resource;
        var secretResourceName = $"{app.Name}-clientsecret";
        var secretResource = new EntraClientSecretResource(
            secretResourceName,
            app,
            secretParamBuilder.Resource);

        var secretResourceBuilder = builder.ApplicationBuilder.AddResource(secretResource)
            .ExcludeFromManifest()
            .WithParentRelationship(builder)
            .WithInitialState(AuthDashboardSnapshots.Waiting("EntraClientSecret"))
            .WithCreateClientSecretCommand();

        secretParamBuilder.WithParentRelationship(secretResourceBuilder);

        builder.WithAnnotation(
            new ClientSecretAnnotation(secretResource),
            ResourceAnnotationMutationBehavior.Replace);

        return builder;
    }

    /// <summary>
    /// Adds one or more localhost redirect URIs into the Entra Graph platform bucket
    /// (<see cref="AuthApplicationType"/>).
    /// </summary>
    /// <param name="builder">Entra Auth app registration builder.</param>
    /// <param name="redirectUriType">Graph platform bucket (Web / Spa / Native).</param>
    /// <param name="port">Optional localhost port (1–65535).</param>
    /// <param name="path">Optional path (leading <c>/</c> normalized).</param>
    /// <param name="scheme">URI scheme(s); defaults to <see cref="LocalhostRedirectScheme.Https"/>.</param>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithLocalhostRedirectUri(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AuthApplicationType redirectUriType,
        int? port = null,
        string? path = null,
        LocalhostRedirectScheme scheme = LocalhostRedirectScheme.Https)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var uri in AuthAppRegistrationResourceExtensions.BuildLocalhostUris(port, path, scheme))
        {
            WithRedirectUri(builder, redirectUriType, uri);
        }

        return builder;
    }

    /// <summary>
    /// Adds an absolute redirect URI into the Entra Graph platform bucket.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithRedirectUri(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AuthApplicationType redirectUriType,
        string uri)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Redirect URI must be an absolute http or https URI.",
                nameof(uri));
        }

        var entry = AuthRedirectUri.FromLiteral(uri);
        builder.Resource.AddRedirectUri(entry);
        AddEntraPlatform(builder, redirectUriType, entry);
        return builder;
    }

    /// <summary>
    /// Adds a parameter-based redirect URI into the Entra Graph platform bucket.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithRedirectUri(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AuthApplicationType redirectUriType,
        IResourceBuilder<ParameterResource> uri,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        var entry = AuthRedirectUri.FromParameter(uri.Resource, AuthAppRegistrationResourceExtensions.NormalizePath(path));
        builder.Resource.AddRedirectUri(entry);
        AddEntraPlatform(builder, redirectUriType, entry);
        return builder;
    }

    /// <summary>
    /// Sets the desired Entra supported account types (Graph <c>signInAudience</c>).
    /// When omitted, AuthOps defaults to <see cref="SupportedAccountsType.SingleTenant"/>.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithSupportedAccounts(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        SupportedAccountsType supportedAccounts)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithAnnotation(
            new SupportedAccountsAnnotation(supportedAccounts),
            ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>
    /// Exposes an API with Application ID URI <paramref name="url"/> and optional scopes.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiExposition(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        string url,
        Action<IApiExpositionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(configure);

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                "API Application ID URI must be an absolute URI (e.g. api://my-api).",
                nameof(url));
        }

        builder.WithAnnotation(new ApiIdentifierUriAnnotation(url, useClientIdTemplate: false));
        configure(new ApiExpositionBuilder(builder));
        return builder;
    }

    /// <summary>
    /// Exposes an API with Application ID URI <c>api://{ClientId}</c> (resolved after ClientId is known)
    /// and optional scopes.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiExposition(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        Action<IApiExpositionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.WithAnnotation(new ApiIdentifierUriAnnotation(literalUri: null, useClientIdTemplate: true));
        configure(new ApiExpositionBuilder(builder));
        return builder;
    }

    /// <summary>
    /// Exposes an Entra app role on this Auth app.
    /// </summary>
    public static IResourceBuilder<AppRoleApiExposition> WithAppRoleExposition(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AllowedMemberType allowedMemberType,
        string value,
        string description)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var app = builder.Resource;
        var resourceName = $"{app.Name}-approle-{ApiExpositionBuilder.Sanitize(value)}";
        var role = new AppRoleApiExposition(
            resourceName,
            app,
            allowedMemberType,
            value,
            description,
            Guid.NewGuid());

        builder.WithAnnotation(new ExposedApiAnnotation(role));

        return builder.ApplicationBuilder.AddResource(role)
            .ExcludeFromManifest()
            .WithParentRelationship(builder.Resource)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthAppRole"));
    }

    /// <summary>
    /// Consumes a previously exposed scope or app role (Graph <c>requiredResourceAccess</c>).
    /// Adds a dashboard child <c>{app}-apiperm-{value}</c>.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiPermission<TApiExposition>(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        IResourceBuilder<TApiExposition> exposition)
        where TApiExposition : ApiExposition
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(exposition);

        var app = builder.Resource;
        var value = exposition.Resource switch
        {
            ScopeApiExposition scope => scope.ScopeValue,
            AppRoleApiExposition role => role.Value,
            _ => exposition.Resource.Name
        };
        var resourceName = $"{app.Name}-apiperm-{ApiExpositionBuilder.Sanitize(value)}";
        var permissionResource = new ApiPermissionResource(resourceName, app, exposition.Resource);

        builder.WithAnnotation(new ApiPermissionAnnotation(permissionResource));
        builder.ApplicationBuilder.AddResource(permissionResource)
            .ExcludeFromManifest()
            .WithParentRelationship(builder.Resource)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthApiPermission"));

        AuthOpsExtensions.EnsureWaitFor(builder, exposition.Resource.Owner);

        return builder;
    }

    /// <summary>
    /// Consumes a first-party well-known API permission (e.g. <c>MicrosoftGraph.Delegated.UserRead</c>)
    /// as Graph <c>requiredResourceAccess</c>. Adds a dashboard child <c>{app}-apiperm-{value}</c>.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiPermission(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        WellKnownApiPermission permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        var app = builder.Resource;
        var resourceName = $"{app.Name}-apiperm-{ApiExpositionBuilder.Sanitize(permission.Value)}";
        var permissionResource = new ApiPermissionResource(resourceName, app, permission);

        builder.WithAnnotation(new WellKnownApiPermissionAnnotation(permissionResource));
        builder.ApplicationBuilder.AddResource(permissionResource)
            .ExcludeFromManifest()
            .WithParentRelationship(builder.Resource)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthApiPermission"));

        return builder;
    }

    private static void AddEntraPlatform(
        IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AuthApplicationType redirectUriType,
        AuthRedirectUri entry)
    {
        var annotation = builder.Resource.Annotations.OfType<EntraRedirectUrisAnnotation>().FirstOrDefault();
        if (annotation is null)
        {
            annotation = new EntraRedirectUrisAnnotation();
            builder.WithAnnotation(annotation);
        }

        annotation.Entries.Add(new EntraRedirectUriEntry
        {
            Type = redirectUriType,
            Uri = entry
        });
    }
}
