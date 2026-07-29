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
        AzureCustomDomainOpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);

        CustomDomain = customDomain;
        CertificateName = certificateName;
        Options = options;
    }

    public IResourceBuilder<ParameterResource> CustomDomain { get; }

    public IResourceBuilder<ParameterResource> CertificateName { get; }

    public AzureCustomDomainOpsOptions Options { get; }
}
