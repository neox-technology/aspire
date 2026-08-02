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
    public const string AuthDeployStepName = "deploy-auth";

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

    internal static void EnsureDeployAuthGate(IResourceBuilder<EntraAuthOpsResource> provider)
    {
        if (provider.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthDeployStepName, StringComparison.Ordinal)))
        {
            return;
        }

        provider.WithAnnotation(new AuthNamedStepAnnotation(AuthDeployStepName));
        provider.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthDeployStepName,
            Description = "AuthOps deploy gate — all Auth app provision steps completed.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = _ => Task.CompletedTask
        });
    }

    internal static void RegisterAppPipelineSteps(
        IResourceBuilder<EntraAuthOpsResource> provider,
        IResourceBuilder<AuthAppResource> appBuilder)
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

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planName,
            Description = $"Plan AuthOps app registration changes for '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [prereqName],
            Action = async context =>
            {
                var provisioner = ResolveProvisioner(context.Services);
                var plan = await provisioner.PlanAsync(app, context.CancellationToken).ConfigureAwait(false);
                appBuilder.WithAnnotation(new AuthAppRegistrationPlanAnnotation(plan));
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
            }
        });

        EnsureDeployAuthGate(provider);
    }

    private static IEntraGraphAppProvisioner ResolveProvisioner(IServiceProvider services) =>
        services.GetService(typeof(IEntraGraphAppProvisioner)) as IEntraGraphAppProvisioner
        ?? EntraGraphAppProvisioner.Create(services);
}
