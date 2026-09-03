using System.Text;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire child resource for an OAuth2 permission scope exposed by an
/// <see cref="AzureEntraIdAppRegistrationResource"/>. Not a provisioning resource;
/// Graph <c>api.oauth2PermissionScopes</c> is emitted by the parent module.
/// </summary>
public sealed class AzureEntraIdScopeResource : Resource, IResourceWithParent<AzureEntraIdAppRegistrationResource>, IAzureEntraIdPermissibleResource
{
    internal const string AdminConsentType = "Admin";
    internal const string UserConsentType = "User";

    /// <summary>
    /// RFC 4122 URL namespace used to derive a stable Graph scope <c>id</c>.
    /// </summary>
    private static readonly Guid ScopeIdNamespace = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// Initializes a new <see cref="AzureEntraIdScopeResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="parent">App registration that exposes this scope.</param>
    /// <param name="value">Graph <c>oauth2PermissionScopes[].value</c>.</param>
    /// <param name="adminConsentDisplayName">Admin-consent title.</param>
    /// <param name="adminConsentDescription">Admin-consent description.</param>
    /// <param name="userConsentDisplayName">User-consent title, or <see langword="null"/> for admin-only.</param>
    /// <param name="userConsentDescription">User-consent description, or <see langword="null"/> for admin-only.</param>
    public AzureEntraIdScopeResource(
        string name,
        AzureEntraIdAppRegistrationResource parent,
        string value,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName = null,
        string? userConsentDescription = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(adminConsentDescription);

        Parent = parent;
        Value = value;
        AdminConsentDisplayName = adminConsentDisplayName;
        AdminConsentDescription = adminConsentDescription;
        UserConsentDisplayName = userConsentDisplayName;
        UserConsentDescription = userConsentDescription;
        ConsentType = userConsentDisplayName is null ? AdminConsentType : UserConsentType;
        Id = CreateScopeId(parent.Name, value);
    }

    /// <inheritdoc />
    public AzureEntraIdAppRegistrationResource Parent { get; }

    /// <summary>
    /// Graph <c>oauth2PermissionScopes[].value</c> (scp claim).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Graph <c>adminConsentDisplayName</c>.
    /// </summary>
    public string AdminConsentDisplayName { get; }

    /// <summary>
    /// Graph <c>adminConsentDescription</c>.
    /// </summary>
    public string AdminConsentDescription { get; }

    /// <summary>
    /// Graph <c>userConsentDisplayName</c>, or <see langword="null"/> when consent type is Admin.
    /// </summary>
    public string? UserConsentDisplayName { get; }

    /// <summary>
    /// Graph <c>userConsentDescription</c>, or <see langword="null"/> when consent type is Admin.
    /// </summary>
    public string? UserConsentDescription { get; }

    /// <summary>
    /// Graph <c>type</c>: <c>Admin</c> or <c>User</c>.
    /// </summary>
    public string ConsentType { get; }

    /// <inheritdoc />
    public AzureEntraIdPermissionType Type => AzureEntraIdPermissionType.Scope;

    /// <summary>
    /// Stable Graph <c>oauth2PermissionScopes[].id</c> derived from parent name and <see cref="Value"/>.
    /// </summary>
    public Guid Id { get; }

    internal static Guid CreateScopeId(string parentName, string value)
    {
        var nameBytes = Encoding.UTF8.GetBytes($"{parentName}:{value}");
        Span<byte> ns = stackalloc byte[16];
        ScopeIdNamespace.TryWriteBytes(ns, bigEndian: true, out _);

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
