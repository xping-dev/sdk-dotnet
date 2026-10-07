/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Hosting;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The CLI's real auth registrations against a <see cref="FakeCloud"/>, with a fake clock and a
/// scratch <c>~/.xping</c>.
/// </summary>
internal sealed class AuthTestHost : IAsyncDisposable
{
    public AuthTestHost(string cliVersion = "1.0.0")
    {
        Time = new TimerTrackingTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        Cloud = new FakeCloud(Time);
        HomeDirectory = Path.Combine(Path.GetTempPath(), "xping-cli-auth-tests", Guid.NewGuid().ToString("N"), ".xping");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false));
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(new XpingHome(HomeDirectory));
        services.AddSingleton(new CliVersion(cliVersion));
        Services = services.BuildServiceProvider();
    }

    public TimerTrackingTimeProvider Time { get; }

    public FakeCloud Cloud { get; }

    public string HomeDirectory { get; }

    public ServiceProvider Services { get; }

    public DiscoveryClient Discovery => Services.GetRequiredService<DiscoveryClient>();

    public OAuthClient OAuth => Services.GetRequiredService<OAuthClient>();

    public Task<DiscoveryDocument> DiscoverAsync(bool useCache = false) =>
        Discovery.GetAsync(Cloud.CloudUrl, useCache, CancellationToken.None);

    /// <summary>
    /// Waits for <paramref name="task"/>, moving the fake clock past each wait it starts
    /// (<see cref="ClockDriver"/>).
    /// </summary>
    public Task<T> DriveAsync<T>(Task<T> task) => ClockDriver.DriveAsync(Time, task);

    public Task DriveAsync(Task task) => ClockDriver.DriveAsync(Time, task);

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync().ConfigureAwait(false);
        await Cloud.DisposeAsync().ConfigureAwait(false);

        string scratch = Path.GetDirectoryName(HomeDirectory)!;
        if (Directory.Exists(scratch))
            Directory.Delete(scratch, recursive: true);
    }
}
