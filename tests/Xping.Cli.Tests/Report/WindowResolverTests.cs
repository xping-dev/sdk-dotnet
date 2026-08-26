/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Report;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Tests.Report;

public sealed class WindowResolverTests
{
    // Comfortably after every fixture session, so the default age bound includes them unless a test
    // deliberately moves it.
    private static readonly DateTime Now = TestSessionFactory.Epoch.AddHours(1);

    private static WindowResolver CreateResolver(DateTime? now = null) =>
        new(new FakeTimeProvider(new DateTimeOffset(now ?? Now, TimeSpan.Zero)));

    private static TestSession[] Sessions(int count) =>
        [.. Enumerable.Range(0, count).Select(i => TestSessionFactory.Session(i, "Alpha", "Beta"))];

    [Fact]
    public void NoStoreIsReportedRatherThanTreatedAsEmpty()
    {
        var source = new FakeSessionSource(Sessions(3)) { IsAvailable = false };

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Null(result.Window);
        Assert.Equal(WindowFailure.NoStore, result.Failure);
    }

    [Fact]
    public void EmptyStoreIsDistinctFromNoStore()
    {
        var source = new FakeSessionSource();

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Null(result.Window);
        Assert.Equal(WindowFailure.EmptyStore, result.Failure);
    }

    [Fact]
    public void DefaultWindowCapsAtTheSessionLimit()
    {
        var source = new FakeSessionSource(Sessions(40));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(
            LocalAnalysisConstants.DefaultWindowSessions, result.Window!.SessionCount);
        Assert.Equal(WindowResolution.Default, result.Window.Resolution);
    }

    [Fact]
    public void DefaultWindowPrefersTheAgeBoundWhenItYieldsFewer()
    {
        var source = new FakeSessionSource(Sessions(10));

        // Far enough in the future that only the newest few sessions fall inside the age bound.
        DateTime now = TestSessionFactory.Epoch
            .AddDays(LocalAnalysisConstants.DefaultWindowDays)
            .AddMinutes(7);

        WindowResult result = CreateResolver(now).Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(3, result.Window!.SessionCount);
    }

    [Fact]
    public void DefaultWindowFallsBackToOldHistoryRatherThanReportingNothing()
    {
        var source = new FakeSessionSource(Sessions(6));

        // Every session is now well outside the age bound. Telling a developer returning from leave
        // that they have no history would be wrong: they have old history, and the window says so.
        DateTime now = TestSessionFactory.Epoch.AddDays(365);

        WindowResult result = CreateResolver(now).Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(6, result.Window!.SessionCount);
    }

    [Fact]
    public void RunsTakesTheMostRecentSessions()
    {
        var source = new FakeSessionSource(Sessions(10));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(4, null, null));

