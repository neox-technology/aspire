namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra app registration under an <see cref="EntraAuthOpsResource"/>.
/// </summary>
public sealed class EntraAuthAppRegistrationResource : AuthAppRegistrationResource
{
    public EntraAuthAppRegistrationResource(string name, EntraAuthOpsResource provider, string displayName)
        : base(name, provider, displayName)
    {
        EntraProvider = provider;
    }

    /// <summary>
    /// Parent Entra Auth provider.
    /// </summary>
    public EntraAuthOpsResource EntraProvider { get; }
}
