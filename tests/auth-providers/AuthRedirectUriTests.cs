using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthRedirectUriTests
{
    [Fact]
    public void WithLocalhostRedirectUri_TypeOnly_IsHttpsLocalhost()
    {
        var web = CreateApp();

        web.WithLocalhostRedirectUri(AuthApplicationType.Web);

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Web, entry.RedirectUriType);
        Assert.Equal("https://localhost", entry.Literal);
        Assert.Null(entry.Parameter);
        Assert.Null(entry.Path);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PortOnly()
    {
        var web = CreateApp();

        web.WithLocalhostRedirectUri(AuthApplicationType.Spa, 7281);

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Spa, entry.RedirectUriType);
        Assert.Equal("https://localhost:7281", entry.Literal);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PathOnly_NormalizesLeadingSlash()
    {
        var web = CreateApp();

        web.WithLocalhostRedirectUri(AuthApplicationType.Web, path: "signin-oidc");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Web, entry.RedirectUriType);
        Assert.Equal("https://localhost/signin-oidc", entry.Literal);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PortAndPath()
    {
        var web = CreateApp();

        web.WithLocalhostRedirectUri(AuthApplicationType.Native, 7281, "/callback");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Native, entry.RedirectUriType);
        Assert.Equal("https://localhost:7281/callback", entry.Literal);
    }

    [Fact]
    public void WithRedirectUri_Literal_StoresAbsoluteUriAndType()
    {
        var web = CreateApp();

        web.WithRedirectUri(AuthApplicationType.Web, "https://contoso.example/callback");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Web, entry.RedirectUriType);
        Assert.Equal("https://contoso.example/callback", entry.Literal);
        Assert.Null(entry.Parameter);
    }

    [Fact]
    public void WithRedirectUri_Parameter_StoresParameterWithoutPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var baseUrl = builder.AddParameter("public-base-url");
        var web = entra.AddAppRegistration("web", "Test Web");

        web.WithRedirectUri(AuthApplicationType.Spa, baseUrl);

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Spa, entry.RedirectUriType);
        Assert.Null(entry.Literal);
        Assert.Same(baseUrl.Resource, entry.Parameter);
        Assert.Null(entry.Path);
    }

    [Fact]
    public void WithRedirectUri_ParameterAndPath_NormalizesPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var baseUrl = builder.AddParameter("public-base-url");
        var web = entra.AddAppRegistration("web", "Test Web");

        web.WithRedirectUri(AuthApplicationType.Spa, baseUrl, "signin-oidc");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal(AuthApplicationType.Spa, entry.RedirectUriType);
        Assert.Same(baseUrl.Resource, entry.Parameter);
        Assert.Equal("/signin-oidc", entry.Path);
    }

    [Fact]
    public void RedirectUri_Methods_Accumulate_WithTypes()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var baseUrl = builder.AddParameter("public-base-url");
        var web = entra.AddAppRegistration("web", "Test Web");

        web.WithLocalhostRedirectUri(AuthApplicationType.Web, 7281)
            .WithRedirectUri(AuthApplicationType.Spa, "https://contoso.example/callback")
            .WithRedirectUri(AuthApplicationType.Native, baseUrl, "/app");

        Assert.Equal(3, web.Resource.RedirectUris.Count);
        Assert.Equal(AuthApplicationType.Web, web.Resource.RedirectUris[0].RedirectUriType);
        Assert.Equal("https://localhost:7281", web.Resource.RedirectUris[0].Literal);
        Assert.Equal(AuthApplicationType.Spa, web.Resource.RedirectUris[1].RedirectUriType);
        Assert.Equal("https://contoso.example/callback", web.Resource.RedirectUris[1].Literal);
        Assert.Equal(AuthApplicationType.Native, web.Resource.RedirectUris[2].RedirectUriType);
        Assert.Same(baseUrl.Resource, web.Resource.RedirectUris[2].Parameter);
        Assert.Equal("/app", web.Resource.RedirectUris[2].Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("ftp://example.com/callback")]
    public void WithRedirectUri_Literal_RejectsInvalid(string? uri)
    {
        var web = CreateApp();

        Assert.ThrowsAny<ArgumentException>(() =>
            web.WithRedirectUri(AuthApplicationType.Web, uri!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void WithLocalhostRedirectUri_RejectsInvalidPort(int port)
    {
        var web = CreateApp();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            web.WithLocalhostRedirectUri(AuthApplicationType.Web, port));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("/already", "/already")]
    [InlineData("no-slash", "/no-slash")]
    public void NormalizePath_BehavesAsExpected(string? input, string? expected)
    {
        Assert.Equal(expected, AuthAppResourceExtensions.NormalizePath(input));
    }

    private static IResourceBuilder<AuthAppResource> CreateApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        return entra.AddAppRegistration("web", "Test Web");
    }
}
