using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Base Aspire resource for an API permission exposed by an <see cref="EntraAuthAppRegistrationResource"/>
/// (OAuth2 permission scope or app role).
/// </summary>
public abstract class ApiExposition : Resource, IResourceWithParent<EntraAuthAppRegistrationResource>
{
    protected ApiExposition(string name, EntraAuthAppRegistrationResource owner)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    /// <summary>
    /// Auth app that exposes this permission.
    /// </summary>
    public EntraAuthAppRegistrationResource Owner { get; }

    /// <inheritdoc />
    public EntraAuthAppRegistrationResource Parent => Owner;

    IResource IResourceWithParent.Parent => Owner;
}
