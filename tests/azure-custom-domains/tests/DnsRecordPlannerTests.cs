using Neox.Aspire.Hosting.Azure.Dns;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DnsRecordPlannerTests
{
    private readonly DnsRecordPlanner _planner = new();

    [Fact]
    public void Plan_Apex_EmitsAAndAsuidTxt()
    {
        var plan = _planner.Plan(new DnsPlanInput(
            "contoso.com",
            "api.nicehill-1234.westeurope.azurecontainerapps.io",
            "20.50.1.2",
            "verification-code"));

        Assert.Equal(HostnameKind.Apex, plan.Kind);
        Assert.Equal("contoso.com", plan.ZoneName);
        Assert.Contains(plan.Records, r => r.Type == "A" && r.Name == "" && r.Value == "20.50.1.2" && r.Ttl == 0);
        Assert.Contains(plan.Records, r => r.Type == "TXT" && r.Name == "asuid" && r.Value == "verification-code" && r.Ttl == 0);
    }

    [Fact]
    public void Plan_Subdomain_EmitsAAndAsuidTxt()
    {
        var plan = _planner.Plan(new DnsPlanInput(
            "www.contoso.com",
            "api.nicehill-1234.westeurope.azurecontainerapps.io",
            "20.50.1.2",
            "verification-code"));

        Assert.Equal(HostnameKind.Subdomain, plan.Kind);
        Assert.Equal("contoso.com", plan.ZoneName);
        Assert.Equal("www", plan.RelativeHost);
        Assert.Contains(plan.Records, r => r.Type == "A" && r.Name == "www" && r.Value == "20.50.1.2" && r.Ttl == 0);
        Assert.Contains(plan.Records, r => r.Type == "TXT" && r.Name == "asuid.www" && r.Value == "verification-code" && r.Ttl == 0);
        Assert.DoesNotContain(plan.Records, r => r.Type == "CNAME");
    }

    [Fact]
    public void Plan_ExplicitTtl_AppliesToAllRecords()
    {
        var plan = _planner.Plan(new DnsPlanInput(
            "www.contoso.com",
            "api.nicehill-1234.westeurope.azurecontainerapps.io",
            "20.50.1.2",
            "verification-code",
            Ttl: 3600));

        Assert.All(plan.Records, r => Assert.Equal(3600, r.Ttl));
    }

    [Theory]
    [InlineData("example.com", HostnameKind.Apex)]
    [InlineData("www.example.com", HostnameKind.Subdomain)]
    [InlineData("api.prod.example.com", HostnameKind.Subdomain)]
    public void DetectKind_MatchesExpectation(string hostname, HostnameKind expected)
    {
        Assert.Equal(expected, DnsRecordPlanner.DetectKind(hostname));
    }
}
