/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Commands;
using Xping.Cli.Services;

namespace Xping.Cli.Hosting;

/// <summary>
/// Registers the services backing the <c>xping</c> command surface.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXpingCliServices(
        this IServiceCollection services, TextWriter output, TextWriter error, TextReader input)
    {
        services.AddSingleton(new ConsoleIO(output, error, input));
        services.AddSingleton<ILocalRunStoreFactory, LocalRunStoreFactory>();

        services.AddTransient<ReportCommand>();
        services.AddTransient<WhereCommand>();
        services.AddTransient<ClearCommand>();

        // Extension point for the Xping Dashboard work: an authenticated HTTP client with Polly
        // resilience (mirroring XpingServiceCollectionExtensions.AddXpingUploader in
        // Xping.Sdk.Core) and token-storage services will register here, e.g.:
        //   services.AddHttpClient<IDashboardClient, DashboardClient>((sp, client) => { ... })
        //       .AddResilienceHandler("xping-dashboard-resilience", (builder, context) => { ... });
        //   services.AddXpingDashboardAuth();

        return services;
    }
}
