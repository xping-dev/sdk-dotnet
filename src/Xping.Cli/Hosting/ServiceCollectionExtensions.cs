/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
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
        services.AddSingleton<ILocalSessionStoreFactory, LocalSessionStoreFactory>();

        services.AddXpingLocalAnalysis();

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

    /// <summary>
    /// Registers local analysis and every finding provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Providers are listed explicitly rather than discovered by scanning the assembly. Reflection
    /// order is not guaranteed to be stable, and the report has to be byte-identical between runs;
    /// an explicit list also means a provider cannot start running because someone happened to add
    /// a class in the right namespace.
    /// </para>
    /// <para>
    /// A new metric is one line here. That is the whole extension point.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddXpingLocalAnalysis(this IServiceCollection services)
    {
        // Injected rather than read statically so that `--since <date>` — the one place analysis
        // reads a clock — can be pinned in tests.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWindowResolver, WindowResolver>();
        services.AddSingleton<FindingCoordinator>();

        services.AddSingleton<IFindingProvider, RetryMaskedProvider>();
        services.AddSingleton<IFindingProvider, VanishedProvider>();

        return services;
    }
}
