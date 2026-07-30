#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.AppContainers;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Extension methods that register OctoDNS DomainOps provider resources.
/// Fluent provider selectors are source-generated from <c>octodns-providers.json</c>.
/// </summary>
public static partial class DomainOpsProviderExtensions
{
    /// <summary>
    /// Starts configuring a DomainOps DNS provider resource (select a generated provider next).
    /// </summary>
    public static IDomainOpsProviderBuilder AddDomainOpsProvider(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DomainOpsProviderBuilder(builder, name);
    }

    private static void AddDomainOpsProviderCore(IDistributedApplicationBuilder applicationBuilder)
    {
        var domainOps = AzureCustomDomainOpsExtensions.EnsureDomainOpsResource(applicationBuilder);
        if (domainOps.Resource.HasAnnotationOfType<DomainOpsPrereqDomainStepAnnotation>())
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsPrereqDomainStepAnnotation());
        domainOps.WithPipelineStepFactory(factoryContext => CreatePrereqDomainStep(factoryContext, applicationBuilder));
    }

    private static PipelineStep CreatePrereqDomainStep(
        PipelineStepFactoryContext factoryContext,
        IDistributedApplicationBuilder applicationBuilder)
    {
        var acaEnv = applicationBuilder.Resources
            .OfType<AzureContainerAppEnvironmentResource>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "DomainOps requires an Azure Container Apps environment. " +
                "Call AddAzureContainerAppEnvironment(...) on the AppHost before running pipeline steps.");

        return new PipelineStep
        {
            Name = AzureCustomDomainOpsExtensions.DomainPrereqStepName,
            Description = "Shared DomainOps prerequisite gate after the ACA environment is provisioned.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            DependsOnSteps = [$"provision-{acaEnv.Name}"],
            Action = _ => Task.CompletedTask
        };
    }

    private static PipelineStep CreatePrereqProviderStep(
        PipelineStepFactoryContext factoryContext,
        DomainOpsProviderResource provider)
    {
        var stepName = AzureCustomDomainOpsExtensions.GetDomainPrereqProviderStepName(provider.ProviderSlug);
        var image = provider.DefaultDockerImage;

        return new PipelineStep
        {
            Name = stepName,
            Description = $"Pull OctoDNS Docker image '{image}' for DomainOps.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            DependsOnSteps = [AzureCustomDomainOpsExtensions.DomainPrereqStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(stepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                await PullOctoDnsImageAsync(runner, logger, image, context.CancellationToken).ConfigureAwait(false);
            }
        };
    }

    internal static async Task PullOctoDnsImageAsync(
        IProcessRunner runner,
        ILogger logger,
        string image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(image);

        logger.LogInformation("Pulling OctoDNS image {Image}.", image);
        var result = await runner.RunAsync(
                "docker",
                ["pull", image],
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to pull Docker image '{image}' (exit {result.ExitCode}): {result.StandardError}");
        }
    }

    private sealed partial class DomainOpsProviderBuilder(
        IDistributedApplicationBuilder applicationBuilder,
        string name) : IDomainOpsProviderBuilder
    {
        private void BindSecret(
            DomainOpsProviderResource resource,
            string yamlPropertyName,
            IResourceBuilder<ParameterResource>? existing,
            bool secret)
        {
            if (existing is not null)
            {
                resource.BindAuthParameter(yamlPropertyName, existing.Resource);
                return;
            }

            var parameterName = resource.GetParameterName(yamlPropertyName);
            var parameter = applicationBuilder.AddParameter(parameterName, secret: secret);
            resource.BindAuthParameter(yamlPropertyName, parameter.Resource);
        }

        private IResourceBuilder<T> AddProviderResource<T>(T resource)
            where T : DomainOpsProviderResource
        {
            AddDomainOpsProviderCore(applicationBuilder);

            var slugAlreadyRegistered = applicationBuilder.Resources
                .SelectMany(r => r.Annotations.OfType<DomainOpsPrereqProviderStepAnnotation>())
                .Any(a => string.Equals(a.Slug, resource.ProviderSlug, StringComparison.Ordinal));

            var builder = applicationBuilder.AddResource(resource)
                .ExcludeFromManifest()
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "DomainOpsProvider",
                    State = KnownResourceStates.Running,
                    Properties = []
                });

            if (!slugAlreadyRegistered)
            {
                builder.WithAnnotation(new DomainOpsPrereqProviderStepAnnotation(resource.ProviderSlug));
                builder.WithPipelineStepFactory(factoryContext => CreatePrereqProviderStep(factoryContext, resource));
            }

            return builder;
        }
    }

    private sealed class DomainOpsPrereqDomainStepAnnotation : IResourceAnnotation;

    private sealed class DomainOpsPrereqProviderStepAnnotation(string slug) : IResourceAnnotation
    {
        public string Slug { get; } = slug;
    }
}
