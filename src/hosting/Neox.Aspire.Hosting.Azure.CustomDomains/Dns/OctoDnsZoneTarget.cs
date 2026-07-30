using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

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
