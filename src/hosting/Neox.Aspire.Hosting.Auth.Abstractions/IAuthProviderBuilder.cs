using Aspire.Hosting;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Starts configuring an AuthOps identity provider (select a provider extension such as <c>.Entra(...)</c> next).
/// </summary>
public interface IAuthProviderBuilder
{
    /// <summary>
    /// The distributed application builder that owns this AuthOps provider.
    /// </summary>
    IDistributedApplicationBuilder ApplicationBuilder { get; }

    /// <summary>
    /// Resource name for the Auth provider.
    /// </summary>
    string Name { get; }
}
