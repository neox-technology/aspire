using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Stores the DomainOps binding for a compute resource (hostname params, provider, options).
/// Added by <see cref="AzureCustomDomainOpsExtensions.WithAzureCustomDomainOps{T,TProvider}"/>.
/// </summary>
public sealed class AzureCustomDomainOpsAnnotation : IResourceAnnotation
{
    public AzureCustomDomainOpsAnnotation(
        IResourceBuilder<ParameterResource> customDomain,
        IResourceBuilder<ParameterResource> certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        CustomDomain = customDomain;
        CertificateName = certificateName;
        Provider = provider;
        Options = options;
    }

    /// <summary>
    /// Aspire parameter for the custom hostname (apex or subdomain).
    /// </summary>
    public IResourceBuilder<ParameterResource> CustomDomain { get; }

    /// <summary>
    /// Aspire parameter for the managed certificate name used by Bicep <c>ConfigureCustomDomain</c> and DomainOps bind.
    /// </summary>
    public IResourceBuilder<ParameterResource> CertificateName { get; }

    /// <summary>
    /// DNS provider resource registered with <c>AddDomainOpsProvider</c>.
    /// </summary>
    public DomainOpsProviderResource Provider { get; }

    /// <summary>
    /// Per-binding DomainOps options (zone name, OctoDNS paths, TTL, etc.).
    /// </summary>
    public AzureCustomDomainOpsOptions Options { get; }
}
