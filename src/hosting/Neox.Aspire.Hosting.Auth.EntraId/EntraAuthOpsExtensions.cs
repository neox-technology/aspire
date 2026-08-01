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
                    context.CancellationToken).ConfigureAwait(false);
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
        var planName = AuthOpsExtensions.GetPlanAuthStepName(app.Name);
        var provisionName = AuthOpsExtensions.GetProvisionAuthStepName(app.Name);

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
            DependsOnSteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
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
                else if (app.Options.CreateClientSecret || app.Options.RotateClientSecret)
                {
                    await AuthParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        [app.ClientSecretParameter],
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });

        EnsureDeployAuthGate(provider);
    }

    private static IEnumerable<ParameterResource> CollectProvisionInputs(AuthAppResource app)
    {
        yield return app.TenantIdParameter;
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
}
