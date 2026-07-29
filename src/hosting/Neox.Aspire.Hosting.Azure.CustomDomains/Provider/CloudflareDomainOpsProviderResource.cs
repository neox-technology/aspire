namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// OctoDNS Cloudflare provider resource.
/// </summary>
public sealed class CloudflareDomainOpsProviderResource : DomainOpsProviderResource
{
    public CloudflareDomainOpsProviderResource(string name)
        : base(name)
    {
    }

    public override string ProviderClass => "octodns_cloudflare.CloudflareProvider";

    public override string DefaultDockerImage => "octodns/cloudflare";
}
