using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Neox.Aspire.Hosting.Auth;

internal static class EntraAuthDashboardServiceCollectionExtensions
{
    public static void AddEntraAuthDashboardServices(this IDistributedApplicationBuilder applicationBuilder)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);

        applicationBuilder.Services.TryAddSingleton<EntraAuthDashboardStatusService>();
        applicationBuilder.Services.TryAddSingleton<IEntraAuthHealthProbe>(sp =>
            EntraGraphAuthHealthProbe.Create(sp));
        applicationBuilder.Services.TryAddEventingSubscriber<EntraAuthDashboardLifecycleSubscriber>();
    }
}
