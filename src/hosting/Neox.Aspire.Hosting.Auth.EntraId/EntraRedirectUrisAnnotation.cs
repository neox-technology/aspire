using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra Graph platform assignment for redirect URIs accumulated on an Auth app.
/// </summary>
internal sealed class EntraRedirectUrisAnnotation : IResourceAnnotation
{
    public List<EntraRedirectUriEntry> Entries { get; } = [];
}

/// <summary>
/// One Entra-typed redirect entry (platform bucket + shared flat URI model).
/// </summary>
internal sealed class EntraRedirectUriEntry
{
    public required AuthApplicationType Type { get; init; }

    public required AuthRedirectUri Uri { get; init; }
}
