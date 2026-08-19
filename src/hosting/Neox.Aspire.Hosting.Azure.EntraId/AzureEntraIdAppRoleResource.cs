using System.Text;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire child resource for an app role exposed by an
/// <see cref="AzureEntraIdAppRegistrationResource"/>. Not a provisioning resource;
/// Graph <c>appRoles</c> is emitted by the parent module.
/// </summary>
public sealed class AzureEntraIdAppRoleResource : Resource, IResourceWithParent<AzureEntraIdAppRegistrationResource>, IAzureEntraIdPermissibleResource
{
    internal const string GraphUserMemberType = "User";
    internal const string GraphApplicationMemberType = "Application";

    /// <summary>
    /// RFC 4122 OID namespace used to derive a stable Graph app-role <c>id</c>
    /// (distinct from the URL namespace used by scopes).
    /// </summary>
    private static readonly Guid AppRoleIdNamespace = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

    private static readonly AllowedMemberTypes ValidMemberTypes =
        AllowedMemberTypes.User | AllowedMemberTypes.Application;

    /// <summary>
    /// Initializes a new <see cref="AzureEntraIdAppRoleResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="parent">App registration that exposes this role.</param>
    /// <param name="allowedMemberTypes">Graph <c>allowedMemberTypes</c> flags.</param>
    /// <param name="value">Graph <c>appRoles[].value</c>.</param>
    /// <param name="displayName">Graph <c>displayName</c>.</param>
    /// <param name="description">Graph <c>description</c>.</param>
    public AzureEntraIdAppRoleResource(
        string name,
        AzureEntraIdAppRegistrationResource parent,
        AllowedMemberTypes allowedMemberTypes,
        string value,
        string displayName,
        string description)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ThrowIfInvalidMemberTypes(allowedMemberTypes);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentException.ThrowIfNullOrEmpty(description);

        Parent = parent;
        AllowedMemberTypes = allowedMemberTypes;
        Value = value;
        DisplayName = displayName;
        Description = description;
        Id = CreateAppRoleId(parent.Name, value);
    }

    /// <inheritdoc />
    public AzureEntraIdAppRegistrationResource Parent { get; }

    /// <summary>
    /// Graph <c>allowedMemberTypes</c> flags.
    /// </summary>
    public AllowedMemberTypes AllowedMemberTypes { get; }

    /// <summary>
    /// Graph <c>appRoles[].value</c> (roles claim).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Graph <c>displayName</c>.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Graph <c>description</c>.
    /// </summary>
    public string Description { get; }

    /// <inheritdoc />
    public AzureEntraIdPermissionType Type => AzureEntraIdPermissionType.Role;

    /// <summary>
    /// Stable Graph <c>appRoles[].id</c> derived from parent name and <see cref="Value"/>.
    /// </summary>
    public Guid Id { get; }

    internal IEnumerable<string> GraphAllowedMemberTypes
    {
        get
        {
            if (AllowedMemberTypes.HasFlag(AllowedMemberTypes.User))
            {
                yield return GraphUserMemberType;
            }

            if (AllowedMemberTypes.HasFlag(AllowedMemberTypes.Application))
            {
                yield return GraphApplicationMemberType;
            }
        }
    }

    internal static void ThrowIfInvalidMemberTypes(AllowedMemberTypes allowedMemberTypes)
    {
        if (allowedMemberTypes == 0 || (allowedMemberTypes & ~ValidMemberTypes) != 0)
        {
            throw new ArgumentException(
                "allowedMemberTypes must include User, Application, or both.",
                nameof(allowedMemberTypes));
        }
    }

    internal static Guid CreateAppRoleId(string parentName, string value)
    {
        var nameBytes = Encoding.UTF8.GetBytes($"{parentName}:{value}");
        Span<byte> ns = stackalloc byte[16];
        AppRoleIdNamespace.TryWriteBytes(ns, bigEndian: true, out _);

        var data = new byte[16 + nameBytes.Length];
        ns.CopyTo(data);
        nameBytes.CopyTo(data.AsSpan(16));

        Span<byte> hash = stackalloc byte[20];
        System.Security.Cryptography.SHA1.HashData(data, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}
