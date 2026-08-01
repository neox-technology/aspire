using Aspire.Hosting;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class AuthProviderBuilder : IAuthProviderBuilder
{
    public AuthProviderBuilder(IDistributedApplicationBuilder applicationBuilder, string name)
    {
        ApplicationBuilder = applicationBuilder;
        Name = name;
        AuthOpsExtensions.EnsureAuthOpsResource(applicationBuilder);
        AuthOpsExtensions.EnsurePrereqAuthStep(applicationBuilder);
    }

    public IDistributedApplicationBuilder ApplicationBuilder { get; }

    public string Name { get; }
}
