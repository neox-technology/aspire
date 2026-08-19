using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Login host injected as <c>{sectionName}Instance</c> for Microsoft.Identity.Web or MSAL.
/// </summary>
public sealed class EntraIdInstance
{
    private const string WorkforceUrl = "https://login.microsoftonline.com/";

    private readonly string? _literalUrl;
    private readonly ParameterResource? _subdomainParameter;

    private EntraIdInstance(string literalUrl)
    {
        _literalUrl = literalUrl;
    }

    private EntraIdInstance(ParameterResource subdomainParameter)
    {
        _subdomainParameter = subdomainParameter;
    }

    /// <summary>
    /// Workforce / organizational directory host (<c>https://login.microsoftonline.com/</c>).
    /// </summary>
    public static EntraIdInstance Workforce { get; } = new(WorkforceUrl);

    /// <summary>
    /// External ID (CIAM) host <c>https://{tenantSubdomain}.ciamlogin.com/</c>.
    /// </summary>
    /// <param name="tenantSubdomain">Directory subdomain (for example <c>contoso</c>), not a URL or GUID.</param>
    public static EntraIdInstance Ciam(string tenantSubdomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantSubdomain);
        var trimmed = tenantSubdomain.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal) || trimmed.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "CIAM tenant subdomain must be the directory subdomain (for example 'contoso'), not a URL.",
                nameof(tenantSubdomain));
        }

        return new($"https://{trimmed}.ciamlogin.com/");
    }

    /// <summary>
    /// External ID (CIAM) host from an Aspire parameter interpolated at env-callback time.
    /// Does not read <see cref="ParameterResource"/> value at <c>Configure()</c>.
    /// </summary>
    public static EntraIdInstance Ciam(IResourceBuilder<ParameterResource> tenantSubdomain)
    {
        ArgumentNullException.ThrowIfNull(tenantSubdomain);
        return new(tenantSubdomain.Resource);
    }

    internal void ApplyInstanceEnvironment<TResource>(
        IResourceBuilder<TResource> builder,
        string sectionName)
        where TResource : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        if (_literalUrl is not null)
        {
            builder.WithEnvironment($"{sectionName}Instance", _literalUrl);
            return;
        }

            builder.WithEnvironment(
                $"{sectionName}Instance",
                ReferenceExpression.Create($"https://{_subdomainParameter!}.ciamlogin.com/"));
    }
}
