#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Extension methods that register Azure Container Apps custom domain pipeline steps.
/// </summary>
public static class AzureCustomDomainOpsExtensions
{
    public const string DomainVerifyStepName = "domain-verify";
    public const string DomainProvisionStepName = "domain-provision";
    public const string DomainGuardStepName = "domain-guard";

    /// <summary>
    /// Registers custom domain ops (verify / provision / guard) for the resource via <c>aspire do</c> pipeline steps.
    /// </summary>
    public static IResourceBuilder<T> WithAzureCustomDomainOps<T, TProvider>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> customDomain,
        IResourceBuilder<ParameterResource> certificateName,
        IResourceBuilder<TProvider> provider,
        Action<AzureCustomDomainOpsOptions>? configure = null)
        where T : IResource
        where TProvider : DomainOpsProviderResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(provider);

        var options = new AzureCustomDomainOpsOptions
        {
            ContainerAppResourceName = builder.Resource.Name
        };
        configure?.Invoke(options);

        builder.WithAnnotation(new AzureCustomDomainOpsAnnotation(customDomain, certificateName, provider.Resource, options));

        DomainOpsProviderCommandExtensions.EnsureProviderCommands(provider);

        EnsureDomainOpsResource(builder.ApplicationBuilder)
            .WithPipelineStepFactory(factoryContext =>
                CreateSteps(factoryContext, builder.Resource, customDomain, certificateName, provider.Resource, options));

        return builder;
    }

    private static IResourceBuilder<AzureCustomDomainOpsResource> EnsureDomainOpsResource(
        IDistributedApplicationBuilder applicationBuilder)
    {
        var existing = applicationBuilder.Resources
            .OfType<AzureCustomDomainOpsResource>()
            .FirstOrDefault();

        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        return applicationBuilder.AddResource(new AzureCustomDomainOpsResource(AzureCustomDomainOpsResource.DefaultResourceName))
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "DomainOps",
                State = KnownResourceStates.Running,
                Properties = []
            });
    }

    private static IEnumerable<PipelineStep> CreateSteps(
        PipelineStepFactoryContext factoryContext,
        IResource targetResource,
        IResourceBuilder<ParameterResource> customDomain,
        IResourceBuilder<ParameterResource> certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options)
    {
        yield return new PipelineStep
        {
            Name = DomainVerifyStepName,
            Description = "Verify DNS and certificate parameter consistency for ACA custom domains.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(DomainVerifyStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var orchestrator = new DomainOpsOrchestrator(runner, logger);
                await orchestrator.VerifyAsync(targetResource, customDomain.Resource, certificateName.Resource, options, context.CancellationToken)
                    .ConfigureAwait(false);
            }
        };

        yield return new PipelineStep
        {
            Name = DomainGuardStepName,
            Description = "Fail when a certificate name is required and empty (steady-state).",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(DomainGuardStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var orchestrator = new DomainOpsOrchestrator(runner, logger);
                await orchestrator.GuardAsync(certificateName.Resource, options, context.CancellationToken)
                    .ConfigureAwait(false);
            }
        };

        yield return new PipelineStep
        {
            Name = DomainProvisionStepName,
            Description = "Provision DNS via OctoDNS (Docker), bind ACA managed certificate, update GitHub variable.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(DomainProvisionStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var orchestrator = new DomainOpsOrchestrator(runner, logger);
                await orchestrator.ProvisionAsync(
                        targetResource,
                        customDomain.Resource,
                        certificateName.Resource,
                        provider,
                        options,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        };
    }
}
