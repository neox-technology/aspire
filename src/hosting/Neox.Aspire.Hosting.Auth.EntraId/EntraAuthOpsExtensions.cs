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
    /// <summary>Entra-specific prerequisite (<c>prereq-auth-entra</c>).</summary>
    public const string AuthPrereqEntraStepName = "prereq-auth-entra";

    /// <summary>Deploy gate required by Aspire <c>deploy</c> (<c>deploy-auth</c>).</summary>
    public const string AuthDeployStepName = "deploy-auth";

    internal static void EnsurePrereqEntraStep(IResourceBuilder<EntraAuthProviderResource> provider)
    {
        if (provider.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthPrereqEntraStepName, StringComparison.Ordinal)))
        {
            return;
        }

        provider.WithAnnotation(new AuthNamedStepAnnotation(AuthPrereqEntraStepName));
        provider.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthPrereqEntraStepName,
            Description = "AuthOps Entra prerequisite: tenant parameter and Graph credential path.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
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

    internal static void EnsureDeployAuthGate(IResourceBuilder<EntraAuthProviderResource> provider)
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
        IResourceBuilder<EntraAuthProviderResource> provider,
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
                else if (app.Options.CreateClientSecret || app.Options.RotateClientSecret)
                {
                    // Confidential client expected a secret but Graph did not return one — prompt / CI.
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
}
