using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific fluent configuration for <see cref="AuthAppResource"/>.
/// </summary>
public static class EntraAuthAppResourceExtensions
{
    /// <summary>
    /// Sets the desired Entra supported account types (Graph <c>signInAudience</c>).
    /// When omitted, AuthOps defaults to <see cref="SupportedAccountsType.SingleTenant"/>.
    /// </summary>
    public static IResourceBuilder<AuthAppResource> WithSupportedAccounts(
        this IResourceBuilder<AuthAppResource> builder,
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
    public static IResourceBuilder<AuthAppResource> WithApiExposition(
        this IResourceBuilder<AuthAppResource> builder,
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
    public static IResourceBuilder<AuthAppResource> WithApiExposition(
        this IResourceBuilder<AuthAppResource> builder,
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
        this IResourceBuilder<AuthAppResource> builder,
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
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AuthAppRole",
                State = KnownResourceStates.Running,
                Properties = []
            });
    }

    /// <summary>
    /// Consumes a previously exposed scope or app role (Graph <c>requiredResourceAccess</c>).
    /// </summary>
    public static IResourceBuilder<AuthAppResource> WithApiPermission<TApiExposition>(
        this IResourceBuilder<AuthAppResource> builder,
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
    public static IResourceBuilder<AuthAppResource> WithApiPermission(
        this IResourceBuilder<AuthAppResource> builder,
        WellKnownApiPermission permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);

        builder.WithAnnotation(new WellKnownApiPermissionAnnotation(permission));
        return builder;
    }
}
