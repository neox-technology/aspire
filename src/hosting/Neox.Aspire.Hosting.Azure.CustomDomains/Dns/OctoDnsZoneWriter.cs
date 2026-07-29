using System.Text;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Writes OctoDNS zone YAML fragments from a <see cref="DnsPlan"/>.
/// </summary>
public sealed class OctoDnsZoneWriter
{
    /// <summary>
    /// Serializes planned records into OctoDNS YamlProvider format for the zone.
    /// </summary>
    public string WriteZoneYaml(DnsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sb = new StringBuilder();
        sb.AppendLine("---");

        foreach (var group in plan.Records.GroupBy(r => string.IsNullOrEmpty(r.Name) ? "" : r.Name))
        {
            var key = string.IsNullOrEmpty(group.Key) ? "''" : Quote(group.Key);
            sb.Append(key).Append(':').AppendLine();

            foreach (var record in group)
            {
                sb.Append("  - type: ").AppendLine(record.Type);
                sb.Append("    ttl: ").Append(record.Ttl).AppendLine();
                sb.Append("    value: ").AppendLine(Quote(record.Value));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the zone YAML to <paramref name="zoneDirectory"/>/<paramref name="plan"/>.ZoneName.yaml.
    /// </summary>
    public string WriteToDirectory(DnsPlan plan, string zoneDirectory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneDirectory);

        Directory.CreateDirectory(zoneDirectory);
        var path = Path.Combine(zoneDirectory, $"{plan.ZoneName}.yaml");
        File.WriteAllText(path, WriteZoneYaml(plan));
        return path;
    }

    private static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        if (value.Any(c => char.IsWhiteSpace(c) || c is ':' or '#' or '\'' or '"'))
        {
            return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
        }

        return value;
    }
}
