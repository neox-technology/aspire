#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure;

public static partial class AzureCustomDomainOpsExtensions
{
    private static void EnsureZoneSteps(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IDistributedApplicationBuilder applicationBuilder,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string zoneName)
    {
        var planStepName = GetDomainPlanZoneStepName(zoneName);
        var provisionStepName = GetDomainProvisionZoneStepName(zoneName);

        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planStepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(planStepName));
        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(provisionStepName));

        var capturedProvider = provider;
        var capturedOptions = options;
        var capturedZone = zoneName;

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planStepName,
            Description = $"Write/upsert OctoDNS zone YAML for '{zoneName}'.",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [GetDomainPlanProviderStepName(provider.ProviderSlug)],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(planStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var bindings = await CollectZoneBindingsAsync(
                        applicationBuilder,
                        capturedProvider,
                        capturedZone,
                        context.CancellationToken)
                    .ConfigureAwait(false);

                foreach (var binding in bindings)
                {
                    await DomainOpsParameterPrompt.EnsureReadyAsync(
                            context.Services,
                            DomainOpsParameterPrompt.CollectRequired(
                                DomainOpsActionKind.PlanZone,
                                binding.CustomDomain,
                                binding.CertificateName,
                                capturedProvider,
                                capturedOptions),
                            context.CancellationToken)
                        .ConfigureAwait(false);
                }

                var inputs = new List<ZoneBindingInput>();
                foreach (var binding in bindings)
                {
                    var hostname = await binding.CustomDomain.GetValueAsync(context.CancellationToken)
                        .ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(hostname))
                    {
                        throw new InvalidOperationException(
                            $"Custom domain parameter '{binding.CustomDomain.Name}' is empty.");
                    }

                    inputs.Add(new ZoneBindingInput(
                        hostname,
                        binding.Options.ContainerAppResourceName ?? binding.TargetResource.Name));
                }

                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                await orchestrator.PlanZoneAsync(
                        capturedZone,
                        inputs,
                        capturedProvider,
                        capturedOptions,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = provisionStepName,
            Description = $"Apply OctoDNS sync for zone '{zoneName}' (dry-run + upsert-only guard internal).",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [planStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(provisionStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var bindings = await CollectZoneBindingsAsync(
                        applicationBuilder,
                        capturedProvider,
                        capturedZone,
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var first = bindings.FirstOrDefault()
                    ?? throw new InvalidOperationException($"No bindings for zone '{capturedZone}'.");

                await DomainOpsParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        DomainOpsParameterPrompt.CollectRequired(
                            DomainOpsActionKind.ProvisionZone,
                            first.CustomDomain,
                            first.CertificateName,
                            capturedProvider,
                            capturedOptions),
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var hostname = await first.CustomDomain.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    throw new InvalidOperationException(
                        $"Custom domain parameter '{first.CustomDomain.Name}' is empty.");
                }

                var appName = first.Options.ContainerAppResourceName ?? first.TargetResource.Name;
                var azure = ArmAzureContainerAppClient.Create(context.Services);
                var targets = await azure.GetTargetsAsync(
                        appName,
                        resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
                        environmentName: capturedOptions.ContainerAppEnvironmentName,
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var waitPlan = new DnsRecordPlanner().Plan(new DnsPlanInput(
                    hostname,
                    targets.Fqdn,
                    targets.StaticIp,
                    targets.CustomDomainVerificationId,
                    capturedOptions.Ttl ?? 0));

                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                await orchestrator.ProvisionZoneAsync(
                        capturedZone,
                        capturedProvider,
                        capturedOptions,
                        waitPlan,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });
    }

    private static async Task<IReadOnlyList<DomainBindingAnnotation>> CollectZoneBindingsAsync(
        IDistributedApplicationBuilder applicationBuilder,
        DomainOpsProviderResource provider,
        string zoneName,
        CancellationToken cancellationToken)
    {
        var results = new List<DomainBindingAnnotation>();
        foreach (var (resource, annotation) in EnumerateBindings(applicationBuilder, provider))
        {
            var hostname = await annotation.CustomDomain.Resource.GetValueAsync(cancellationToken).ConfigureAwait(false);
            string resolvedZone;
            if (!string.IsNullOrWhiteSpace(hostname))
            {
                resolvedZone = DnsRecordPlanner.GetZoneName(hostname);
            }
            else if (!string.IsNullOrWhiteSpace(annotation.Options.DnsZoneName))
            {
                resolvedZone = DnsRecordPlanner.NormalizeHostname(annotation.Options.DnsZoneName);
            }
            else
            {
                continue;
            }

            if (string.Equals(resolvedZone, zoneName, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new DomainBindingAnnotation(
                    resource,
                    annotation.CustomDomain.Resource,
                    annotation.CertificateName.Resource,
                    annotation.Options));
            }
        }

        return results;
    }
}
