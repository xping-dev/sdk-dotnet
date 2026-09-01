/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class EvidenceLevelResolverTests
{
    private const string Subject = "Subject";
    private const string Other = "Other";

    // ---------------------------------------------------------------------------------------
    // The bands
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "Low")]
    [InlineData(7, "Low")]
    [InlineData(8, "Moderate")]
    [InlineData(15, "Moderate")]
    [InlineData(16, "High")]
    public void SessionsAreBandedAtAndEitherSideOfEveryBoundary(int sessions, string expected)
    {
        Assert.Equal(expected, EvidenceLevelResolver.Resolve(sessions).ToString());
    }

    [Fact]
    public void TheBandsFitInsideTheDefaultWindow()
    {
        // The reason the local figures are not Cloud's 15 and 40. Both boundaries have to be
        // reachable in a window the CLI can actually resolve, or High is a level nothing ever gets
        // and the scale quietly loses its top.
        Assert.True(
            LocalAnalysisConstants.EvidenceHighSessions < LocalAnalysisConstants.DefaultWindowSessions,
            "High evidence must be reachable inside the default window");

        Assert.True(
            LocalAnalysisConstants.EvidenceModerateSessions <
            LocalAnalysisConstants.EvidenceHighSessions);
    }

    // ---------------------------------------------------------------------------------------
    // Counting the subject
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASingleTestIsCountedInSessionsRatherThanAttempts()
    {
        // Twenty executions from five sessions is five occasions' worth of evidence. Banding the
        // first number would call one retried week better evidenced than a clean fortnight.
        TestIndex index = Index(subjectSessions: 5, attempts: 4, otherSessions: 20);

        Assert.Equal(20, index.ExecutionsOf($"fp-{Subject}").Count);
        Assert.Equal(5, EvidenceLevelResolver.CountSessions(Single(Subject), index));
    }

    [Fact]
    public void AGroupIsMeasuredByItsBestEvidencedMember()
    {
        TestIndex index = Index(subjectSessions: 5, attempts: 1, otherSessions: 20);

        var group = new FindingSubject.Group(
            "g", [Reference(index, Subject), Reference(index, Other)]);

        Assert.Equal(20, EvidenceLevelResolver.CountSessions(group, index));
    }

    [Fact]
    public void ATestTheWindowNeverSawCountsNothing()
    {
        TestIndex index = Index(subjectSessions: 5, attempts: 1, otherSessions: 20);

        var absent = new FindingSubject.SingleTest(
            new TestReference("fp-Ghost", "MyApp.Tests.Ghost", "Ghost", null, null, "MyApp.Tests"));

        Assert.Equal(0, EvidenceLevelResolver.CountSessions(absent, index));
    }

    // ---------------------------------------------------------------------------------------
    // The floor
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(4, 20, false)]
    [InlineData(5, 20, true)]
    [InlineData(20, 4, false)]
    [InlineData(20, 5, true)]
    public void BothBoundsMustBeCleared(int subjectSessions, int windowSessions, bool expected)
    {
        Assert.Equal(expected, EvidenceLevelResolver.MeetsReportingFloor(subjectSessions, windowSessions));
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    private static FindingSubject.SingleTest Single(string name) =>
        new(new TestReference(
            $"fp-{name}", $"MyApp.Tests.SampleTests.{name}", name, "SampleTests.cs", 10, "MyApp.Tests"));

    private static TestReference Reference(TestIndex index, string name) =>
        index.ReferenceFor($"fp-{name}")!;

    /// <summary>
    /// Builds a window in which the subject retries its way to a large execution count over few
    /// sessions, while a second test runs once in every session.
    /// </summary>
    private static TestIndex Index(int subjectSessions, int attempts, int otherSessions)
    {
        var sessions = new List<TestSession>();

        for (int ordinal = 0; ordinal < otherSessions; ordinal++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution(Other) };

            // Newest sessions hold the subject, so it is present rather than merely padded in.
            if (ordinal >= otherSessions - subjectSessions)
            {
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    executions.Add(TestSessionFactory.Execution(
                        Subject,
                        attempt == attempts ? TestOutcome.Passed : TestOutcome.Failed,
                        attempt: attempt,
                        maxRetries: attempts - 1,
                        passedOnRetry: attempt == attempts && attempts > 1,
                        errorMessage: attempt == attempts ? null : "boom"));
                }
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions));
        }

        return TestIndex.Build(TestSessionFactory.Window([.. sessions]));
    }
}
