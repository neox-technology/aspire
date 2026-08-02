using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Base Aspire resource for an API permission exposed by an <see cref="AuthAppResource"/>
/// (OAuth2 permission scope or app role).
/// </summary>
public abstract class ApiExposition : Resource, IResourceWithParent<AuthAppResource>
{
    protected ApiExposition(string name, AuthAppResource owner)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    /// <summary>
    /// Auth app that exposes this permission.
    /// </summary>
    public AuthAppResource Owner { get; }

    /// <inheritdoc />
    public AuthAppResource Parent => Owner;

    IResource IResourceWithParent.Parent => Owner;
}
