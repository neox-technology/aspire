#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Google-specific AuthOps pipeline registration.
/// </summary>
public static class GoogleAuthOpsExtensions
{
    /// <summary>Deploy gate required by Aspire <c>deploy</c> (<c>deploy-auth</c>).</summary>
    public const string AuthDeployStepName = AuthOpsExtensions.AuthDeployStepName;

    /// <summary>
    /// Builds <c>prereq-{providerResource}-auth</c> for a Google provider resource name.
    /// </summary>
    public static string GetPrereqStepName(string providerResourceName) =>
        AuthOpsExtensions.GetPrereqProviderAuthStepName(providerResourceName);

    internal static void EnsurePrereqGoogleStep(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<GoogleAuthOpsResource> provider)
    {
        AuthOpsExtensions.EnsurePrereqProvidersAuthGate(applicationBuilder);

        var stepName = GetPrereqStepName(provider.Resource.Name);
        if (provider.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, stepName, StringComparison.Ordinal)))
        {
            return;
        }

        provider.WithAnnotation(new AuthNamedStepAnnotation(stepName));
        provider.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = stepName,
            Description = "AuthOps Google prerequisite: project id parameter and ADC credential path.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                var google = provider.Resource;
                await GoogleProjectParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    google.ProjectIdParameter,
                    google.Name,
                    context.CancellationToken).ConfigureAwait(false);

                var projectId = await google.ProjectIdParameter
                    .GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        google.ProjectIdParameter,
                        projectId!,
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });
    }

    internal static void RegisterAppPipelineSteps(
        IResourceBuilder<GoogleAuthOpsResource> provider,
        IResourceBuilder<GoogleAuthAppRegistrationResource> appBuilder)
    {
        var app = appBuilder.Resource;
        var prereqName = AuthOpsExtensions.GetPrereqAppAuthStepName(app.Name);
        var planName = AuthOpsExtensions.GetPlanAuthStepName(app.Name);
        var provisionName = AuthOpsExtensions.GetProvisionAuthStepName(app.Name);

        if (app.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planName, StringComparison.Ordinal)))
        {
            return;
        }

        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(prereqName));
        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(planName));
        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(provisionName));

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = prereqName,
            Description =
                $"AuthOps prerequisite: bind ClientId for app '{app.Name}' (existing client or Parameters__*; create is out of scope).",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                await GoogleOauthClientParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    app.ClientIdParameter,
                    app.TenantIdParameter,
                    app.Name,
                    app.DisplayName,
                    provider.Resource.Name,
                    context.CancellationToken).ConfigureAwait(false);

                var clientId = await app.ClientIdParameter
                    .GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(clientId)
                    || GoogleOauthClientParameterPrompt.IsCreateSentinel(clientId))
                {
                    throw new InvalidOperationException(
                        "Google AuthOps does not create oauth clients. Set ClientId via the Choice prompt " +
                        $"(existing client or custom paste) or Parameters__{app.ClientIdParameter.Name} " +
                        $"(Auth app '{app.Name}').");
                }

                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.ClientIdParameter,
                    clientId!,
                    context.CancellationToken).ConfigureAwait(false);
            }
        });

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planName,
            Description = $"Plan AuthOps Google ClientId bind for '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [prereqName],
            Action = async context =>
            {
                var provisioner = ResolveProvisioner(context.Services);
                var plan = await provisioner.PlanAsync(app, context.CancellationToken).ConfigureAwait(false);
                appBuilder.WithAnnotation(new GoogleOauthClientPlanAnnotation(plan));
            }
        });

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = provisionName,
            Description = $"Bind Google ProjectId + ClientId for '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [planName],
            RequiredBySteps = [AuthOpsExtensions.AuthDeployStepName],
            Action = async context =>
            {
                var provisioner = ResolveProvisioner(context.Services);

                var plan = app.Annotations.OfType<GoogleOauthClientPlanAnnotation>().LastOrDefault()?.Plan
                    ?? await provisioner.PlanAsync(app, context.CancellationToken).ConfigureAwait(false);

                await AuthParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    [app.TenantIdParameter],
                    context.CancellationToken).ConfigureAwait(false);

                var result = await provisioner.ProvisionAsync(app, plan, context.CancellationToken)
                    .ConfigureAwait(false);

                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.TenantIdParameter,
                    result.ProjectId,
                    context.CancellationToken).ConfigureAwait(false);
                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.ClientIdParameter,
                    result.ClientId,
                    context.CancellationToken).ConfigureAwait(false);
            }
        });

        AuthOpsExtensions.EnsureDeployAuthGate(provider.ApplicationBuilder);
    }

    /// <summary>
    /// Injects generic <c>AUTH_GOOGLE_*</c> environment variables from a Google Auth app registration.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<GoogleAuthAppRegistrationResource> authApp,
        Action<AuthEnvOptions>? configure = null)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authApp);
        return WithAuth(builder, authApp.Resource, configure);
    }

    /// <summary>
    /// Injects generic <c>AUTH_GOOGLE_*</c> environment variables from a Google Auth app registration.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        GoogleAuthAppRegistrationResource authApp,
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

        if (envOptions.IncludeClientSecret == true)
        {
            builder.WithEnvironment(
                envOptions.ResolveName(AuthOutput.ClientSecret, prefix),
                authApp.ClientSecretParameter);
        }

        if (envOptions.IncludeAuthority && authApp.Provider.AuthorityExpression is { } authority)
        {
            var authorityName = envOptions.ResolveName(AuthOutput.Authority, prefix);
            builder.WithEnvironment(authorityName, authority(authApp.TenantIdParameter));
        }

        return builder;
    }

    private static IGoogleIamOauthClientProvisioner ResolveProvisioner(IServiceProvider services) =>
        services.GetService(typeof(IGoogleIamOauthClientProvisioner)) as IGoogleIamOauthClientProvisioner
        ?? GoogleIamOauthClientProvisioner.Create(services);
}
