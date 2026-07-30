using Neox.Aspire.Hosting.Azure.Dns;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class OctoDnsZoneWriterTests
{
    [Fact]
    public void WriteZoneYaml_IncludesRecordTypes()
    {
        var plan = new DnsRecordPlanner().Plan(new DnsPlanInput(
            "www.contoso.com",
            "app.example.azurecontainerapps.io",
            "1.2.3.4",
            "asuid-value"));

        var yaml = new OctoDnsZoneWriter().WriteZoneYaml(plan);

        Assert.Contains("type: A", yaml, StringComparison.Ordinal);
        Assert.Contains("type: TXT", yaml, StringComparison.Ordinal);
        Assert.Contains("asuid.www", yaml, StringComparison.Ordinal);
        Assert.Contains("1.2.3.4", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("type: CNAME", yaml, StringComparison.Ordinal);
        // OctoDNS enforce_order: ttl before type before value within each record mapping.
        Assert.Matches(@"(?s)- ttl: \d+\s+type: A\s+value:", yaml);
    }

    [Fact]
    public void WriteToDirectory_CreatesZoneFile()
    {
        var plan = new DnsRecordPlanner().Plan(new DnsPlanInput(
            "contoso.com",
            "app.example.azurecontainerapps.io",
            "1.2.3.4",
            "asuid-value"));

        var dir = Path.Combine(Path.GetTempPath(), "neox-octodns-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = new OctoDnsZoneWriter().WriteToDirectory(plan, dir);
            Assert.True(File.Exists(path));
            Assert.Equal("contoso.com.yaml", Path.GetFileName(path));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
