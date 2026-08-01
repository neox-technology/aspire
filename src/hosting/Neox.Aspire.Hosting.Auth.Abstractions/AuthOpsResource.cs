using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Hidden AppHost resource that owns shared AuthOps pipeline steps (<c>aspire do</c>).
/// </summary>
public sealed class AuthOpsResource : Resource
{
    /// <summary>
    /// Default Aspire resource name when AuthOps is first registered (<c>auth-ops</c>).
    /// </summary>
    public const string DefaultResourceName = "auth-ops";

    /// <summary>
    /// Creates the AuthOps marker resource.
    /// </summary>
    public AuthOpsResource(string name) : base(name)
    {
    }
}
