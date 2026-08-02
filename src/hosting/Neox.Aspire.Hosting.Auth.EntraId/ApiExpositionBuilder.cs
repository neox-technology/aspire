using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class ApiExpositionBuilder(
    IResourceBuilder<EntraAuthAppRegistrationResource> appBuilder) : IApiExpositionBuilder
{
    public IResourceBuilder<ScopeApiExposition> AddScopeWithAdminConsent(
        string name,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName = null,
        string? userConsentDescription = null) =>
        AddScope(
            name,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName,
            userConsentDescription,
            allowUserConsent: false);

    public IResourceBuilder<ScopeApiExposition> AddScopeWithAdminAndUserConsent(
        string name,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string userConsentDisplayName,
        string userConsentDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userConsentDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userConsentDescription);

        return AddScope(
            name,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName,
            userConsentDescription,
            allowUserConsent: true);
    }

    private IResourceBuilder<ScopeApiExposition> AddScope(
        string name,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName,
        string? userConsentDescription,
        bool allowUserConsent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConsentDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConsentDescription);

        var app = appBuilder.Resource;
        var resourceName = $"{app.Name}-scope-{Sanitize(name)}";
        var scope = new ScopeApiExposition(
            resourceName,
            app,
            name,
            adminConsentDisplayName,
            adminConsentDescription,
            userConsentDisplayName,
            userConsentDescription,
            allowUserConsent,
            Guid.NewGuid());

        appBuilder.WithAnnotation(new ExposedApiAnnotation(scope));

        return appBuilder.ApplicationBuilder.AddResource(scope)
            .ExcludeFromManifest()
            .WithParentRelationship(appBuilder.Resource)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthApiScope"));
    }

    internal static string Sanitize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var c in value)
        {
            buffer[i++] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-';
        }

        return new string(buffer).Trim('-');
    }
}
