#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// AuthOps binding and pipeline registration (<c>WithAuth</c>, step names, shared gates).
/// </summary>
public static partial class AuthOpsExtensions
{
    /// <summary>Shared AuthOps prerequisite gate (<c>prereq-auth</c>).</summary>
    public const string AuthPrereqStepName = "prereq-auth";

    /// <summary>Entra-specific prerequisite (<c>prereq-auth-entra</c>).</summary>
    public const string AuthPrereqEntraStepName = "prereq-auth-entra";

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
        builder.WithEnvironment(envOptions.ResolveName(AuthOutput.ClientSecret, prefix), authApp.ClientSecretParameter);

        if (envOptions.IncludeAuthority)
        {
            var authorityName = envOptions.ResolveName(AuthOutput.Authority, prefix);
            builder.WithEnvironment(async context =>
            {
                var tenantId = await authApp.TenantIdParameter.GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    context.EnvironmentVariables[authorityName] =
                        $"https://login.microsoftonline.com/{tenantId}";
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

    internal static void EnsurePrereqEntraStep(IResourceBuilder<EntraAuthProviderResource> provider)
    {
        var authOps = EnsureAuthOpsResource(provider.ApplicationBuilder);
        if (authOps.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthPrereqEntraStepName, StringComparison.Ordinal)))
        {
            return;
        }

        authOps.WithAnnotation(new AuthNamedStepAnnotation(AuthPrereqEntraStepName));
        authOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthPrereqEntraStepName,
            Description = "AuthOps Entra prerequisite: tenant parameter and Graph credential path.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            DependsOnSteps = [AuthPrereqStepName],
            Action = async context =>
            {
                var entra = provider.Resource;
                if (entra.TenantIdParameter is not null)
                {
                    await AuthParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        [entra.TenantIdParameter],
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });
    }

    internal static void RegisterAppPipelineSteps(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<AuthAppResource> appBuilder)
    {
        var app = appBuilder.Resource;
        var planName = GetPlanAuthStepName(app.Name);
        var provisionName = GetProvisionAuthStepName(app.Name);

        if (app.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planName, StringComparison.Ordinal)))
        {
            return;
        }

        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(planName));
        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(provisionName));

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planName,
            Description = $"Validate AuthOps desired model for app '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [AuthPrereqEntraStepName],
            Action = context =>
            {
                ValidateAppOptions(app);
                return Task.CompletedTask;
            }
        });

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = provisionName,
            Description = $"Provision or adopt Entra app registration '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [planName],
            RequiredBySteps = [AuthDeployStepName],
            Action = async context =>
            {
                var provisioner = context.Services.GetService(typeof(IEntraGraphAppProvisioner)) as IEntraGraphAppProvisioner
                    ?? EntraGraphAppProvisioner.Create(context.Services);

                await AuthParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    CollectProvisionInputs(app),
                    context.CancellationToken).ConfigureAwait(false);

                var result = await provisioner.ProvisionAsync(app, context.CancellationToken).ConfigureAwait(false);

                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.TenantIdParameter,
                    result.TenantId,
                    context.CancellationToken).ConfigureAwait(false);
                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.ClientIdParameter,
                    result.ClientId,
                    context.CancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(result.ClientSecret))
                {
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        app.ClientSecretParameter,
                        result.ClientSecret,
                        context.CancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Adopt without rotation: secret must already be supplied.
                    await AuthParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        [app.ClientSecretParameter],
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });

        EnsureDeployAuthGate(applicationBuilder);
    }

    private static void EnsureDeployAuthGate(IDistributedApplicationBuilder applicationBuilder)
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

    private static IEnumerable<ParameterResource> CollectProvisionInputs(AuthAppResource app)
    {
        yield return app.TenantIdParameter;
        if (!string.IsNullOrWhiteSpace(app.Options.ExistingClientId))
        {
            // Client id known; secret required when not rotating/creating.
            if (!app.Options.CreateClientSecret && !app.Options.RotateClientSecret)
            {
                // Still may need secret from CI — prompted after provision if Graph did not return one.
            }
        }
    }

    private static void ValidateAppOptions(AuthAppResource app)
    {
        var o = app.Options;
        if (string.IsNullOrWhiteSpace(o.DisplayName))
        {
            throw new InvalidOperationException($"Auth app '{app.Name}' requires DisplayName.");
        }

        if (o.ApplicationType is AuthApplicationType.Web or AuthApplicationType.Spa or AuthApplicationType.Native
            && o.RedirectUris.Count == 0
            && string.IsNullOrWhiteSpace(o.ExistingClientId))
        {
            throw new InvalidOperationException(
                $"Auth app '{app.Name}' ({o.ApplicationType}) requires at least one RedirectUri when creating.");
        }
    }

    private sealed class AuthNamedStepAnnotation(string stepName) : IResourceAnnotation
    {
        public string StepName { get; } = stepName;
    }
}
