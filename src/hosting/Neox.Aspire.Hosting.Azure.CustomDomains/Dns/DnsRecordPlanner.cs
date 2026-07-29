namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Plans A/CNAME + TXT ownership records for Azure Container Apps custom domains.
/// </summary>
public sealed class DnsRecordPlanner
{
    /// <summary>
    /// Builds the DNS records required for managed certificate validation.
    /// </summary>
    public DnsPlan Plan(DnsPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.CustomHostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ContainerAppFqdn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.EnvironmentStaticIp);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.CustomDomainVerificationId);

        var hostname = NormalizeHostname(input.CustomHostname);
        var kind = DetectKind(hostname);
        var (zoneName, relativeHost) = SplitZone(hostname, kind);
        var verificationId = input.CustomDomainVerificationId.Trim();

        var records = new List<DnsRecord>();

        if (kind == HostnameKind.Apex)
        {
            records.Add(new DnsRecord("A", "", input.EnvironmentStaticIp.Trim()));
            records.Add(new DnsRecord("TXT", "asuid", verificationId));
        }
        else
        {
            records.Add(new DnsRecord("CNAME", relativeHost, NormalizeHostname(input.ContainerAppFqdn)));
            records.Add(new DnsRecord("TXT", $"asuid.{relativeHost}", verificationId));
        }

        return new DnsPlan(kind, zoneName, relativeHost, records);
    }

    public static HostnameKind DetectKind(string hostname)
    {
        var labels = NormalizeHostname(hostname).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // apex: example.com (2 labels). subdomain: www.example.com or api.prod.example.com
        return labels.Length <= 2 ? HostnameKind.Apex : HostnameKind.Subdomain;
    }

    internal static string NormalizeHostname(string hostname)
    {
        var value = hostname.Trim().TrimEnd('.').ToLowerInvariant();
        if (value.StartsWith("https://", StringComparison.Ordinal) || value.StartsWith("http://", StringComparison.Ordinal))
        {
            value = new Uri(hostname.Trim()).Host.ToLowerInvariant();
        }

        return value;
    }

    private static (string ZoneName, string RelativeHost) SplitZone(string hostname, HostnameKind kind)
    {
        var labels = hostname.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (labels.Length < 2)
        {
            throw new ArgumentException($"Hostname '{hostname}' must include a registrable domain.", nameof(hostname));
        }

        if (kind == HostnameKind.Apex)
        {
            return (hostname, "");
        }

        var zoneName = string.Join('.', labels[^2], labels[^1]);
        var relativeHost = string.Join('.', labels[..^2]);
        return (zoneName, relativeHost);
    }
}
