/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        Cloud = new FakeCloud(Time);
        HomeDirectory = Path.Combine(Path.GetTempPath(), "xping-cli-auth-tests", Guid.NewGuid().ToString("N"), ".xping");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, isTerminal: false);
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(new XpingHome(HomeDirectory));
        services.AddSingleton(new CliVersion(cliVersion));
        Services = services.BuildServiceProvider();
    }

    public FakeTimeProvider Time { get; }

    public FakeCloud Cloud { get; }

    public string HomeDirectory { get; }

    public ServiceProvider Services { get; }

    public DiscoveryClient Discovery => Services.GetRequiredService<DiscoveryClient>();

    public OAuthClient OAuth => Services.GetRequiredService<OAuthClient>();

    public Task<DiscoveryDocument> DiscoverAsync(bool useCache = false) =>
        Discovery.GetAsync(Cloud.CloudUrl, useCache, CancellationToken.None);

    /// <summary>
    /// Waits for <paramref name="task"/>, moving the fake clock forward in small steps so the
    /// retry waits it starts can elapse.
    /// </summary>
    /// <remarks>
    /// A single large step would be lost if it landed before the client started its wait, so the
    /// clock moves 100 ms at a time until the task is done. Gaps measured on the fake clock are
    /// therefore exact to within one step plus a request's round trip.
    /// </remarks>
    public async Task<T> DriveAsync<T>(Task<T> task)
    {
        await DriveAsync((Task)task).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    public async Task DriveAsync(Task task)
    {
        TimeSpan limit = TimeSpan.FromMinutes(2);
        TimeSpan step = TimeSpan.FromMilliseconds(100);

        for (TimeSpan moved = TimeSpan.Zero; !task.IsCompleted && moved < limit; moved += step)
        {
            await Task.WhenAny(task, Task.Delay(5)).ConfigureAwait(false);
            if (!task.IsCompleted)
                Time.Advance(step);
        }

        await task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync().ConfigureAwait(false);
        await Cloud.DisposeAsync().ConfigureAwait(false);

        string scratch = Path.GetDirectoryName(HomeDirectory)!;
        if (Directory.Exists(scratch))
            Directory.Delete(scratch, recursive: true);
    }
}
