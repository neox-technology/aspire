#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.AppContainers;
using Aspire.Hosting.Pipelines;
using Neox.Aspire.Hosting.Azure.Dns;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Registers Azure Container Apps custom-domain pipeline steps for <c>aspire do</c>.
/// Binding surface uses <c>AzureCustomDomainOps*</c>; provider and step names use <c>DomainOps*</c>.
/// </summary>
public static partial class AzureCustomDomainOpsExtensions
{
    /// <summary>
    /// Shared DomainOps prerequisite gate (<c>prereq-domain</c>), required by each provider image pull.
    /// </summary>
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
    /// Builds the env domains gate step name (<c>provision-{env}-domains</c>).
    /// </summary>
    public static string GetProvisionEnvDomainsStepName(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        return $"provision-{environmentName}-domains";
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
    /// Builds the per-compute domain plan step name (<c>plan-{resource}-domain-{domslug}</c>).
    /// </summary>
    public static string GetDomainPlanResourceStepName(string resourceName, string domainSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainSlug);
        return $"plan-{resourceName}-domain-{domainSlug}";
    }

    /// <summary>
    /// Builds the per-compute hostname-add step name (<c>provision-{resource}-domain-{domslug}</c>).
    /// </summary>
    public static string GetDomainProvisionStepName(string resourceName, string domainSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainSlug);
        return $"provision-{resourceName}-domain-{domainSlug}";
    }

    /// <summary>
    /// Builds the per-compute DomainOps bind step name (<c>deploy-{resource}-domain-{domslug}</c>).
    /// </summary>
    public static string GetDomainDeployStepName(string resourceName, string domainSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainSlug);
        return $"deploy-{resourceName}-domain-{domainSlug}";
    }

    /// <summary>
    /// Shared gate that aggregates all <c>deploy-{resource}-domain-{domslug}</c> steps (<c>deploy-domains</c>).
    /// </summary>
    public const string DeployDomainsStepName = "deploy-domains";

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
    /// Registers DomainOps plan / provision / bind steps for this compute resource.
    /// Call after <c>AddDomainOpsProvider</c> and <c>ConfigureCustomDomain</c>; invoke with <c>aspire do</c>.
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
        var domainSlug = ResolveDomainSlug(builder.ApplicationBuilder, customDomain.Resource);
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
            envName,
            domainSlug);

        // Steps that depend on out-of-model containerapp provision or late resource plans.
        var targetResource = builder.Resource;
        var planZoneStepName = GetDomainPlanZoneStepName(zoneName);
        var provisionResourceStepName = GetDomainProvisionStepName(targetResource.Name, domainSlug);
        var deployStepName = GetDomainDeployStepName(targetResource.Name, domainSlug);
        var envDomainsStepName = GetProvisionEnvDomainsStepName(envName);
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
                    .Where(s =>
                        string.Equals(s.Name, provisionResourceStepName, StringComparison.Ordinal)
                        || string.Equals(s.Name, deployStepName, StringComparison.Ordinal))
                    .DependsOn(infraSteps);
            }

            context.GetSteps(domainOps.Resource)
                .Where(s => string.Equals(s.Name, envDomainsStepName, StringComparison.Ordinal))
                .DependsOn(
                    context.GetSteps(domainOps.Resource)
                        .Where(s => string.Equals(s.Name, provisionResourceStepName, StringComparison.Ordinal)));

            context.GetSteps(domainOps.Resource)
                .Where(s => string.Equals(s.Name, envProvisionStepName, StringComparison.Ordinal))
                .DependsOn(
                    context.GetSteps(domainOps.Resource)
                        .Where(s => string.Equals(s.Name, envDomainsStepName, StringComparison.Ordinal)));

            context.GetSteps(domainOps.Resource)
                .Where(s => string.Equals(s.Name, DeployDomainsStepName, StringComparison.Ordinal))
                .DependsOn(
                    context.GetSteps(domainOps.Resource)
                        .Where(s => string.Equals(s.Name, deployStepName, StringComparison.Ordinal)));

            // Serialize ARM-mutating per-hostname steps on the same compute resource
            // (parallel GET+PATCH races corrupt Container App LROs).
            SerializeSameResourceDomainArmSteps(context.GetSteps(domainOps.Resource), targetResource.Name);
        });

        return builder;
    }

    /// <summary>
    /// Registers DomainOps steps using <paramref name="customDomain"/> and a certificate parameter named
    /// <c>{domain.Name}-certificate</c> (GetOrAdd).
    /// </summary>
    public static IResourceBuilder<T> WithAzureCustomDomainOps<T, TProvider>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> customDomain,
        IResourceBuilder<TProvider> provider,
        Action<AzureCustomDomainOpsOptions>? configure = null)
        where T : IResource
        where TProvider : DomainOpsProviderResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(provider);

        var certificateName = EnsureAzureCustomDomainCertificateParameter(
            builder.ApplicationBuilder,
            customDomain);

        return builder.WithAzureCustomDomainOps(customDomain, certificateName, provider, configure);
    }

    /// <summary>
    /// Registers DomainOps steps with parameters named <c>{resource}-domain</c> /
    /// <c>{resource}-certificate</c> (GetOrAdd). <paramref name="hostname"/> is the domain parameter default.
    /// </summary>
    public static IResourceBuilder<T> WithAzureCustomDomainOps<T, TProvider>(
        this IResourceBuilder<T> builder,
        string hostname,
        IResourceBuilder<TProvider> provider,
        Action<AzureCustomDomainOpsOptions>? configure = null)
        where T : IResource
        where TProvider : DomainOpsProviderResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(provider);

        var (customDomain, certificateName) = EnsureAzureCustomDomainParameters(
            builder.ApplicationBuilder,
            builder.Resource.Name,
            hostname);

        return builder.WithAzureCustomDomainOps(customDomain, certificateName, provider, configure);
    }

    /// <summary>
    /// Chains <c>provision|deploy-{resource}-domain-*</c> steps for one compute resource in ordinal name order
    /// so concurrent Container App patches cannot race.
    /// </summary>
    internal static void SerializeSameResourceDomainArmSteps(IEnumerable<PipelineStep> steps, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        ChainSiblingStepsByPrefix(steps, $"provision-{resourceName}-domain-");
        ChainSiblingStepsByPrefix(steps, $"deploy-{resourceName}-domain-");
    }

    private static void ChainSiblingStepsByPrefix(IEnumerable<PipelineStep> steps, string prefix)
    {
        var ordered = steps
            .Where(s => s.Name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            ordered[i].DependsOn(ordered[i - 1]);
        }
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

    internal static string ResolveDomainSlug(
        IDistributedApplicationBuilder applicationBuilder,
        ParameterResource customDomain)
    {
        var hostname = TryPeekHostname(applicationBuilder, customDomain);
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new InvalidOperationException(
                $"Cannot resolve domain slug for DomainOps step names. Provide a default for parameter '{customDomain.Name}' " +
                $"(e.g. AddParameter(\"{customDomain.Name}\", \"www.example.com\")) or set Parameters:{customDomain.Name}.");
        }

        return ToZoneSlug(DnsRecordPlanner.NormalizeHostname(hostname));
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
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        // Prefer AddParameter constant defaults; skip generated/user-secrets defaults that fail at design time.
        if (customDomain.Default is not null)
        {
            try
            {
                var fromDefault = customDomain.Default.GetDefaultValue();
                if (!string.IsNullOrWhiteSpace(fromDefault))
                {
                    return fromDefault;
                }
            }
            catch
            {
                // Ignore non-constant defaults (e.g. generated secrets) at registration time.
            }
        }

        // AddParameter(name, value) may expose the constant via obsolete Value when Default is null
        // (e.g. under --publisher manifest).
#pragma warning disable CS0618
        try
        {
            var fromValue = customDomain.Value;
            if (!string.IsNullOrWhiteSpace(fromValue))
            {
                return fromValue;
            }
        }
        catch
        {
            // Value throws when the parameter has no default and is unresolved.
        }
#pragma warning restore CS0618

        return null;
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
        string environmentName,
        string domainSlug)
    {
        EnsurePlanProviderStep(domainOps, applicationBuilder, provider, options);
        EnsureZoneSteps(domainOps, applicationBuilder, provider, options, zoneName);
        EnsureEnvDomainsGate(domainOps, environmentName);
        EnsureEnvCertificateSteps(domainOps, applicationBuilder, environmentName);
        EnsureDeployDomainsGate(domainOps);
        EnsureResourceSteps(
            domainOps,
            targetResource,
            customDomain,
            certificateName,
            provider,
            options,
            zoneName,
            environmentName,
            domainSlug);
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
