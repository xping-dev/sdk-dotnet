/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Flows;
using Xping.Cli.Auth.Store;
using Xping.Cli.Commands;
using Xping.Cli.Commands.Auth;
using Xping.Cli.Configuration;
using Xping.Cli.Report;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Cli.Services;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Shared;

namespace Xping.Cli.Hosting;

/// <summary>
/// Registers the services backing the <c>xping</c> command surface.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXpingCliServices(
        this IServiceCollection services,
        TextWriter output,
        TextWriter error,
        TextReader input,
        Terminals terminals)
    {
        services.AddSingleton(new ConsoleIO(output, error, input, terminals));
        services.AddSingleton<GlobalOptions>();
        services.AddSingleton<ILocalSessionStoreFactory, LocalSessionStoreFactory>();

        services.AddXpingLocalAnalysis();

        services.AddTransient<ReportCommand>();
        services.AddTransient<WhereCommand>();
        services.AddTransient<ClearCommand>();

        services.AddXpingCliAuth();

        return services;
    }

    /// <summary>
    /// Registers <c>login</c>, <c>logout</c> and <c>auth status</c>, and the services behind them.
    /// </summary>
    /// <remarks>
    /// The Cloud HTTP client, the credential stores and the token refresher register here as they
    /// are built (cli-auth-cli-spec §2.2). None of it is added to the SDK uploader's client.
    /// </remarks>
    private static IServiceCollection AddXpingCliAuth(this IServiceCollection services)
    {
        services.AddTransient<LoginCommand>();
        services.AddTransient<LogoutCommand>();
        services.AddTransient<AuthStatusCommand>();

        services.AddXpingSerialization();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new CliVersion(XpingVersion.Current));
        services.AddSingleton(_ => XpingHome.ForCurrentUser());
        services.AddSingleton<DiscoveryCache>();
        services.AddSingleton<DiscoveryClient>();
        services.AddSingleton<OAuthClient>();
        services.AddSingleton<FileCredentialStore>();
        services.AddSingleton<CredentialStoreSelector>();
        services.AddSingleton<CredentialResolver>();

        services.TryAddSingleton<IEnvironmentVariableProvider, ProcessEnvironment>();
        services.AddSingleton<CliConfigurationLoader>();
        services.AddSingleton<ILauncherProcess, LauncherProcess>();
        services.AddSingleton<IBrowserLauncher>(provider => new BrowserLauncher(
            provider.GetRequiredService<ILauncherProcess>(),
            HostOsDetector.Current,
            provider.GetRequiredService<ILogger<BrowserLauncher>>()));
        services.AddSingleton<IHeadlessDetector>(provider => new HeadlessDetector(
            provider.GetRequiredService<IEnvironmentVariableProvider>(),
            HostOsDetector.Current,
            File.Exists));
        services.AddTransient<LoopbackFlow>();

        services
            .AddHttpClient(AuthHttpClients.OAuth, (provider, client) =>
            {
                client.Timeout = AuthHttpClients.OAuthTimeout;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(provider.GetRequiredService<CliVersion>().UserAgent);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })

            // A redirect on the back channel is never followed (contract §10.2), and no cookie the
            // Portal sets for its browser session belongs in a CLI request.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })

            // The factory's logging handlers write full URLs and headers. They would be silenced by
            // the verbose logger's category filter anyway; removing them means no filter has to hold.
            .RemoveAllLoggers();

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

        services.AddSingleton<IFindingProvider, FailureModeProvider>();
        services.AddSingleton<IFindingProvider, RetryProvider>();
        services.AddSingleton<IFindingProvider, DurationProvider>();
        services.AddSingleton<IFindingProvider, VanishedProvider>();
        services.AddSingleton<IFindingProvider, ParallelSensitiveProvider>();
        services.AddSingleton<IFindingProvider, TimeSensitiveProvider>();

        return services;
    }
}
