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
/// TTL <c>0</c> means provider / domain default.
/// </summary>
public sealed record DnsRecord(string Type, string Name, string Value, int Ttl = 0);

/// <summary>
/// Inputs required to plan ACA custom domain DNS records.
/// </summary>
public sealed record DnsPlanInput(
    string CustomHostname,
    string ContainerAppFqdn,
    string EnvironmentStaticIp,
    string CustomDomainVerificationId,
    int Ttl = 0);

/// <summary>
/// Result of DNS planning for a single hostname.
/// </summary>
public sealed record DnsPlan(HostnameKind Kind, string ZoneName, string RelativeHost, IReadOnlyList<DnsRecord> Records);
