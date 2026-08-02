#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// AuthOps shared helpers (plan/provision/prereq step names, gates). Provider packages own <c>WithAuth</c>.
/// </summary>
public static class AuthOpsExtensions
{
    /// <summary>
    /// Shared AuthOps prerequisite gate (<c>prereq-providers-auth</c>) — all providers authenticated.
    /// </summary>
    public const string AuthPrereqProvidersStepName = "prereq-providers-auth";

    /// <summary>
    /// Shared AuthOps deploy gate (<c>deploy-auth</c>) — all Auth app provision steps completed.
    /// Hosted on <see cref="AuthOpsResource"/> so multiple providers share one step name.
    /// </summary>
    public const string AuthDeployStepName = "deploy-auth";

    /// <summary>
    /// Builds <c>prereq-{providerResource}-auth</c> from the Aspire provider resource name.
    /// </summary>
    public static string GetPrereqProviderAuthStepName(string providerResourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);
        return $"prereq-{providerResourceName}-auth";
    }

    /// <summary>
    /// Builds <c>prereq-{app}-auth</c> from the Auth app resource name.
    /// </summary>
    public static string GetPrereqAppAuthStepName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"prereq-{appName}-auth";
    }

    /// <summary>
    /// Builds <c>plan-{app}-auth</c>.
    /// </summary>
    public static string GetPlanAuthStepName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"plan-{appName}-auth";
    }

    /// <summary>
    /// Builds <c>provision-{app}-auth</c>.
    /// </summary>
    public static string GetProvisionAuthStepName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        return $"provision-{appName}-auth";
    }

    internal static IResourceBuilder<AuthOpsResource> EnsureAuthOpsResource(
        IDistributedApplicationBuilder applicationBuilder)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);

        var existing = applicationBuilder.Resources
            .OfType<AuthOpsResource>()
            .FirstOrDefault();

        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        return applicationBuilder.AddResource(new AuthOpsResource(AuthOpsResource.DefaultResourceName))
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AuthOps",
                State = KnownResourceStates.Running,
                Properties = []
            });
    }

    internal static void EnsurePrereqProvidersAuthGate(IDistributedApplicationBuilder applicationBuilder)
    {
        var authOps = EnsureAuthOpsResource(applicationBuilder);
        if (authOps.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthPrereqProvidersStepName, StringComparison.Ordinal)))
        {
            return;
        }

        authOps.WithAnnotation(new AuthNamedStepAnnotation(AuthPrereqProvidersStepName));
        authOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthPrereqProvidersStepName,
            Description = "AuthOps gate — all identity providers authenticated.",
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
