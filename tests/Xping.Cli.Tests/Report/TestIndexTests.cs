/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class TestIndexTests
{
    private const string Subject = "Alpha";
    private const string SubjectFingerprint = "fp-Alpha";

    /// <summary>
    /// Builds a window in which <see cref="Subject"/> ran in the newest <paramref name="presentIn"/>
    /// sessions, each run taking <paramref name="attempts"/> attempts to finish.
    /// </summary>
    /// <remarks>
    /// A second test runs in every session so that the window never shrinks to the sessions the
    /// subject appeared in — the denominator has to stay the whole window for the ratio to mean
    /// anything.
    /// </remarks>
    private static AnalysisWindow Window(int total, int presentIn, int attempts)
    {
        var sessions = new List<TestSession>();

        for (int ordinal = 0; ordinal < total; ordinal++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            // Highest ordinal is newest, so the subject's runs are the most recent sessions.
            if (ordinal >= total - presentIn)
            {
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    executions.Add(TestSessionFactory.Execution(
                        Subject,
                        outcome: attempt == attempts ? TestOutcome.Passed : TestOutcome.Failed,
                        attempt: attempt,
                        passedOnRetry: attempt == attempts && attempts > 1,
                        maxRetries: attempts - 1,
                        errorMessage: attempt == attempts ? null : "boom"));
                }
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions));
        }

        return TestSessionFactory.Window([.. sessions]);
    }

    [Fact]
    public void RunFrequencyCountsSessionsRatherThanAttempts()
    {
        // The bug this pins: three attempts in half the sessions is thirty executions over twenty
        // sessions, which read as a test that runs on every build.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 3));

        Assert.Equal(30, index.ExecutionsOf(SubjectFingerprint).Count);
        Assert.Equal(0.50, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void RunFrequencyIsOneForATestThatRunsInEverySession()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 20, attempts: 1));

        Assert.Equal(1.0, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void RunFrequencyIsStillOneWhenEverySessionRetries()
    {
        // Retries must not be what pushes the value to one: a test that genuinely runs everywhere
        // has to be indistinguishable from itself with retries switched on.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 20, attempts: 3));

        Assert.Equal(1.0, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void ATestThatRetriesFourTimesInAQuarterOfSessionsDoesNotOutrankOneThatRunsEverywhere()
    {
        // The ranking consequence, stated as the issue states it: before the fix both read 1.0.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 5, attempts: 4));

        Assert.Equal(0.25, index.RunFrequencyOf(SubjectFingerprint));
        Assert.Equal(1.0, index.RunFrequencyOf("fp-Stable"));
    }

    [Fact]
    public void RunFrequencyIsZeroForAFingerprintTheWindowNeverSaw()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 1));

        Assert.Equal(0, index.RunFrequencyOf("fp-NeverRan"));
    }

    [Fact]
    public void RunFrequencyIsZeroOverAnEmptyWindow()
    {
        AnalysisWindow empty = AnalysisWindow.Create(
            [], TestSessionFactory.Epoch, TestSessionFactory.Epoch, WindowResolution.Default, null);

        Assert.Equal(0, TestIndex.Build(empty).RunFrequencyOf(SubjectFingerprint));
    }
}
