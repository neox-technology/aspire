#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific AuthOps pipeline registration and step names.
/// </summary>
public static class EntraAuthOpsExtensions
{
    /// <summary>Deploy gate required by Aspire <c>deploy</c> (<c>deploy-auth</c>).</summary>
    public const string AuthDeployStepName = AuthOpsExtensions.AuthDeployStepName;

    /// <summary>
    /// Builds <c>prereq-{providerResource}-auth</c> for an Entra provider resource name.
    /// </summary>
    public static string GetPrereqStepName(string providerResourceName) =>
        AuthOpsExtensions.GetPrereqProviderAuthStepName(providerResourceName);

    internal static void EnsurePrereqEntraStep(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<EntraAuthOpsResource> provider)
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
            Description = "AuthOps Entra prerequisite: tenant parameter and Graph credential path.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                var entra = provider.Resource;
                await EntraTenantParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    entra.TenantIdParameter,
                    entra.Name,
                    context.CancellationToken).ConfigureAwait(false);

                var tenantId = await entra.TenantIdParameter
                    .GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        entra.TenantIdParameter,
                        tenantId!,
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });
    }

    internal static void RegisterAppPipelineSteps(
        IResourceBuilder<EntraAuthOpsResource> provider,
        IResourceBuilder<EntraAuthAppRegistrationResource> appBuilder)
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
                $"AuthOps prerequisite: ClientId for app '{app.Name}' (adopt GUID or create with DisplayName '{app.DisplayName}').",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                await EntraAppRegistrationParameterPrompt.EnsureReadyAsync(
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
                if (!EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
                {
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        app.ClientIdParameter,
                        clientId!,
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });

        appBuilder.WithPipelineStepFactory(_ =>
        {
            var planDependsOn = new List<string> { prereqName };
            foreach (var permission in app.Annotations.OfType<ApiPermissionAnnotation>())
            {
                var exposer = permission.Exposition.Owner;
                if (!ReferenceEquals(exposer, app))
                {
                    var exposerPlan = AuthOpsExtensions.GetPlanAuthStepName(exposer.Name);
                    if (!planDependsOn.Contains(exposerPlan, StringComparer.Ordinal))
                    {
                        planDependsOn.Add(exposerPlan);
                    }
                }
            }

            return new PipelineStep
            {
                Name = planName,
                Description = $"Plan AuthOps app registration changes for '{app.Name}'.",
                Tags = ["auth-ops"],
                Resource = app,
                DependsOnSteps = planDependsOn,
                Action = async context =>
                {
                    var provisioner = ResolveProvisioner(context.Services);
                    var plan = await provisioner.PlanAsync(app, context.CancellationToken).ConfigureAwait(false);
                    appBuilder.WithAnnotation(new AuthAppRegistrationPlanAnnotation(plan));
                }
            };
        });

        appBuilder.WithPipelineStepFactory(_ =>
        {
            var dependsOn = new List<string> { planName };
            foreach (var permission in app.Annotations.OfType<ApiPermissionAnnotation>())
            {
                var exposer = permission.Exposition.Owner;
                if (!ReferenceEquals(exposer, app))
                {
                    var exposerProvision = AuthOpsExtensions.GetProvisionAuthStepName(exposer.Name);
                    if (!dependsOn.Contains(exposerProvision, StringComparer.Ordinal))
                    {
                        dependsOn.Add(exposerProvision);
                    }
                }
            }

            return new PipelineStep
            {
                Name = provisionName,
                Description = $"Provision or adopt Entra app registration '{app.Name}'.",
                Tags = ["auth-ops"],
                Resource = app,
                DependsOnSteps = dependsOn,
                RequiredBySteps = [AuthOpsExtensions.AuthDeployStepName],
                Action = async context =>
                {
                    var provisioner = ResolveProvisioner(context.Services);

                    var plan = app.Annotations.OfType<AuthAppRegistrationPlanAnnotation>().LastOrDefault()?.Plan
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
                        result.TenantId,
                        context.CancellationToken).ConfigureAwait(false);
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        app.ClientIdParameter,
                        result.ClientId,
                        context.CancellationToken).ConfigureAwait(false);

                    // WithClientSecret: bind provided secret value in memory only (no Graph addPassword).
                    if (app.Annotations.OfType<ClientSecretAnnotation>().Any())
                    {
                        await AuthParameterPrompt.EnsureReadyAsync(
                                context.Services,
                                [app.ClientSecretParameter],
                                context.CancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            };
        });

        AuthOpsExtensions.EnsureDeployAuthGate(provider.ApplicationBuilder);
    }

    /// <summary>
    /// Injects Microsoft.Identity.Web <c>AzureAd__*</c> environment variables from an Entra app registration.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<EntraAuthAppRegistrationResource> authApp,
        Action<EntraAuthEnvOptions>? configure = null)
        where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authApp);
        return WithAuth(builder, authApp.Resource, configure);
    }

    /// <summary>
    /// Injects Microsoft.Identity.Web <c>AzureAd__*</c> environment variables from an Entra app registration.
    /// </summary>
    public static IResourceBuilder<T> WithAuth<T>(
        this IResourceBuilder<T> builder,
        EntraAuthAppRegistrationResource authApp,
        Action<EntraAuthEnvOptions>? configure = null)
        where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authApp);

        var envOptions = new EntraAuthEnvOptions();
        configure?.Invoke(envOptions);

        if (envOptions.IncludeInstance)
        {
            builder.WithEnvironment(envOptions.ResolveName(AuthOutput.Instance), envOptions.Instance);
        }

        builder.WithEnvironment(envOptions.ResolveName(AuthOutput.TenantId), authApp.TenantIdParameter);
        builder.WithEnvironment(envOptions.ResolveName(AuthOutput.ClientId), authApp.ClientIdParameter);

        var emitSecret = envOptions.IncludeClientSecret == true
            || (envOptions.IncludeClientSecret != false
                && authApp.Annotations.OfType<ClientSecretAnnotation>().Any());
        if (emitSecret)
        {
            builder.WithEnvironment(
                envOptions.ResolveName(AuthOutput.ClientSecret),
                authApp.ClientSecretParameter);
        }

        AuthOpsExtensions.EnsureWaitFor(builder, authApp);

        return builder;
    }

    private static IEntraGraphAppProvisioner ResolveProvisioner(IServiceProvider services) =>
        services.GetService(typeof(IEntraGraphAppProvisioner)) as IEntraGraphAppProvisioner
        ?? EntraGraphAppProvisioner.Create(services);
}
