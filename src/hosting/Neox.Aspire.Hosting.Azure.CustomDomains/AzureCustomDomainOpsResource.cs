using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Marker resource that owns custom domain pipeline steps for the AppHost.
/// </summary>
public sealed class AzureCustomDomainOpsResource : Resource
{
    public const string DefaultResourceName = "domain-ops";

    public AzureCustomDomainOpsResource(string name) : base(name)
    {
    }
}
