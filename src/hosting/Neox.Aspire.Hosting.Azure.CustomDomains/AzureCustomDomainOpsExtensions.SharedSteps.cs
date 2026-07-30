#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure;

public static partial class AzureCustomDomainOpsExtensions
{
    private static void EnsureEnvDomainsGate(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        string environmentName)
    {
        var stepName = GetProvisionEnvDomainsStepName(environmentName);
        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, stepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(stepName));
        domainOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = stepName,
            Description = $"Gate: all resource hostnames registered for ACA environment '{environmentName}'.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            Action = _ => Task.CompletedTask
        });
    }

    private static void EnsureDeployDomainsGate(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps)
    {
        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, DeployDomainsStepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(DeployDomainsStepName));
        domainOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = DeployDomainsStepName,
            Description = "Gate: all resource domain certificate binds complete.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = _ => Task.CompletedTask
        });
    }

    private static void EnsurePlanProviderStep(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IDistributedApplicationBuilder applicationBuilder,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options)
    {
        var stepName = GetDomainPlanProviderStepName(provider.ProviderSlug);
        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, stepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(stepName));
        var capturedProvider = provider;
        var capturedOptions = options;
        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = stepName,
            Description = $"Write OctoDNS config (octodns.yaml) for provider '{provider.ProviderSlug}'.",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [GetDomainPrereqProviderStepName(provider.ProviderSlug)],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(stepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var zones = await CollectZoneNamesForProviderAsync(
                        applicationBuilder,
                        capturedProvider,
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                orchestrator.PlanProvider(capturedProvider, zones, capturedOptions);
            }
        });
    }

    private static async Task<IReadOnlyList<string>> CollectZoneNamesForProviderAsync(
        IDistributedApplicationBuilder applicationBuilder,
        DomainOpsProviderResource provider,
        CancellationToken cancellationToken)
    {
        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (resource, annotation) in EnumerateBindings(applicationBuilder, provider))
        {
            _ = resource;
            var hostname = await annotation.CustomDomain.Resource.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(hostname))
            {
                if (!string.IsNullOrWhiteSpace(annotation.Options.DnsZoneName))
                {
                    zones.Add(DnsRecordPlanner.NormalizeHostname(annotation.Options.DnsZoneName));
                }

                continue;
            }

            zones.Add(DnsRecordPlanner.GetZoneName(hostname));
        }

        if (zones.Count == 0)
        {
            throw new InvalidOperationException(
                $"No DNS zones found for provider '{provider.ProviderSlug}'. Set custom domain parameters or DnsZoneName.");
        }

        return zones.OrderBy(z => z, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<(IResource Resource, AzureCustomDomainOpsAnnotation Annotation)> EnumerateBindings(
        IDistributedApplicationBuilder applicationBuilder,
        DomainOpsProviderResource provider)
    {
        foreach (var resource in applicationBuilder.Resources)
        {
            foreach (var annotation in resource.Annotations.OfType<AzureCustomDomainOpsAnnotation>())
            {
                if (ReferenceEquals(annotation.Provider, provider)
                    || string.Equals(annotation.Provider.Name, provider.Name, StringComparison.Ordinal))
                {
                    yield return (resource, annotation);
                }
            }
        }
    }
}
