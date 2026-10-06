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
        services.AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, isTerminal: false);
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
    /// Waits for <paramref name="task"/>, moving the fake clock past each wait it starts.
    /// </summary>
    /// <remarks>
    /// The clock moves only when the code under test has started a timer, and by exactly that
    /// timer's due time, so gaps measured on the fake clock are exact whatever the machine's speed.
    /// A task that neither finishes nor starts a timer fails the test rather than hang it.
    /// </remarks>
    public async Task<T> DriveAsync<T>(Task<T> task)
    {
        await DriveAsync((Task)task).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    public async Task DriveAsync(Task task)
    {
        TimeSpan patience = TimeSpan.FromSeconds(30);

        while (!task.IsCompleted)
        {
            // Waiting does not take the timer, so a wait abandoned because the task finished
            // first leaves it for the next call.
            Task<bool> timerCreated = Time.CreatedTimers.WaitToReadAsync().AsTask();
            await Task.WhenAny(task, timerCreated).WaitAsync(patience).ConfigureAwait(false);

            if (Time.CreatedTimers.TryRead(out TimeSpan dueTime))
                Time.Advance(dueTime);
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
