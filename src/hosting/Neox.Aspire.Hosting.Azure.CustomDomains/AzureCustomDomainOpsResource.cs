using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Hidden AppHost resource that owns shared DomainOps pipeline steps (<c>aspire do</c>).
/// </summary>
public sealed class AzureCustomDomainOpsResource : Resource
{
    /// <summary>
    /// Default Aspire resource name when DomainOps is first registered (<c>domain-ops</c>).
    /// </summary>
    public const string DefaultResourceName = "domain-ops";

    /// <summary>
    /// Creates the DomainOps marker resource.
    /// </summary>
    public AzureCustomDomainOpsResource(string name) : base(name)
    {
    }
}
