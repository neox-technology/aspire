using Aspire.Hosting;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        Infrastructure.Configure(builder);
        Auth.Configure(builder);
        Services.Configure(builder);
        Web.Configure(builder);

        builder.Build().Run();
    }
}
