using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard child resource for a consumer Auth app API permission
/// (<c>{consumer}-apiperm-{value}</c> / Graph <c>requiredResourceAccess</c>).
/// </summary>
public sealed class ApiPermissionResource : Resource, IResourceWithParent<EntraAuthAppRegistrationResource>
{
    public ApiPermissionResource(
        string name,
        EntraAuthAppRegistrationResource owner,
        ApiExposition exposition)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(exposition);
        Owner = owner;
        Exposition = exposition;
        Value = exposition switch
        {
            ScopeApiExposition scope => scope.ScopeValue,
            AppRoleApiExposition role => role.Value,
            _ => exposition.Name
        };
    }

    public ApiPermissionResource(
        string name,
        EntraAuthAppRegistrationResource owner,
        WellKnownApiPermission wellKnownPermission)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(wellKnownPermission);
        Owner = owner;
        WellKnownPermission = wellKnownPermission;
        Value = wellKnownPermission.Value;
    }

    /// <summary>Consumer Auth app that declares this permission.</summary>
    public EntraAuthAppRegistrationResource Owner { get; }

    /// <inheritdoc />
    public EntraAuthAppRegistrationResource Parent => Owner;

    IResource IResourceWithParent.Parent => Owner;

    /// <summary>In-model exposed scope or app role, when not well-known.</summary>
    public ApiExposition? Exposition { get; }

    /// <summary>First-party well-known permission, when not in-model.</summary>
    public WellKnownApiPermission? WellKnownPermission { get; }

    /// <summary>Permission value used for naming and status descriptions.</summary>
    public string Value { get; }
}
