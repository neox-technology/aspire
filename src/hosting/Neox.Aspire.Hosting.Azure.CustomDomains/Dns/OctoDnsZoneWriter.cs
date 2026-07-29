using System.Text;
using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Writes OctoDNS zone YAML fragments from a <see cref="DnsPlan"/> using YamlDotNet models
/// (shape aligned with the official OctoDNS zone JSON Schema).
/// </summary>
public sealed class OctoDnsZoneWriter
{
    private static readonly ISerializer RecordSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>
    /// Serializes planned records into OctoDNS YamlProvider format for the zone.
    /// </summary>
    public string WriteZoneYaml(DnsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sb = new StringBuilder();
        sb.AppendLine("---");

        foreach (var group in plan.Records.GroupBy(r => r.Name ?? string.Empty, StringComparer.Ordinal))
        {
            var key = string.IsNullOrEmpty(group.Key) ? "''" : QuoteScalar(group.Key);
            sb.Append(key).Append(':').AppendLine();

            foreach (var record in group)
            {
                var model = new OctoDnsZoneRecord
                {
                    Type = record.Type,
                    Ttl = record.Ttl,
                    Value = record.Value
                };

                var recordYaml = RecordSerializer.Serialize(model);
                var lines = recordYaml.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                    {
                        sb.Append("  - ").AppendLine(lines[i]);
                    }
                    else
                    {
                        sb.Append("    ").AppendLine(lines[i]);
                    }
                }
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
        File.WriteAllText(path, WriteZoneYaml(plan), Encoding.UTF8);
        return path;
    }

    private static string QuoteScalar(string value)
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
