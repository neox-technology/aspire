using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Consumer Auth app dependency on an exposed scope or app role (<c>WithApiPermission</c>).
/// </summary>
public sealed class ApiPermissionAnnotation(ApiExposition exposition) : IResourceAnnotation
{
    /// <summary>Exposed scope or app role to consume.</summary>
    public ApiExposition Exposition { get; } = exposition
        ?? throw new ArgumentNullException(nameof(exposition));
}
