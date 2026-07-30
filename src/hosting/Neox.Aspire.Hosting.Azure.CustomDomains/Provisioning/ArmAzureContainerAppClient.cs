using Aspire.Hosting.Azure;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// ARM-backed <see cref="IAzureContainerAppClient"/> using Aspire's <see cref="ITokenCredentialProvider"/> when available.
/// </summary>
public sealed class ArmAzureContainerAppClient : IAzureContainerAppClient
{
    private readonly ArmClient _armClient;
    private readonly string _subscriptionId;
    private readonly string? _defaultResourceGroup;

    public ArmAzureContainerAppClient(
        TokenCredential credential,
        string subscriptionId,
        string? defaultResourceGroup = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        _subscriptionId = subscriptionId;
        _defaultResourceGroup = defaultResourceGroup;
        _armClient = new ArmClient(credential, subscriptionId);
    }

    /// <summary>
    /// Creates a client from DI (<see cref="ITokenCredentialProvider"/>) and Aspire Azure configuration
    /// (<c>Azure:SubscriptionId</c> / <c>Azure:ResourceGroup</c>), typically after <c>create-provisioning-context</c>.
    /// </summary>
    public static ArmAzureContainerAppClient Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        var credential = services.GetService<ITokenCredentialProvider>()?.TokenCredential
            ?? new DefaultAzureCredential();

        var subscriptionId = FirstNonEmpty(
            configuration?["Azure:SubscriptionId"],
            Environment.GetEnvironmentVariable("Azure__SubscriptionId"),
            Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"))
            ?? throw new InvalidOperationException(
                "Subscription id is required (Azure:SubscriptionId / Azure__SubscriptionId).");

        var resourceGroup = FirstNonEmpty(
            configuration?["Azure:ResourceGroup"],
            Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
            Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP"));

        return new ArmAzureContainerAppClient(credential, subscriptionId, resourceGroup);
    }

    public async Task<AzureContainerAppTargets> GetTargetsAsync(
        string containerAppName,
        string? resourceGroup,
        string? environmentName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerAppName);

        resourceGroup = FirstNonEmpty(
            resourceGroup,
            _defaultResourceGroup,
            Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
            Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP"))
            ?? throw new InvalidOperationException(
                "Resource group is required (Azure:ResourceGroup / Azure__ResourceGroup).");

        var app = await GetContainerAppAsync(resourceGroup, containerAppName, cancellationToken).ConfigureAwait(false);
        var data = app.Data;

        var fqdn = data.Configuration?.Ingress?.Fqdn
            ?? data.LatestRevisionFqdn
            ?? throw new InvalidOperationException("Container App ingress FQDN was not found.");

        var asuid = data.CustomDomainVerificationId
            ?? throw new InvalidOperationException("Container App customDomainVerificationId was not found.");

        var environmentId = data.EnvironmentId ?? data.ManagedEnvironmentId
            ?? throw new InvalidOperationException("Container App environment id was not found.");

        // Container App EnvironmentId is authoritative. Callers often pass the Aspire resource name
        // (e.g. "aca-env"), which is not the Azure managedEnvironments resource name.
        _ = environmentName;
        var resolvedEnvironmentName = environmentId.Name;
        if (string.IsNullOrWhiteSpace(resolvedEnvironmentName))
        {
            throw new InvalidOperationException("Container App environment name could not be resolved.");
        }

        var envRg = environmentId.ResourceGroupName ?? resourceGroup;
        var env = await GetManagedEnvironmentAsync(envRg, resolvedEnvironmentName, cancellationToken).ConfigureAwait(false);
        var staticIp = env.Data.StaticIP?.ToString()
            ?? throw new InvalidOperationException("Container Apps environment staticIp was not found.");

        return new AzureContainerAppTargets(
            containerAppName,
            resourceGroup,
            resolvedEnvironmentName,
            fqdn,
            staticIp,
            asuid);
    }

    public async Task<IReadOnlyList<AzureManagedCertificateInfo>> ListManagedCertificatesAsync(
        AzureContainerAppTargets targets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var env = await GetManagedEnvironmentAsync(targets.ResourceGroup, targets.EnvironmentName, cancellationToken)
            .ConfigureAwait(false);
        var results = new List<AzureManagedCertificateInfo>();

        await foreach (var cert in env.GetContainerAppManagedCertificates().GetAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            var id = cert.Id?.ToString()
                ?? throw new InvalidOperationException($"Managed certificate '{cert.Data.Name}' has no resource id.");
            results.Add(new AzureManagedCertificateInfo(
                cert.Data.Name,
                cert.Data.Properties?.SubjectName,
                id));
        }

        return results;
    }

    public async Task<AzureManagedCertificateInfo> CreateManagedCertificateAsync(
        AzureContainerAppTargets targets,
        string hostname,
        string certificateName,
        string validationMethod,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationMethod);

        var env = await GetManagedEnvironmentAsync(targets.ResourceGroup, targets.EnvironmentName, cancellationToken)
            .ConfigureAwait(false);

        var domainControl = ResolveDomainControlValidation(validationMethod);
        var certCollection = env.GetContainerAppManagedCertificates();

        var certData = new ContainerAppManagedCertificateData(env.Data.Location)
        {
            Properties = new ManagedCertificateProperties
            {
                SubjectName = hostname,
                DomainControlValidation = domainControl
            }
        };

        var certOperation = await certCollection
            .CreateOrUpdateAsync(WaitUntil.Completed, certificateName, certData, cancellationToken)
            .ConfigureAwait(false);
        var certificateId = certOperation.Value.Id
            ?? throw new InvalidOperationException($"Managed certificate '{certificateName}' was created without a resource id.");

        return new AzureManagedCertificateInfo(
            certOperation.Value.Data.Name,
            certOperation.Value.Data.Properties?.SubjectName ?? hostname,
            certificateId.ToString());
    }

