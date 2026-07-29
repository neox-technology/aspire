using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Single OctoDNS YamlProvider zone record (subset of the official zone JSON Schema).
/// </summary>
public sealed class OctoDnsZoneRecord
{
    [YamlMember(Alias = "type", Order = 0)]
    public required string Type { get; init; }

    [YamlMember(Alias = "ttl", Order = 1)]
    public int Ttl { get; init; } = 300;

    [YamlMember(Alias = "value", Order = 2)]
    public required string Value { get; init; }
}

/// <summary>
/// Root OctoDNS config document (providers + zones), aligned with the official config JSON Schema shape.
/// </summary>
public sealed class OctoDnsConfigDocument
{
    [YamlMember(Alias = "providers", Order = 0)]
    public Dictionary<string, Dictionary<string, object?>> Providers { get; init; } = new(StringComparer.Ordinal);

    [YamlMember(Alias = "zones", Order = 1)]
    public Dictionary<string, OctoDnsZoneTarget> Zones { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Zone entry under <c>zones:</c> in <c>octodns.yaml</c>.
/// </summary>
public sealed class OctoDnsZoneTarget
{
    [YamlMember(Alias = "sources", Order = 0)]
    public List<string> Sources { get; init; } = [];

    [YamlMember(Alias = "targets", Order = 1)]
    public List<string> Targets { get; init; } = [];
}
