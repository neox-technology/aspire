using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard child resource for an Entra Auth app client secret (<c>{app}-clientsecret</c>).
/// </summary>
/// <remarks>
/// Does not implement <see cref="IResourceWithParent"/> so Aspire does not inherit the parent app's
/// <c>HealthCheckAnnotation</c> onto this leaf. Dashboard nesting uses <c>WithParentRelationship</c>.
/// </remarks>
public sealed class EntraClientSecretResource : Resource
{
    public EntraClientSecretResource(
        string name,
        EntraAuthAppRegistrationResource owner,
        ParameterResource parameter)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(parameter);
        Owner = owner;
        Parameter = parameter;
    }

    /// <summary>Auth app that owns this client secret.</summary>
    public EntraAuthAppRegistrationResource Owner { get; }

    /// <summary>Auth app that owns this client secret (dashboard parent).</summary>
    public EntraAuthAppRegistrationResource Parent => Owner;

    /// <summary>Workload secret parameter bound to this resource.</summary>
    public ParameterResource Parameter { get; }
}
