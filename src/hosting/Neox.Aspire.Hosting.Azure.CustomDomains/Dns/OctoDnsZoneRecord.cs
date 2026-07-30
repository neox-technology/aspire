using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Single OctoDNS YamlProvider zone record (subset of the official zone JSON Schema).
/// </summary>
public sealed class OctoDnsZoneRecord
{
    // OctoDNS YamlProvider enforce_order (default true) requires alphabetical keys: ttl, type, value.
    // TTL 0 means provider / domain default.
    [YamlMember(Alias = "ttl", Order = 0)]
    public int Ttl { get; init; } = 0;

    [YamlMember(Alias = "type", Order = 1)]
    public required string Type { get; init; }

    [YamlMember(Alias = "value", Order = 2)]
    public required string Value { get; init; }
}
