using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// AuthOps binding and shared helpers (<c>WithAuth</c>, plan/provision step names, parameters).
/// </summary>
public static class AuthOpsExtensions
{
    /// <summary>
    /// Builds <c>plan-auth-{app}</c>.
    /// </summary>
    public static string GetPlanAuthStepName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"plan-auth-{appName}";
    }

    /// <summary>
    /// Builds <c>provision-auth-{app}</c>.
    /// </summary>
    public static string GetProvisionAuthStepName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"provision-auth-{appName}";
    }

    /// <summary>
    /// Injects generic <c>AUTH_*</c> environment variables from an Auth app's workload parameters.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<AuthAppResource> authApp,
        Action<AuthEnvOptions>? configure = null)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authApp);
        return WithAuth(builder, authApp.Resource, configure);
    }

    /// <summary>
    /// Injects generic <c>AUTH_*</c> environment variables from an Auth app's workload parameters.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        AuthAppResource authApp,
        Action<AuthEnvOptions>? configure = null)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authApp);

        var envOptions = new AuthEnvOptions();
        configure?.Invoke(envOptions);
        var prefix = string.IsNullOrWhiteSpace(envOptions.Prefix)
            ? authApp.DefaultEnvPrefix
            : envOptions.Prefix!;

        builder.WithEnvironment(envOptions.ResolveName(AuthOutput.TenantId, prefix), authApp.TenantIdParameter);
        builder.WithEnvironment(envOptions.ResolveName(AuthOutput.ClientId, prefix), authApp.ClientIdParameter);

        var includeClientSecret = envOptions.IncludeClientSecret ?? authApp.Options.CreateClientSecret;
        if (includeClientSecret)
        {
            builder.WithEnvironment(
                envOptions.ResolveName(AuthOutput.ClientSecret, prefix),
                authApp.ClientSecretParameter);
        }

        if (envOptions.IncludeAuthority && authApp.Provider.AuthorityFormatter is { } formatter)
        {
            var authorityName = envOptions.ResolveName(AuthOutput.Authority, prefix);
            builder.WithEnvironment(async context =>
            {
                var tenantId = await authApp.TenantIdParameter.GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    context.EnvironmentVariables[authorityName] = formatter(tenantId);
                }
            });
        }

        if (envOptions.IncludeRedirectUri && authApp.Options.RedirectUris.Count > 0)
        {
            builder.WithEnvironment(
                envOptions.ResolveName(AuthOutput.RedirectUri, prefix),
                authApp.Options.RedirectUris[0]);
        }

        return builder;
    }

    internal static IResourceBuilder<ParameterResource> GetOrAddParameter(
        IDistributedApplicationBuilder applicationBuilder,
        string parameterName,
        string? defaultValue,
        bool secret)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        var existing = applicationBuilder.Resources.OfType<ParameterResource>()
            .FirstOrDefault(p => string.Equals(p.Name, parameterName, StringComparison.Ordinal));
        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            return applicationBuilder.AddParameter(parameterName, defaultValue, secret: secret);
        }

        return applicationBuilder.AddParameter(parameterName, secret: secret);
    }
}