        Assert.Equal(4, result.Window!.SessionCount);
        Assert.Equal(WindowResolution.Runs, result.Window.Resolution);
        Assert.Equal("4", result.Window.ResolutionArgument);
        Assert.Equal(TestSessionFactory.SessionIdFor(9), result.Window.Sessions[0].SessionId);
    }

    [Fact]
    public void RequestingMoreRunsThanExistYieldsWhatThereIs()
    {
        var source = new FakeSessionSource(Sessions(3));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(50, null, null));

        Assert.Equal(3, result.Window!.SessionCount);
    }

    [Fact]
    public void ASingleSessionResolvesWithAnEmptyBaseline()
    {
        var source = new FakeSessionSource(Sessions(1));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(1, result.Window!.SessionCount);
        Assert.Single(result.Window.CurrentSlice);
        Assert.Empty(result.Window.BaselineSlice);
        Assert.False(result.Window.MeetsReportingFloor);
    }

    [Fact]
    public void SessionsAreOrderedNewestFirst()
    {
        var source = new FakeSessionSource(Sessions(5));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        DateTime[] starts = [.. result.Window!.Sessions.Select(s => s.StartedAt)];
        Assert.Equal(starts.OrderByDescending(s => s), starts);
    }

    [Theory]
    [InlineData(7, 1)]
    [InlineData(8, 3)]
    [InlineData(20, 3)]
    public void CurrentSliceNarrowsInSmallWindows(int sessionCount, int expectedSliceSize)
    {
        var source = new FakeSessionSource(Sessions(sessionCount));

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(expectedSliceSize, result.Window!.CurrentSliceSize);
        Assert.Equal(sessionCount - expectedSliceSize, result.Window.BaselineSlice.Count);
    }

    [Fact]
    public void SinceShaAnchorsOnTheOldestMatchingSession()
    {
        // The same commit tested three times: all three runs belong in the window, so anchoring on
        // the newest match would silently discard two of them.
        TestSession[] sessions =
        [
            TestSessionFactory.Session(0, [TestSessionFactory.Execution("Alpha")], sha: "aaaa1111"),
            TestSessionFactory.Session(1, [TestSessionFactory.Execution("Alpha")], sha: "bbbb2222"),
            TestSessionFactory.Session(2, [TestSessionFactory.Execution("Alpha")], sha: "bbbb2222"),
            TestSessionFactory.Session(3, [TestSessionFactory.Execution("Alpha")], sha: "bbbb2222")
        ];

        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, "bbbb2222", null));

        Assert.Equal(3, result.Window!.SessionCount);
        Assert.Equal(WindowResolution.SinceSha, result.Window.Resolution);
    }

    [Fact]
    public void SinceShaAcceptsAnAbbreviation()
    {
        TestSession[] sessions =
        [
            TestSessionFactory.Session(0, [TestSessionFactory.Execution("Alpha")], sha: "aaaa1111"),
            TestSessionFactory.Session(1, [TestSessionFactory.Execution("Alpha")], sha: "bbbb2222")
        ];

        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, "BBBB", null));

        Assert.Equal(1, result.Window!.SessionCount);
    }

    [Fact]
    public void AnUnknownShaIsItsOwnFailure()
    {
        TestSession[] sessions =
            [TestSessionFactory.Session(0, [TestSessionFactory.Execution("Alpha")], sha: "aaaa1111")];

        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, "deadbeef", null));

        Assert.Null(result.Window);
        Assert.Equal(WindowFailure.ShaNotFound, result.Failure);
        Assert.Contains("deadbeef", result.FailureDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void SinceDateSelectsFromTheBoundary()
    {
        var source = new FakeSessionSource(Sessions(10));

        // Sessions are one minute apart from the epoch, so this excludes the oldest six.
        string boundary = TestSessionFactory.Epoch
            .AddMinutes(6)
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        WindowResult result = CreateResolver()
            .Resolve(source, new WindowRequest(null, boundary, null));

        Assert.Equal(4, result.Window!.SessionCount);
        Assert.Equal(WindowResolution.SinceDate, result.Window.Resolution);
    }

    [Fact]
    public void SinceRejectsAValueThatIsNeitherDateNorCommit()
    {
        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(Sessions(3)), new WindowRequest(null, "last-tuesday", null));

        Assert.Null(result.Window);
        Assert.Equal(WindowFailure.SinceNotUnderstood, result.Failure);
    }

    [Fact]
    public void UnreadableFilesAreCarriedOntoTheResult()
    {
        var source = new FakeSessionSource(Sessions(3)) { UnreadableCount = 2 };

        WindowResult result = CreateResolver().Resolve(source, new WindowRequest(null, null, null));

        Assert.Equal(2, result.UnreadableSessions);
    }

    [Fact]
    public void AssemblyScopingExcludesOtherSuites()
    {
        TestSession[] sessions =
        [
            TestSessionFactory.Session(0, [TestSessionFactory.Execution("Alpha", assembly: "Alpha.Tests")]),
            TestSessionFactory.Session(1, [TestSessionFactory.Execution("Beta", assembly: "Beta.Tests")]),
            TestSessionFactory.Session(2, [TestSessionFactory.Execution("Alpha", assembly: "Alpha.Tests")])
        ];

        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, null, "Alpha.Tests"));

        Assert.Equal(2, result.Window!.SessionCount);
    }

    [Fact]
    public void ARunSharedBySeveralSuitesCountsOnceForEachOfThem()
    {
        // One test host, two test projects, one session. Each suite ran twice and its window has to
        // say two — not one because the run was labelled elsewhere, and not four because both
        // suites' executions were pooled into it.
        TestSession[] sessions =
        [
            Mixed(0),
            Mixed(1)
        ];

        WindowResult alpha = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, null, "Alpha.Tests"));
        WindowResult beta = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, null, "Beta.Tests"));

        Assert.Equal(2, alpha.Window!.SessionCount);
        Assert.Equal(2, beta.Window!.SessionCount);
    }

    [Fact]
    public void AScopedWindowCarriesOnlyThatSuitesExecutions()
    {
        TestSession[] sessions = [Mixed(0)];

        WindowResult result = CreateResolver()
            .Resolve(new FakeSessionSource(sessions), new WindowRequest(null, null, "Alpha.Tests"));

        TestSession scoped = Assert.Single(result.Window!.Sessions);

        Assert.All(
            scoped.Executions, e => Assert.Equal("Alpha.Tests", e.Identity.Assembly));
    }

    /// <summary>
    /// Builds a session recording both suites, as one shared test host produces.
    /// </summary>
    private static TestSession Mixed(int ordinal) =>
        TestSessionFactory.Session(
            ordinal,
            [
                TestSessionFactory.Execution("Alpha", assembly: "Alpha.Tests"),
                TestSessionFactory.Execution("Beta", assembly: "Beta.Tests")
            ]);
}
