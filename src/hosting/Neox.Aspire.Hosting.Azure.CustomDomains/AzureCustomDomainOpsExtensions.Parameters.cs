using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

public static partial class AzureCustomDomainOpsExtensions
{
    /// <summary>
    /// Aspire parameter name for the custom hostname of compute resource <paramref name="resourceName"/>
    /// (<c>{resource}-domain</c>).
    /// </summary>
    public static string GetCustomDomainParameterName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"{resourceName}-domain";
    }

    /// <summary>
    /// Aspire parameter name for the managed certificate of compute resource <paramref name="resourceName"/>
    /// (<c>{resource}-certificate</c>).
    /// </summary>
    public static string GetCertificateParameterName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"{resourceName}-certificate";
    }

    /// <summary>
    /// Aspire parameter name for the managed certificate paired with domain parameter
    /// <paramref name="customDomain"/> (<c>{domain.Name}-certificate</c>).
    /// </summary>
    public static string GetCertificateParameterName(ParameterResource customDomain)
    {
        ArgumentNullException.ThrowIfNull(customDomain);
        return $"{customDomain.Name}-certificate";
    }

    /// <summary>
    /// Gets or adds domain and certificate parameters for <paramref name="resourceName"/> using dash naming
    /// (<c>{resource}-domain</c> / <c>{resource}-certificate</c>).
    /// Use the returned builders with consumer-owned <c>ConfigureCustomDomain</c>.
    /// </summary>
    /// <param name="applicationBuilder">AppHost builder.</param>
    /// <param name="resourceName">Compute resource name (e.g. <c>api</c>).</param>
    /// <param name="hostname">Optional default hostname for the domain parameter.</param>
    public static (
        IResourceBuilder<ParameterResource> CustomDomain,
        IResourceBuilder<ParameterResource> CertificateName)
        EnsureAzureCustomDomainParameters(
            IDistributedApplicationBuilder applicationBuilder,
            string resourceName,
            string? hostname = null)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var domain = GetOrAddParameter(
            applicationBuilder,
            GetCustomDomainParameterName(resourceName),
            hostname);

        var certificate = GetOrAddCertificateParameter(
            applicationBuilder,
            GetCertificateParameterName(resourceName));

        return (domain, certificate);
    }

    /// <summary>
    /// Gets or adds the certificate parameter deduced from <paramref name="customDomain"/>
    /// (<c>{domain.Name}-certificate</c>).
    /// </summary>
    public static IResourceBuilder<ParameterResource> EnsureAzureCustomDomainCertificateParameter(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<ParameterResource> customDomain)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        ArgumentNullException.ThrowIfNull(customDomain);

        return GetOrAddCertificateParameter(
            applicationBuilder,
            GetCertificateParameterName(customDomain.Resource));
    }

    private static IResourceBuilder<ParameterResource> GetOrAddParameter(
        IDistributedApplicationBuilder applicationBuilder,
        string parameterName,
        string? defaultValue)
    {
        var existing = FindParameter(applicationBuilder, parameterName);
        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        return string.IsNullOrWhiteSpace(defaultValue)
            ? applicationBuilder.AddParameter(parameterName)
            : applicationBuilder.AddParameter(parameterName, defaultValue);
    }

    private static IResourceBuilder<ParameterResource> GetOrAddCertificateParameter(
        IDistributedApplicationBuilder applicationBuilder,
        string parameterName)
    {
        var existing = FindParameter(applicationBuilder, parameterName);
        if (existing is not null)
        {
            return applicationBuilder.CreateResourceBuilder(existing);
        }

        return applicationBuilder.AddParameter(parameterName, string.Empty, publishValueAsDefault: true);
    }

    private static ParameterResource? FindParameter(
        IDistributedApplicationBuilder applicationBuilder,
        string parameterName)
        => applicationBuilder.Resources
            .OfType<ParameterResource>()
            .FirstOrDefault(p => string.Equals(p.Name, parameterName, StringComparison.Ordinal));
}
