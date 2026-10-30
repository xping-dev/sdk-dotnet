/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The fixtures' own derivations, which the goldens trust to say what the pipeline would.
/// </summary>
public sealed class ReportFixturesTests
{
    /// <summary>
    /// Two rows that started at the same instant are replayed in the order RUNS prints them, so a
    /// mode's first sighting is the row the timeline shows it in (failure-mode spec Amendment 4C).
    /// </summary>
    [Fact]
    public void ReplayedRowsThatStartedTogetherKeepTheirOrder()
    {
        var at = new DateTime(2026, 8, 20, 14, 2, 0, DateTimeKind.Utc);

        TestSessionDto[] sessions =
        [
            Row(at, TestOutcome.Failed),
            Row(at, TestOutcome.Passed),
            Row(at.AddMinutes(-5), TestOutcome.Passed)
        ];

        FailureModeDto mode = Assert.Single(ReportFixtures.HistoryModes(
            "Tied", sessions, (_, _) => new ReportFixtures.RecordedFailure("System.Exception", "boom", null)));

        Assert.Equal(0, mode.FirstSeenSessionsAgo);
        Assert.True(mode.FirstSeenInLatestSession);
    }

    private static TestSessionDto Row(DateTime at, TestOutcome outcome) =>
        new(Guid.Empty, at, "c670fc3a91", Appeared: true, Partial: false, outcome, 1, 10, [new TestAttemptDto(1, outcome, 10)]);
}
