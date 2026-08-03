using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Base Aspire resource for an API permission exposed by an <see cref="EntraAuthAppRegistrationResource"/>
/// (OAuth2 permission scope or app role).
/// </summary>
/// <remarks>
/// Does not implement <see cref="IResourceWithParent"/> so Aspire does not inherit the parent app's
/// <c>HealthCheckAnnotation</c> onto this leaf (which would pin child health to the app registration).
/// Dashboard nesting uses <c>WithParentRelationship</c> only.
/// </remarks>
public abstract class ApiExposition : Resource
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

    /// <summary>Auth app that exposes this permission (dashboard parent).</summary>
    public EntraAuthAppRegistrationResource Parent => Owner;
}
