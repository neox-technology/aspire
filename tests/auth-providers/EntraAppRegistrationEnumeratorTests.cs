using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class EntraAppRegistrationEnumeratorTests
{
    [Fact]
    public async Task TryGetAppRegistrationOptionsAsync_EmptyTenant_ReturnsEmpty()
    {
        var options = await EntraAppRegistrationEnumerator.TryGetAppRegistrationOptionsAsync(
            tenantId: " ",
            cancellationToken: CancellationToken.None);

        Assert.Empty(options);
    }

    [Fact]
    public void CreateSentinel_IsRecognized()
    {
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(null));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(""));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(EntraAppRegistrationParameterPrompt.CreateSentinel));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCreateSentinel("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCreateSentinel(EntraAppRegistrationParameterPrompt.CustomSentinel));
    }

    [Fact]
    public void MaxOptions_IsTwoHundred()
    {
        Assert.Equal(200, EntraAppRegistrationEnumerator.MaxOptions);
    }
}
