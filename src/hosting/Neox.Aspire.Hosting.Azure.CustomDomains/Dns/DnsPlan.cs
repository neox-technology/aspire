namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Result of DNS planning for a single hostname.
/// </summary>
public sealed record DnsPlan(HostnameKind Kind, string ZoneName, string RelativeHost, IReadOnlyList<DnsRecord> Records);
