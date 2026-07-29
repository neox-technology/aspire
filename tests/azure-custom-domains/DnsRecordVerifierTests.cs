using Neox.Aspire.Hosting.Azure.Dns;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DnsRecordVerifierTests
{
    [Fact]
    public void FindDrift_ReturnsMissingRecords()
    {
        var expected = new[]
        {
            new DnsRecord("CNAME", "www", "app.example.azurecontainerapps.io"),
            new DnsRecord("TXT", "asuid.www", "code")
        };

        var actual = new[]
        {
            new DnsRecord("CNAME", "www", "app.example.azurecontainerapps.io")
        };

        var drift = new DnsRecordVerifier().FindDrift(expected, actual);

        Assert.Single(drift);
        Assert.Contains("asuid.www", drift[0], StringComparison.Ordinal);
    }

    [Fact]
    public void FindDrift_EmptyWhenMatching()
    {
        var records = new[]
        {
            new DnsRecord("A", "", "1.2.3.4"),
            new DnsRecord("TXT", "asuid", "code")
        };

        Assert.Empty(new DnsRecordVerifier().FindDrift(records, records));
    }
}
