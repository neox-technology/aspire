using Aspire.Hosting;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests.AppHost;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        Auth.Configure(builder);
        Services.Configure(builder);
        Web.Configure(builder);

        builder.Build().Run();
    }
}
