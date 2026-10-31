/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Cloud;
using Xping.Cli.Services;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests;

/// <summary>
/// Keeps an in-process <c>Program.Run</c> away from the developer's own Cloud credentials and from
/// the network.
/// </summary>
/// <remarks>
/// <para>
/// <c>xping report</c> asks Xping Cloud whenever a credential resolves, and the real resolver reads
/// <c>~/.xping</c>, the OS keychain and <c>XPING_APIKEY</c>. A developer who has run
/// <c>xping login</c> would otherwise see local-report tests make real requests, and pass or fail
/// on what their workspace holds.
/// </para>
/// <para>
/// So the home directory is a scratch one, the keychain is left out, the three Cloud variables are
/// hidden, and both HTTP clients fail any request outright: a test that reaches the network has a
/// bug, and should say so rather than wait for a timeout.
/// </para>
/// </remarks>
internal static class OfflineCli
{
    private static readonly string[] HiddenVariables = ["XPING_APIKEY", "XPING_CLOUDURL", "XPING_PROJECTID"];

    /// <summary>
    /// Applies the isolation to a <c>Program.Run</c> host.
    /// </summary>
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton(new XpingHome(Path.Combine(Path.GetTempPath(), "xping-offline-" + Guid.NewGuid().ToString("N"), ".xping")));
        services.AddSingleton<IEnvironmentVariableProvider>(new WithoutCloudVariables());
        services.AddSingleton(provider => new CredentialStoreSelector(provider.GetRequiredService<FileCredentialStore>(), keychain: null));

        services.AddHttpClient(CloudHttp.ClientName).ConfigurePrimaryHttpMessageHandler(() => new NoNetworkHandler());
        services.AddHttpClient(AuthHttpClients.OAuth).ConfigurePrimaryHttpMessageHandler(() => new NoNetworkHandler());
    }

    /// <summary>
    /// Applies the isolation, with the commands reading the local store at <paramref name="root"/>.
    /// </summary>
    /// <remarks>
    /// Through the commands' environment, never the process's: the SDK recording this test run
    /// reads <c>XPING_LOCAL_STORE</c> too, and the run's own session would land in the fixture.
    /// </remarks>
    /// <param name="root">The store root.</param>
    /// <param name="variables">More variables the commands read and the process does not have.</param>
    public static Action<IServiceCollection> Store(string root, params (string Name, string Value)[] variables) =>
        With([(LocalSessionStoreFactory.StoreVariable, root), .. variables]);

    private static Action<IServiceCollection> With((string Name, string Value)[] variables) => services =>
    {
        Configure(services);
        services.AddSingleton<IEnvironmentVariableProvider>(new WithoutCloudVariables(variables));
    };

    /// <summary>
    /// The process environment with the Cloud variables removed, and nothing else changed but the
    /// variables a test sets.
    /// </summary>
    private sealed class WithoutCloudVariables(params (string Name, string Value)[] set) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) =>
            Array.Find(set, variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase)) is { Name: not null } match
                ? match.Value
                : HiddenVariables.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith("Xping__", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : System.Environment.GetEnvironmentVariable(name);
    }
}

/// <summary>
/// An HTTP handler that fails the test on any request.
/// </summary>
internal sealed class NoNetworkHandler : HttpMessageHandler
{
    /// <summary>
    /// Gets how many requests reached it.
    /// </summary>
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        throw new InvalidOperationException($"No network in this test: {request.Method} {request.RequestUri}");
    }
}
