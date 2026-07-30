using Neox.Aspire.Hosting.Azure.Dns;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class OctoDnsZoneUpserterTests
{
    [Fact]
    public void UpsertIntoZoneYaml_PreservesUntargetedRecordsAndUpsertsByNameType()
    {
        const string existing = """
            ---
            '':
              - ttl: 3600
                type: MX
                values:
                  - exchange: mx1.contoso.com.
                    preference: 10
              - ttl: 300
                type: A
                value: 1.1.1.1
            mail:
              - ttl: 300
                type: TXT
                value: v=spf1 include:_spf.example.com ~all
            """;

        var plan = new DnsRecordPlanner().Plan(new DnsPlanInput(
            "contoso.com",
            "app.example.azurecontainerapps.io",
            "20.1.2.3",
            "asuid-value"));

        var yaml = new OctoDnsZoneUpserter().UpsertIntoZoneYaml(existing, plan);

        Assert.Contains("type: MX", yaml, StringComparison.Ordinal);
        Assert.Contains("mx1.contoso.com.", yaml, StringComparison.Ordinal);
        Assert.Contains("mail:", yaml, StringComparison.Ordinal);
        Assert.Contains("v=spf1 include:_spf.example.com ~all", yaml, StringComparison.Ordinal);
        Assert.Contains("type: A", yaml, StringComparison.Ordinal);
        Assert.Contains("20.1.2.3", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("1.1.1.1", yaml, StringComparison.Ordinal);
        Assert.Contains("asuid:", yaml, StringComparison.Ordinal);
        Assert.Contains("asuid-value", yaml, StringComparison.Ordinal);
        Assert.Matches(@"(?s)- ttl: \d+\s+type: A\s+value:", yaml);
    }

    [Fact]
    public void UpsertIntoZoneYaml_CreatesFromEmptyDocument()
    {
        var plan = new DnsRecordPlanner().Plan(new DnsPlanInput(
            "www.contoso.com",
            "app.example.azurecontainerapps.io",
            "1.2.3.4",
            "asuid-value"));

        var yaml = new OctoDnsZoneUpserter().UpsertIntoZoneYaml("---\n", plan);

        Assert.Contains("type: CNAME", yaml, StringComparison.Ordinal);
        Assert.Contains("asuid.www", yaml, StringComparison.Ordinal);
        Assert.Contains("app.example.azurecontainerapps.io.", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void UpsertToDirectory_WritesMergedZoneFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "neox-upsert-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var zonePath = Path.Combine(dir, "contoso.com.yaml");
            File.WriteAllText(zonePath, """
                ---
                '':
                  - ttl: 3600
                    type: MX
                    value: 10 mx.contoso.com.
                """);

            var plan = new DnsRecordPlanner().Plan(new DnsPlanInput(
                "www.contoso.com",
                "app.example.azurecontainerapps.io",
                "1.2.3.4",
                "asuid-value"));

            var path = new OctoDnsZoneUpserter().UpsertToDirectory(plan, dir);

            Assert.Equal(zonePath, path);
            var yaml = File.ReadAllText(path);
            Assert.Contains("type: MX", yaml, StringComparison.Ordinal);
            Assert.Contains("type: CNAME", yaml, StringComparison.Ordinal);
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
