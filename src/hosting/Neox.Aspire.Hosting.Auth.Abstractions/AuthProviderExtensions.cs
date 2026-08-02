using Aspire.Hosting;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Extension methods that register AuthOps provider resources.
/// </summary>
public static class AuthProviderExtensions
{
    /// <summary>
    /// Starts configuring an AuthOps identity provider resource.
    /// </summary>
    public static IAuthProviderBuilder AddAuthProvider(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AuthProviderBuilder(builder, name);
    }
}
