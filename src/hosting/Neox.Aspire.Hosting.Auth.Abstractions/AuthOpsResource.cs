using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Shared AuthOps resource that owns cross-app pipeline gates (<c>prereq-auth</c>, <c>deploy-auth</c>).
/// </summary>
public sealed class AuthOpsResource : Resource
{
    public const string DefaultName = "auth-ops";

    public AuthOpsResource(string name = DefaultName)
        : base(name)
    {
    }
}
