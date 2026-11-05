/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Cloud;

namespace Xping.Cli.Tests.Cloud;

public sealed class CloudBudgetTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void TheWholeCloudLayerGetsFourSecondsAndTheCredentialOne()
    {
        // Contract §7.3: CloudTotalBudgetMs and CredentialBudgetMs.
        Assert.Equal(TimeSpan.FromSeconds(4), CloudBudget.Total);
        Assert.Equal(TimeSpan.FromSeconds(1), CloudBudget.Credential);
        Assert.Equal(TimeSpan.FromSeconds(3), CloudHttp.AttemptTimeout);
        Assert.Equal(CloudBudget.Total, CloudHttp.TotalTimeout);
    }

    [Fact]
    public void TheCredentialSliceRunsOutAfterOneSecondAndTheTotalStaysOpen()
    {
        using var budget = new CloudBudget(_time, CancellationToken.None);

        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(budget.CredentialToken.IsCancellationRequested);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(budget.CredentialToken.IsCancellationRequested);
        Assert.True(budget.CredentialExpired);
        Assert.False(budget.Token.IsCancellationRequested);
    }

    [Fact]
    public void TheTotalRunsOutAfterFourSecondsAndTakesTheSliceWithIt()
    {
        using var budget = new CloudBudget(_time, CancellationToken.None);

        _time.Advance(TimeSpan.FromMilliseconds(3999));
        Assert.False(budget.Token.IsCancellationRequested);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(budget.Token.IsCancellationRequested);
        Assert.True(budget.CredentialToken.IsCancellationRequested);
    }

    [Fact]
    public void TheCallersCancellationReachesBothTokensWithoutReadingAsTheSlice()
    {
        using var caller = new CancellationTokenSource();
        using var budget = new CloudBudget(_time, caller.Token);

        caller.Cancel();

        Assert.True(budget.Token.IsCancellationRequested);
        Assert.True(budget.CredentialToken.IsCancellationRequested);
        Assert.False(budget.CredentialExpired);
    }

    [Fact]
    public void RemainingCountsDownOnTheInjectedClockAndStopsAtZero()
    {
        using var budget = new CloudBudget(_time, CancellationToken.None);

        _time.Advance(TimeSpan.FromMilliseconds(2500));
        Assert.Equal(TimeSpan.FromMilliseconds(2500), budget.Elapsed);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), budget.Remaining);

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.Zero, budget.Remaining);
    }

    [Fact]
    public void HasAtLeastIsTrueUpToExactlyWhatIsLeft()
    {
        using var budget = new CloudBudget(_time, CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.True(budget.HasAtLeast(TimeSpan.FromSeconds(1)));

        // The retry and the --test run history each need a second (§3.2, §3.3).
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(budget.HasAtLeast(CloudResilience.MinimumForRetry));
    }
}
