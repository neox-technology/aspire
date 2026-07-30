#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure;

public static partial class AzureCustomDomainOpsExtensions
{
    private static void EnsureEnvCertificateSteps(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IDistributedApplicationBuilder applicationBuilder,
        string environmentName)
    {
        var planStepName = GetPlanEnvCertificatesStepName(environmentName);
        var provisionStepName = GetProvisionEnvCertificatesStepName(environmentName);

        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planStepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(planStepName));
        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(provisionStepName));

        var acaEnv = RequireAcaEnvironment(applicationBuilder);
        var capturedEnv = environmentName;

        domainOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = planStepName,
            Description = $"Inventory managed certificates on ACA environment '{environmentName}'.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            DependsOnSteps = [$"provision-{acaEnv.Name}"],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(planStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var sample = applicationBuilder.Resources
                    .SelectMany(r => r.Annotations.OfType<AzureCustomDomainOpsAnnotation>()
                        .Select(a => (Resource: r, Annotation: a)))
                    .FirstOrDefault();

                if (sample.Resource is null)
                {
                    throw new InvalidOperationException("No DomainOps bindings found for certificate inventory.");
                }

                var appName = sample.Annotation.Options.ContainerAppResourceName ?? sample.Resource.Name;
                var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                await orchestrator.PlanEnvCertificatesAsync(
                        capturedEnv,
                        appName,
                        sample.Annotation.Options,
                        context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });

        domainOps.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = provisionStepName,
            Description = $"Create missing managed certificates on ACA environment '{environmentName}'.",
            Tags = ["domain-ops"],
            Resource = factoryContext.Resource,
            DependsOnSteps = [planStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(provisionStepName);
                var runner = context.Services.GetService<IProcessRunner>() ?? new ProcessRunner();
                var bindings = applicationBuilder.Resources
                    .SelectMany(r => r.Annotations.OfType<AzureCustomDomainOpsAnnotation>()
                        .Select(a => (Resource: r, Annotation: a)))
                    .ToList();

                if (bindings.Count == 0)
                {
                    throw new InvalidOperationException("No DomainOps bindings found for certificate provision.");
                }

                var plans = new List<DomainBindingPlan>();
                foreach (var (resource, annotation) in bindings)
                {
                    await DomainOpsParameterPrompt.EnsureReadyAsync(
                            context.Services,
                            DomainOpsParameterPrompt.CollectRequired(
                                DomainOpsActionKind.ProvisionCertificates,
                                annotation.CustomDomain.Resource,
                                annotation.CertificateName.Resource,
                                annotation.Provider,
                                annotation.Options),
                            context.CancellationToken)
                        .ConfigureAwait(false);

                    var hostname = await annotation.CustomDomain.Resource.GetValueAsync(context.CancellationToken)
                        .ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(hostname))
                    {
                        throw new InvalidOperationException(
                            $"Custom domain parameter '{annotation.CustomDomain.Resource.Name}' is empty.");
                    }

                    var certParam = await annotation.CertificateName.Resource.GetValueAsync(context.CancellationToken)
                        .ConfigureAwait(false);
                    var orchestrator = new DomainOpsOrchestrator(runner, logger, context.Services);
                    plans.Add(orchestrator.PlanResourceDomain(resource, hostname, annotation.Options, certParam));
                }

                var first = bindings[0];
                var appName = first.Annotation.Options.ContainerAppResourceName ?? first.Resource.Name;
                var azure = ArmAzureContainerAppClient.Create(context.Services);
                var targets = await azure.GetTargetsAsync(
                        appName,
                        resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
                        environmentName: first.Annotation.Options.ContainerAppEnvironmentName ?? capturedEnv,
                        context.CancellationToken)
                    .ConfigureAwait(false);

                var existing = await azure.ListManagedCertificatesAsync(targets, context.CancellationToken)
                    .ConfigureAwait(false);

                var provisioner = new DomainProvisioner(
                    runner,
                    azure,
                    delayAsync: null);
                await provisioner.ProvisionEnvCertificatesAsync(targets, plans, existing, context.CancellationToken)
                    .ConfigureAwait(false);
            }
        });
    }
}
