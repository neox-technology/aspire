using System.Text.Json;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class GoogleProjectEnumeratorTests
{
    [Fact]
    public void MaxProjects_IsTwoHundred()
    {
        Assert.Equal(200, GoogleProjectEnumerator.MaxProjects);
    }

    [Fact]
    public async Task TryGetProjectOptionsAsync_DoesNotThrow_WhenNoCredential()
    {
        // Uses ambient ADC when present; otherwise returns empty — must never throw into Choice UI.
        var options = await GoogleProjectEnumerator.TryGetProjectOptionsAsync(
            cancellationToken: CancellationToken.None);

        Assert.NotNull(options);
        Assert.True(options.Count <= GoogleProjectEnumerator.MaxProjects);
    }

    [Fact]
    public void GcloudJson_Shape_DeserializesProjectIdAndName()
    {
        const string json = """
            [
              {"projectId":"neox-technology","name":"Neox Technology"},
              {"projectId":"sokomwatt","name":"Sokomwatt"}
            ]
            """;

        var projects = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
        Assert.NotNull(projects);
        Assert.Equal(2, projects!.Count);
        Assert.Equal("neox-technology", projects[0]["projectId"]);
    }
}
