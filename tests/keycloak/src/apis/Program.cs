using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Neox Aspire Keycloak sample API",
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
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(
                    $"{builder.Configuration["Keycloak:AuthServerUrl"]}/realms/{builder.Configuration["Keycloak:Realm"]}/protocol/openid-connect/auth"),
                TokenUrl = new Uri(
                    $"{builder.Configuration["Keycloak:AuthServerUrl"]}/realms/{builder.Configuration["Keycloak:Realm"]}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect scope." }
                }
            }
        }
    });
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: builder.Configuration["Keycloak:ServiceName"]!,
        realm: builder.Configuration["Keycloak:Realm"]!,
        options =>
        {
            options.Audience = builder.Configuration["Keycloak:Audience"];

            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "v1");
        c.OAuthClientId(builder.Configuration["Keycloak:ClientId"]);
        c.OAuthClientSecret(builder.Configuration["Keycloak:ClientSecret"]);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/me", [Authorize] (HttpContext context) =>
{
    var name = context.User.Identity?.Name ?? context.User.FindFirst("preferred_username")?.Value;
    return Results.Ok(new { name });
});

app.Run();
