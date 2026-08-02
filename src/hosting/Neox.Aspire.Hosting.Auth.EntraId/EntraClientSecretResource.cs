using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard child resource for an Entra Auth app client secret (<c>{app}-clientsecret</c>).
/// </summary>
public sealed class EntraClientSecretResource : Resource, IResourceWithParent<EntraAuthAppRegistrationResource>
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

    /// <inheritdoc />
    public EntraAuthAppRegistrationResource Parent => Owner;

    IResource IResourceWithParent.Parent => Owner;

    /// <summary>Workload secret parameter bound to this resource.</summary>
    public ParameterResource Parameter { get; }
}
