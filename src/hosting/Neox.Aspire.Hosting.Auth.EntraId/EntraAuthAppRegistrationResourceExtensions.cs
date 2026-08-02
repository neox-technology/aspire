using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific fluent configuration for <see cref="EntraAuthAppRegistrationResource"/>.
/// </summary>
public static class EntraAuthAppRegistrationResourceExtensions
{
    /// <summary>
    /// Adds a localhost redirect URI into the Entra Graph platform bucket
    /// (<see cref="AuthApplicationType"/>).
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithLocalhostRedirectUri(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        AuthApplicationType redirectUriType,
        int? port = null,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535 when specified.");
        }

        var uri = port is null
            ? "https://localhost"
            : $"https://localhost:{port.Value}";

        var normalizedPath = AuthAppRegistrationResourceExtensions.NormalizePath(path);
        if (normalizedPath is not null)
        {
            uri += normalizedPath;
        }

        return WithRedirectUri(builder, redirectUriType, uri);
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
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiPermission<TApiExposition>(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        IResourceBuilder<TApiExposition> exposition)
        where TApiExposition : ApiExposition
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(exposition);

        builder.WithAnnotation(new ApiPermissionAnnotation(exposition.Resource));
        return builder;
    }

    /// <summary>
    /// Consumes a first-party well-known API permission (e.g. <c>MicrosoftGraph.Delegated.UserRead</c>)
    /// as Graph <c>requiredResourceAccess</c>.
    /// </summary>
    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithApiPermission(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder,
        WellKnownApiPermission permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        builder.WithAnnotation(new WellKnownApiPermissionAnnotation(permission));
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
