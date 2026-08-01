#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// AuthOps binding and shared pipeline registration (<c>WithAuth</c>, step names, shared gates).
/// </summary>
public static class AuthOpsExtensions
{
    /// <summary>Shared AuthOps prerequisite gate (<c>prereq-auth</c>).</summary>
    public const string AuthPrereqStepName = "prereq-auth";

    /// <summary>Deploy gate required by Aspire <c>deploy</c> (<c>deploy-auth</c>).</summary>
    public const string AuthDeployStepName = "deploy-auth";

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

    internal static IResourceBuilder<AuthOpsResource> EnsureAuthOpsResource(
        IDistributedApplicationBuilder applicationBuilder)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);

        var existing = applicationBuilder.Resources.OfType<AuthOpsResource>().FirstOrDefault();
        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        return applicationBuilder.AddResource(new AuthOpsResource())
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AuthOps",
                State = KnownResourceStates.Running,
                Properties = []
            });
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

    internal static void EnsurePrereqAuthStep(IDistributedApplicationBuilder applicationBuilder)
    {
        var authOps = EnsureAuthOpsResource(applicationBuilder);
        if (authOps.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthPrereqStepName, StringComparison.Ordinal)))
        {
            return;
        }

        authOps.WithAnnotation(new AuthNamedStepAnnotation(AuthPrereqStepName));
        authOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthPrereqStepName,
            Description = "AuthOps prerequisite: management credentials available for identity providers.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            Action = _ => Task.CompletedTask
        });
    }

    internal static void EnsureDeployAuthGate(IDistributedApplicationBuilder applicationBuilder)
    {
        var authOps = EnsureAuthOpsResource(applicationBuilder);
        if (authOps.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthDeployStepName, StringComparison.Ordinal)))
        {
            return;
        }

        authOps.WithAnnotation(new AuthNamedStepAnnotation(AuthDeployStepName));
        authOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthDeployStepName,
            Description = "AuthOps deploy gate — all Auth app provision steps completed.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = _ => Task.CompletedTask
        });
    }
}
