using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Annotation carrying custom domain ops configuration for a compute resource.
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

    public IResourceBuilder<ParameterResource> CustomDomain { get; }

    public IResourceBuilder<ParameterResource> CertificateName { get; }

    public DomainOpsProviderResource Provider { get; }

    public AzureCustomDomainOpsOptions Options { get; }
}
