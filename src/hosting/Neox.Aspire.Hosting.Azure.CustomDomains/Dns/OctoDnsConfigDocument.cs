using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

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
