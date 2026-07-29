namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Whether the custom hostname is an apex (root) or a subdomain.
/// </summary>
public enum HostnameKind
{
    Apex,
    Subdomain
}

/// <summary>
/// A planned DNS record for OctoDNS / provider sync.
/// </summary>
public sealed record DnsRecord(string Type, string Name, string Value, int Ttl = 300);

/// <summary>
/// Inputs required to plan ACA custom domain DNS records.
/// </summary>
public sealed record DnsPlanInput(
    string CustomHostname,
    string ContainerAppFqdn,
    string EnvironmentStaticIp,
    string CustomDomainVerificationId);

/// <summary>
/// Result of DNS planning for a single hostname.
/// </summary>
public sealed record DnsPlan(HostnameKind Kind, string ZoneName, string RelativeHost, IReadOnlyList<DnsRecord> Records);
