/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth.Http;

public sealed class CrossProcessLockTests : IDisposable
{
    private const string CloudUrl = "https://tests.invalid";

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "xping-cli-lock-tests", Guid.NewGuid().ToString("N"));
    private readonly TimerTrackingTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly XpingHome _home;
    private readonly CrossProcessLock _lock;

    public CrossProcessLockTests()
    {
        _home = new XpingHome(Path.Combine(_scratch, ".xping"));
        _lock = new CrossProcessLock(_home, _time, NullLogger<CrossProcessLock>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public void TheWaitIsShorterThanTheAttemptItRunsInside() =>
        Assert.True(CrossProcessLock.Patience < CloudHttp.AttemptTimeout);

    [Fact]
    public async Task TheLockFileIsPrivateAndKeyedByCloudUrl()
    {
        using (await _lock.AcquireAsync(CloudUrl, CancellationToken.None))
            Assert.True(File.Exists(_home.LockFile(CloudUrl)));

        Assert.EndsWith(".lock", _home.LockFile(CloudUrl), StringComparison.Ordinal);
        Assert.NotEqual(_home.LockFile(CloudUrl), _home.LockFile("https://other.tests.invalid"));

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_home.LockFile(CloudUrl)));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(_home.LockFile(CloudUrl))!));
        }
    }

    [Fact]
    public async Task ASecondHolderWaitsUntilTheFirstReleases()
    {
        IDisposable first = await _lock.AcquireAsync(CloudUrl, CancellationToken.None);
        Task<IDisposable> second = _lock.AcquireAsync(CloudUrl, CancellationToken.None);

        TimeSpan retry = await _time.CreatedTimers.ReadAsync();
        Assert.Equal(CrossProcessLock.RetryInterval, retry);
        Assert.False(second.IsCompleted);

        first.Dispose();
        _time.Advance(retry);

        using IDisposable held = await second.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(File.Exists(_home.LockFile(CloudUrl)));
    }

    [Fact]
    public async Task AfterFiveSecondsTheRefreshProceedsWithoutTheLock()
    {
        using IDisposable first = await _lock.AcquireAsync(CloudUrl, CancellationToken.None);

        using IDisposable second = await ClockDriver.DriveAsync(_time, _lock.AcquireAsync(CloudUrl, CancellationToken.None));

        Assert.True(_time.GetUtcNow() >= new DateTimeOffset(2026, 9, 29, 10, 0, 5, TimeSpan.Zero));
        Assert.IsNotType<FileStream>(second);
    }
}
