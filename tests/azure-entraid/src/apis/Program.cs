using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Neox Aspire Entra ID sample API",
        Version = typeof(Program).Assembly.GetName().Version?.ToString()
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document),
            new List<string>()
        }
    });
    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"),
                Scopes = new Dictionary<string, string>
                {
                    { $"api://{builder.Configuration["AzureAd:ClientId"]}/access_as_user", "Access as user." }
                }
            }
        }
    });
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "v1");
        c.OAuthAppName(JwtBearerDefaults.AuthenticationScheme);
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
