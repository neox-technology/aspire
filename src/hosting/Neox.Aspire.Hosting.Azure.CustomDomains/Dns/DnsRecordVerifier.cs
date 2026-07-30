namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Compares expected DNS records to an observed set (used after OctoDNS sync / DNS wait).
/// </summary>
public sealed class DnsRecordVerifier
{
    /// <summary>
    /// Returns human-readable drift lines for expected records missing from <paramref name="actual"/>.
    /// </summary>
    public IReadOnlyList<string> FindDrift(IReadOnlyList<DnsRecord> expected, IReadOnlyList<DnsRecord> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var drift = new List<string>();
        var actualSet = actual
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in expected)
        {
            var key = Normalize(record);
            if (!actualSet.Contains(key))
            {
                drift.Add($"Missing {record.Type} {record.Name} -> {record.Value}");
            }
        }

        return drift;
    }

    private static string Normalize(DnsRecord record)
        => $"{record.Type}|{record.Name.TrimEnd('.')}|{record.Value.TrimEnd('.')}".ToLowerInvariant();
}
