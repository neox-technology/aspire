namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// A planned DNS record for OctoDNS / provider sync.
/// TTL <c>0</c> means provider / domain default.
/// </summary>
public sealed record DnsRecord(string Type, string Name, string Value, int Ttl = 0);
