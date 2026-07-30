using System.Collections;
using System.Globalization;
using System.Text;
using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Upserts planned ACA DNS records into an OctoDNS zone YAML file.
/// Never removes existing records (upsert-only; delete is not a DomainOps capability).
/// </summary>
public sealed class OctoDnsZoneUpserter
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    private static readonly ISerializer RecordSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>
    /// Upserts <paramref name="plan"/> records into <c>{zoneDirectory}/{zone}.yaml</c>,
    /// creating the file from an empty document when missing.
    /// </summary>
    public string UpsertToDirectory(DnsPlan plan, string zoneDirectory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneDirectory);

        Directory.CreateDirectory(zoneDirectory);
        var path = Path.Combine(zoneDirectory, $"{plan.ZoneName}.yaml");
        var existing = File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : "---" + Environment.NewLine;
        File.WriteAllText(path, UpsertIntoZoneYaml(existing, plan), Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// Upserts planned records into existing zone YAML text by <c>(name, type)</c>.
    /// </summary>
    public string UpsertIntoZoneYaml(string existingYaml, DnsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var root = ParseZone(existingYaml);
        foreach (var record in plan.Records)
        {
            UpsertRecord(root, record.Name ?? string.Empty, record);
        }

        return SerializeZone(root);
    }

    private static Dictionary<string, object?> ParseZone(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml) || yaml.Trim() is "---")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var raw = Deserializer.Deserialize<object>(yaml);
        return NormalizeRoot(raw);
    }

    private static Dictionary<string, object?> NormalizeRoot(object? raw)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (raw is null)
        {
            return result;
        }

        foreach (var (key, value) in EnumerateMap(raw))
        {
            result[key] = value;
        }

        return result;
    }

    private static IEnumerable<(string Key, object? Value)> EnumerateMap(object raw)
    {
        if (raw is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                yield return (key, entry.Value);
            }
        }
    }

    private static void UpsertRecord(Dictionary<string, object?> root, string nameKey, DnsRecord record)
    {
        var records = GetRecordList(root, nameKey);
        var upserted = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ttl"] = record.Ttl,
            ["type"] = record.Type,
            ["value"] = record.Value
        };

        var replaced = false;
        for (var i = 0; i < records.Count; i++)
        {
            if (!TryGetRecordType(records[i], out var existingType))
            {
                continue;
            }

            if (!string.Equals(existingType, record.Type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            records[i] = upserted;
            replaced = true;
            break;
        }

        if (!replaced)
        {
            records.Add(upserted);
        }

        root[nameKey] = records;
    }

    private static List<object?> GetRecordList(Dictionary<string, object?> root, string nameKey)
    {
        if (!root.TryGetValue(nameKey, out var existing) || existing is null)
        {
            return [];
        }

        if (existing is string)
        {
            return [existing];
        }

        if (existing is IDictionary)
        {
            return [existing];
        }

        if (existing is IList list)
        {
            var result = new List<object?>(list.Count);
            foreach (var item in list)
            {
                result.Add(item);
            }

            return result;
        }

        return [existing];
    }

    private static bool TryGetRecordType(object? record, out string type)
    {
        type = string.Empty;
        if (record is null)
        {
            return false;
        }

        foreach (var (key, value) in EnumerateMap(record))
        {
            if (!string.Equals(key, "type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            type = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return type.Length > 0;
        }

        return false;
    }

    private static string SerializeZone(Dictionary<string, object?> root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");

        foreach (var key in root.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var name = string.IsNullOrEmpty(key) ? "''" : QuoteScalar(key);
            sb.Append(name).Append(':').AppendLine();

            var records = GetRecordList(root, key);
            foreach (var record in records)
            {
                AppendRecord(sb, record);
            }
        }

        return sb.ToString();
    }

    private static void AppendRecord(StringBuilder sb, object? record)
    {
        if (record is null)
        {
            return;
        }

        // Prefer OctoDnsZoneRecord ordering (ttl, type, value) when the entry is a simple upsert shape.
        if (TryAsSimpleRecord(record, out var simple))
        {
            var model = new OctoDnsZoneRecord
            {
                Ttl = simple.Ttl,
                Type = simple.Type,
                Value = simple.Value
            };
            WriteIndentedListItem(sb, RecordSerializer.Serialize(model));
            return;
        }

        WriteIndentedListItem(sb, RecordSerializer.Serialize(record));
    }

    private static bool TryAsSimpleRecord(object record, out (int Ttl, string Type, string Value) simple)
    {
        simple = default;
        string? type = null;
        string? value = null;
        int? ttl = null;
        var extraKeys = false;

        foreach (var (key, raw) in EnumerateMap(record))
        {
            if (string.Equals(key, "type", StringComparison.OrdinalIgnoreCase))
            {
                type = Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            else if (string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            else if (string.Equals(key, "ttl", StringComparison.OrdinalIgnoreCase))
            {
                ttl = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            else
            {
                extraKeys = true;
            }
        }

        if (extraKeys || type is null || value is null)
        {
            return false;
        }

        simple = (ttl ?? 300, type, value);
        return true;
    }

    private static void WriteIndentedListItem(StringBuilder sb, string recordYaml)
    {
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