    public async Task<bool> EnsureHostnameAsync(
        AzureContainerAppTargets targets,
        string hostname,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var app = await GetContainerAppAsync(targets.ResourceGroup, targets.ContainerAppName, cancellationToken)
            .ConfigureAwait(false);

        if (app.Data.Configuration?.Ingress?.CustomDomains is { } existing
            && existing.Any(d => string.Equals(d.Name, hostname, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var domains = new List<ContainerAppCustomDomain>();
        if (app.Data.Configuration?.Ingress?.CustomDomains is { } current)
        {
            domains.AddRange(current);
        }

        domains.Add(new ContainerAppCustomDomain(hostname)
        {
            BindingType = ContainerAppCustomDomainBindingType.Disabled
        });

        await PatchCustomDomainsAsync(app, domains, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task BindHostnameAsync(
        AzureContainerAppTargets targets,
        string hostname,
        string certificateId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);

        var app = await GetContainerAppAsync(targets.ResourceGroup, targets.ContainerAppName, cancellationToken)
            .ConfigureAwait(false);

        var certificateResourceId = new ResourceIdentifier(certificateId);

        var domains = new List<ContainerAppCustomDomain>();
        if (app.Data.Configuration?.Ingress?.CustomDomains is { } existing)
        {
            foreach (var domain in existing)
            {
                if (!string.Equals(domain.Name, hostname, StringComparison.OrdinalIgnoreCase))
                {
                    domains.Add(domain);
                }
            }
        }

        domains.Add(new ContainerAppCustomDomain(hostname, certificateResourceId)
        {
            BindingType = ContainerAppCustomDomainBindingType.SniEnabled
        });

        await PatchCustomDomainsAsync(app, domains, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PatchCustomDomainsAsync(
        ContainerAppResource app,
        IReadOnlyList<ContainerAppCustomDomain> domains,
        CancellationToken cancellationToken)
    {
        var patch = new ContainerAppData(app.Data.Location)
        {
            Configuration = new ContainerAppConfiguration
            {
                Ingress = new ContainerAppIngressConfiguration
                {
                    External = app.Data.Configuration?.Ingress?.External ?? true,
                    TargetPort = app.Data.Configuration?.Ingress?.TargetPort,
                    Transport = app.Data.Configuration?.Ingress?.Transport,
                }
            },
            EnvironmentId = app.Data.EnvironmentId ?? app.Data.ManagedEnvironmentId,
            Template = app.Data.Template
        };

        foreach (var domain in domains)
        {
            patch.Configuration.Ingress.CustomDomains.Add(domain);
        }

        if (app.Data.Configuration?.Ingress is { } ingress)
        {
            patch.Configuration.Ingress.AllowInsecure = ingress.AllowInsecure;
            patch.Configuration.Ingress.ClientCertificateMode = ingress.ClientCertificateMode;
            if (ingress.Traffic is { } traffic)
            {
                foreach (var weight in traffic)
                {
                    patch.Configuration.Ingress.Traffic.Add(weight);
                }
            }
        }

        if (app.Data.Configuration?.Secrets is { } secrets)
        {
            foreach (var secret in secrets)
            {
                patch.Configuration.Secrets.Add(secret);
            }
        }

        await app.UpdateAsync(WaitUntil.Completed, patch, cancellationToken).ConfigureAwait(false);
    }

    internal static ManagedCertificateDomainControlValidation ResolveDomainControlValidation(string validationMethod)
    {
        if (string.Equals(validationMethod, "HTTP", StringComparison.OrdinalIgnoreCase))
        {
            return ManagedCertificateDomainControlValidation.Http;
        }

        if (string.Equals(validationMethod, "CNAME", StringComparison.OrdinalIgnoreCase))
        {
            return ManagedCertificateDomainControlValidation.Cname;
        }

        throw new ArgumentException(
            $"Unsupported validation method '{validationMethod}'. Expected HTTP or CNAME.",
            nameof(validationMethod));
    }

    private async Task<ContainerAppResource> GetContainerAppAsync(
        string resourceGroup,
        string containerAppName,
        CancellationToken cancellationToken)
    {
        var subscription = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(_subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken).ConfigureAwait(false);
        var response = await rg.Value.GetContainerAppAsync(containerAppName, cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    private async Task<ContainerAppManagedEnvironmentResource> GetManagedEnvironmentAsync(
        string resourceGroup,
        string environmentName,
        CancellationToken cancellationToken)
    {
        var subscription = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(_subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken).ConfigureAwait(false);
        var response = await rg.Value.GetContainerAppManagedEnvironmentAsync(environmentName, cancellationToken)
            .ConfigureAwait(false);
        return response.Value;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
