namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// OctoDNS OVH provider resource.
/// </summary>
public sealed class OvhDomainOpsProviderResource : DomainOpsProviderResource
{
    public OvhDomainOpsProviderResource(string name)
        : base(name)
    {
    }

    public override string ProviderClass => "octodns_ovh.OvhProvider";

    public override string DefaultDockerImage => "octodns/ovh";
}
