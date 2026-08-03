using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard child resource for a consumer Auth app API permission
/// (<c>{consumer}-apiperm-{value}</c> / Graph <c>requiredResourceAccess</c>).
/// </summary>
/// <remarks>
/// Does not implement <see cref="IResourceWithParent"/> so Aspire does not inherit the parent app's
/// <c>HealthCheckAnnotation</c> onto this leaf. Dashboard nesting uses <c>WithParentRelationship</c>.
/// </remarks>
public sealed class ApiPermissionResource : Resource
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

    /// <summary>Consumer Auth app (dashboard parent).</summary>
    public EntraAuthAppRegistrationResource Parent => Owner;

    /// <summary>In-model exposed scope or app role, when not well-known.</summary>
    public ApiExposition? Exposition { get; }

    /// <summary>First-party well-known permission, when not in-model.</summary>
    public WellKnownApiPermission? WellKnownPermission { get; }

    /// <summary>Permission value used for naming and status descriptions.</summary>
    public string Value { get; }
}
