/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Cloud;
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
    /// Applies the isolation, with one more variable the commands read and the process does not have.
    /// </summary>
    /// <param name="name">The variable.</param>
    /// <param name="value">Its value.</param>
    public static Action<IServiceCollection> With(string name, string value) => services =>
    {
        Configure(services);
        services.AddSingleton<IEnvironmentVariableProvider>(new WithoutCloudVariables((name, value)));
    };

    /// <summary>
    /// The process environment with the Cloud variables removed, and nothing else changed but the
    /// one variable a test sets.
    /// </summary>
    private sealed class WithoutCloudVariables((string Name, string Value)? set = null) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) =>
            set is { } variable && string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase)
                ? variable.Value
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
