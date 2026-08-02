using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/me", async (
    IAuthorizationHeaderProvider authHeaderProvider,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var authorizationHeader = await authHeaderProvider
        .CreateAuthorizationHeaderForUserAsync(
            ["https://graph.microsoft.com/User.Read"],
            cancellationToken: cancellationToken)
        .ConfigureAwait(false);

    var client = httpClientFactory.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
    request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Results.Problem(
            detail: body,
            statusCode: (int)response.StatusCode,
            title: "Microsoft Graph /me failed");
    }

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    var root = doc.RootElement;

    return Results.Ok(new
    {
        id = root.TryGetProperty("id", out var id) ? id.GetString() : null,
        displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
        mail = root.TryGetProperty("mail", out var mail) ? mail.GetString() : null,
        userPrincipalName = root.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null
    });
}).RequireAuthorization();

app.Run();
