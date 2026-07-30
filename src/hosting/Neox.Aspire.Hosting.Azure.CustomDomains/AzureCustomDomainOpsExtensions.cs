#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.AppContainers;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Extension methods that register Azure Container Apps custom domain pipeline steps.
/// </summary>
public static class AzureCustomDomainOpsExtensions
{
    public const string DomainPrereqStepName = "prereq-domain";

    /// <summary>
    /// Builds the provider OctoDNS config plan step name (<c>plan-domain-{providerSlug}</c>).
    /// </summary>
    public static string GetDomainPlanProviderStepName(string providerSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSlug);
        return $"plan-domain-{providerSlug}";
    }

    /// <summary>
    /// Builds the zone plan step name (<c>plan-domain-{zoneSlug}</c>).
    /// </summary>
    public static string GetDomainPlanZoneStepName(string zoneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneName);
        return $"plan-domain-{ToZoneSlug(zoneName)}";
    }

    /// <summary>
    /// Builds the zone provision step name (<c>provision-domain-{zoneSlug}</c>).
    /// </summary>
    public static string GetDomainProvisionZoneStepName(string zoneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneName);
        return $"provision-domain-{ToZoneSlug(zoneName)}";
    }

    /// <summary>
    /// Builds the env certificate inventory step name (<c>plan-{env}-certificates</c>).
    /// </summary>
    public static string GetPlanEnvCertificatesStepName(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        return $"plan-{environmentName}-certificates";
    }

    /// <summary>
    /// Builds the env certificate provision step name (<c>provision-{env}-certificates</c>).
    /// </summary>
    public static string GetProvisionEnvCertificatesStepName(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        return $"provision-{environmentName}-certificates";
    }

    /// <summary>
    /// Builds the per-compute domain plan step name (<c>plan-{resource}-domain</c>).
    /// </summary>
    public static string GetDomainPlanResourceStepName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"plan-{resourceName}-domain";
    }

    /// <summary>
    /// Builds the per-compute DomainOps bind step name (<c>provision-{resource}-domain</c>).
    /// </summary>
    public static string GetDomainProvisionStepName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"provision-{resourceName}-domain";
    }

    /// <summary>
    /// Builds the provider-specific DomainOps prereq step name (<c>prereq-domain-{providerSlug}</c>).
    /// </summary>
    public static string GetDomainPrereqProviderStepName(string providerSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSlug);
        return $"prereq-domain-{providerSlug}";
    }

    /// <summary>
    /// Builds the Aspire Container App Bicep provision step name (<c>provision-{resource}-containerapp</c>).
    /// </summary>
    public static string GetContainerAppProvisionStepName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"provision-{resourceName}-containerapp";
    }

    /// <summary>
    /// Sanitizes a DNS zone name for use in pipeline step names (e.g. <c>contoso.com</c> → <c>contoso-com</c>).
    /// </summary>
    public static string ToZoneSlug(string zoneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneName);
        return zoneName.Trim().TrimEnd('.').ToLowerInvariant().Replace('.', '-');
    }

    /// <summary>
    /// Registers custom domain ops (plan / provision) for the resource via <c>aspire do</c> pipeline steps.
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

        var zoneName = ResolveZoneName(builder.ApplicationBuilder, customDomain.Resource, options);
        var acaEnv = RequireAcaEnvironment(builder.ApplicationBuilder);
        var envName = options.ContainerAppEnvironmentName ?? acaEnv.Name;

        builder.WithAnnotation(new AzureCustomDomainOpsAnnotation(customDomain, certificateName, provider.Resource, options));

        var domainOps = EnsureDomainOpsResource(builder.ApplicationBuilder);
        RegisterSharedSteps(
            domainOps,
            builder.ApplicationBuilder,
            builder.Resource,
            customDomain.Resource,
            certificateName.Resource,
            provider.Resource,
            options,
            zoneName,
            envName);

        // Steps that depend on out-of-model containerapp provision or late resource plans.
        var targetResource = builder.Resource;
        var planZoneStepName = GetDomainPlanZoneStepName(zoneName);
        var zoneProvisionStepName = GetDomainProvisionZoneStepName(zoneName);
        var bindStepName = GetDomainProvisionStepName(targetResource.Name);
        var planResourceStepName = GetDomainPlanResourceStepName(targetResource.Name);
        var envProvisionStepName = GetProvisionEnvCertificatesStepName(envName);

        domainOps.WithPipelineConfiguration(context =>
        {
            var deploymentTarget = targetResource.GetDeploymentTargetAnnotation()?.DeploymentTarget;
            if (deploymentTarget is not null)
            {
                var infraSteps = context.GetSteps(deploymentTarget, WellKnownPipelineTags.ProvisionInfrastructure);

                context.GetSteps(domainOps.Resource)
                    .Where(s => string.Equals(s.Name, planZoneStepName, StringComparison.Ordinal))
                    .DependsOn(infraSteps);

                context.GetSteps(domainOps.Resource)
                    .Where(s => string.Equals(s.Name, bindStepName, StringComparison.Ordinal))
                    .DependsOn(infraSteps);
            }

            context.GetSteps(domainOps.Resource)
                .Where(s => string.Equals(s.Name, envProvisionStepName, StringComparison.Ordinal))
                .DependsOn(
                    context.GetSteps(domainOps.Resource)
                        .Where(s =>
                            string.Equals(s.Name, planResourceStepName, StringComparison.Ordinal)
                            || string.Equals(s.Name, zoneProvisionStepName, StringComparison.Ordinal)));
        });

        return builder;
    }

    internal static IResourceBuilder<AzureCustomDomainOpsResource> EnsureDomainOpsResource(
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

    internal static string ResolveZoneName(
        IDistributedApplicationBuilder applicationBuilder,
        ParameterResource customDomain,
        AzureCustomDomainOpsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DnsZoneName))
        {
            return DnsRecordPlanner.NormalizeHostname(options.DnsZoneName);
        }

        var hostname = TryPeekHostname(applicationBuilder, customDomain);
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new InvalidOperationException(
                $"Cannot resolve DNS zone for DomainOps. Set AzureCustomDomainOpsOptions.DnsZoneName " +
                $"or provide a default for parameter '{customDomain.Name}' (e.g. AddParameter(\"{customDomain.Name}\", \"www.example.com\")).");
        }

        return DnsRecordPlanner.GetZoneName(hostname);
    }

    internal static string? TryPeekHostname(
        IDistributedApplicationBuilder applicationBuilder,
        ParameterResource customDomain)
    {
        var fromConfig = applicationBuilder.Configuration[$"Parameters:{customDomain.Name}"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        fromConfig = applicationBuilder.Configuration[$"Parameters__{customDomain.Name}"];
        return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig;
    }

    private static AzureContainerAppEnvironmentResource RequireAcaEnvironment(
        IDistributedApplicationBuilder applicationBuilder)
        => applicationBuilder.Resources
               .OfType<AzureContainerAppEnvironmentResource>()
               .FirstOrDefault()
           ?? throw new InvalidOperationException(
               "DomainOps requires an Azure Container Apps environment. " +
               "Call AddAzureContainerAppEnvironment(...) on the AppHost before WithAzureCustomDomainOps.");

    private static void RegisterSharedSteps(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IDistributedApplicationBuilder applicationBuilder,
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string zoneName,
        string environmentName)
    {
        EnsurePlanProviderStep(domainOps, applicationBuilder, provider, options);
        EnsureZoneSteps(domainOps, applicationBuilder, provider, options, zoneName);
        EnsureEnvCertificateSteps(domainOps, applicationBuilder, environmentName);
        EnsureResourceSteps(
            domainOps,
            targetResource,
            customDomain,
            certificateName,
            provider,
            options,
            zoneName,
            environmentName);
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
                    targets.CustomDomainVerificationId));

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

    private static void EnsureResourceSteps(
        IResourceBuilder<AzureCustomDomainOpsResource> domainOps,
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string zoneName,
        string environmentName)
    {
        var planStepName = GetDomainPlanResourceStepName(targetResource.Name);
        var bindStepName = GetDomainProvisionStepName(targetResource.Name);
        var zoneProvisionStepName = GetDomainProvisionZoneStepName(zoneName);
        var envProvisionStepName = GetProvisionEnvCertificatesStepName(environmentName);

        if (domainOps.Resource.Annotations.OfType<DomainOpsNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planStepName, StringComparison.Ordinal)))
        {
            return;
        }

        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(planStepName));
        domainOps.WithAnnotation(new DomainOpsNamedStepAnnotation(bindStepName));

        var capturedOptions = options;

        domainOps.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planStepName,
            Description = $"Prepare domain binding model for '{targetResource.Name}' (no ARM).",
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
            Name = bindStepName,
            Description = $"Bind managed certificate to custom domain on '{targetResource.Name}'.",
            Tags = ["domain-ops"],
            Resource = domainOps.Resource,
            DependsOnSteps = [envProvisionStepName, planStepName],
            Action = async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(bindStepName);
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

    private sealed class DomainOpsNamedStepAnnotation(string stepName) : IResourceAnnotation
    {
        public string StepName { get; } = stepName;
    }

    private sealed record DomainBindingAnnotation(
        IResource TargetResource,
        ParameterResource CustomDomain,
        ParameterResource CertificateName,
        AzureCustomDomainOpsOptions Options);
}
