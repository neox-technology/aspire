#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure;

public static partial class AzureCustomDomainOpsExtensions
{
    private static void EnsureResourceSteps(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string zoneName,
        string environmentName,
        string domainSlug)
    {
        var planStepName = GetDomainPlanResourceStepName(targetResource.Name, domainSlug);
        var provisionStepName = GetDomainProvisionStepName(targetResource.Name, domainSlug);
        var deployStepName = GetDomainDeployStepName(targetResource.Name, domainSlug);
        var zoneProvisionStepName = GetDomainProvisionZoneStepName(zoneName);
        var envProvisionStepName = GetProvisionEnvCertificatesStepName(environmentName);

        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planStepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(planStepName));
        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(provisionStepName));
        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(deployStepName));

        var capturedOptions = options;

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planStepName,
            Description = $"Prepare domain binding model for '{targetResource.Name}' / '{domainSlug}' (no ARM).",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [zoneProvisionStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(planStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                await DomainOpsParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        DomainOpsParameterPrompt.CollectRequired(
                            DomainOpsActionKind.PlanResourceDomain,
                            customDomain,
                            certificateName,
                            provider,
                            capturedOptions),
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var hostname = await customDomain.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    throw new InvalidOperationException("Custom domain parameter is empty. Set Parameters__customDomain.");
                }

                var certParam = await certificateName.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                orchestrator.PlanResourceDomain(targetResource, hostname, capturedOptions, certParam);
            }
        });

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = provisionStepName,
            Description = $"Add custom hostname '{domainSlug}' to '{targetResource.Name}' without certificate.",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [planStepName, zoneProvisionStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(provisionStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                await DomainOpsParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        DomainOpsParameterPrompt.CollectRequired(
                            DomainOpsActionKind.ProvisionResourceDomain,
                            customDomain,
                            certificateName,
                            provider,
                            capturedOptions),
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var hostname = await customDomain.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    throw new InvalidOperationException("Custom domain parameter is empty. Set Parameters__customDomain.");
                }

                var certParam = await certificateName.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                var plan = orchestrator.PlanResourceDomain(targetResource, hostname, capturedOptions, certParam);
                await orchestrator.ProvisionResourceDomainAsync(
                        targetResource,
                        plan,
                        capturedOptions,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = deployStepName,
            Description = $"Bind managed certificate to '{domainSlug}' on '{targetResource.Name}'.",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [envProvisionStepName],
            RequiredBySteps = [DeployDomainsStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(deployStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                await DomainOpsParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        DomainOpsParameterPrompt.CollectRequired(
                            DomainOpsActionKind.BindResourceDomain,
                            customDomain,
                            certificateName,
                            provider,
                            capturedOptions),
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var hostname = await customDomain.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    throw new InvalidOperationException("Custom domain parameter is empty. Set Parameters__customDomain.");
                }

                var certParam = await certificateName.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                var plan = orchestrator.PlanResourceDomain(targetResource, hostname, capturedOptions, certParam);
                await orchestrator.BindResourceDomainAsync(
                        targetResource,
                        plan,
                        capturedOptions,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });
    }
}
