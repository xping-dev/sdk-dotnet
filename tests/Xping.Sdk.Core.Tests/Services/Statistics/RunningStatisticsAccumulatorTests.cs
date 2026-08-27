/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.Statistics.Internals;

namespace Xping.Sdk.Core.Tests.Services.Statistics;

public sealed class RunningStatisticsAccumulatorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TestExecution BuildExecution(
        string name,
        TestOutcome outcome,
        TimeSpan duration = default)
    {
        return new TestExecutionBuilder()
            .WithTestName(name)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .Build();
    }

    /// <summary>
    /// Builds an execution carrying a real identity and attempt number, which is what the
    /// distinct-test counters key on. <paramref name="fingerprint"/> stands in for
    /// <c>TestIdentity.TestFingerprint</c>; two executions sharing one are two attempts of one test.
    /// </summary>
    private static TestExecution BuildAttempt(
        string fingerprint,
        TestOutcome outcome,
        int attemptNumber = 1,
        string assembly = "SampleApp.Tests",
        TimeSpan duration = default)
    {
        TestIdentity identity = new TestIdentityBuilder()
            .WithTestFingerprint(fingerprint)
            .WithFullyQualifiedName($"SampleApp.Tests.{fingerprint}")
            .WithAssembly(assembly)
            .Build();

        RetryMetadata retry = new RetryMetadataBuilder()
            .WithAttemptNumber(attemptNumber)
            .WithPassedOnRetry(attemptNumber > 1 && outcome == TestOutcome.Passed)
            .Build();

        return new TestExecutionBuilder()
            .WithIdentity(identity)
            .WithTestName(fingerprint)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithRetry(retry)
            .Build();
    }

    // ---------------------------------------------------------------------------
    // Record — guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_NullExecution_ThrowsArgumentNullException()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => accumulator.Record(null!));
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — empty state
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_WithNoRecordedExecutions_ReturnsAllZeros()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
        Assert.Equal(0, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
        Assert.Equal(0, snapshot.Skipped);
        Assert.Equal(0, snapshot.Inconclusive);
        Assert.Equal(0, snapshot.NotExecuted);
        Assert.Equal(0.0, snapshot.SuccessRate);
        Assert.Equal(0, snapshot.DistinctTests);
        Assert.Equal(0, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
        Assert.Equal(0, snapshot.FinalSkipped);
        Assert.Equal(0, snapshot.FinalInconclusive);
        Assert.Equal(0, snapshot.FinalNotExecuted);
        Assert.Equal(0.0, snapshot.FinalSuccessRate);
        Assert.Equal(0L, snapshot.TotalDurationMs);
        Assert.Equal(0L, snapshot.WallClockDurationMs);
        Assert.Equal(0L, snapshot.AverageDurationMs);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
        Assert.Null(snapshot.SlowestTestName);
    }

    // ---------------------------------------------------------------------------
    // Record — outcome counter increments
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_PassedExecution_IncrementsPassedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Passed);
    }

    [Fact]
    public void Record_FailedExecution_IncrementsFailedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Failed);
    }

    [Fact]
    public void Record_SkippedExecution_IncrementsSkippedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Skipped));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Skipped);
    }

    [Fact]
    public void Record_InconclusiveExecution_IncrementsInconclusiveAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Inconclusive));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Inconclusive);
    }

    [Fact]
    public void Record_NotExecutedExecution_IncrementsNotExecutedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.NotExecuted));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.NotExecuted);
    }

    [Fact]
    public void Record_MultipleOutcomes_AllCountersReflectActualValues()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T3", TestOutcome.Failed));
        accumulator.Record(BuildExecution("T4", TestOutcome.Skipped));
        accumulator.Record(BuildExecution("T5", TestOutcome.Inconclusive));
        accumulator.Record(BuildExecution("T6", TestOutcome.NotExecuted));
        accumulator.Record(BuildExecution("T7", TestOutcome.Timeout));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(7, snapshot.Total);
        Assert.Equal(2, snapshot.Passed);
        Assert.Equal(1, snapshot.Failed);
        Assert.Equal(1, snapshot.Skipped);
        Assert.Equal(1, snapshot.Inconclusive);
        Assert.Equal(1, snapshot.NotExecuted);
        Assert.Equal(1, snapshot.Timeout);
    }

    /// <summary>
    /// The counters must fail loudly rather than silently drop an outcome they do not know about.
    /// The cast is the only way to reach that arm — every declared member has a bucket — and that is
    /// the point: it stands in for the member someone adds later without updating this switch.
    /// </summary>
    [Fact]
    public void Record_UnknownOutcome_ThrowsRatherThanSilentlyMiscounting()
    {
        var accumulator = new RunningStatisticsAccumulator();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.Record(BuildExecution("T1", (TestOutcome)999)));
    }

    /// <summary>
    /// The rejection happens before any bookkeeping, so a rejected execution leaves no trace in
    /// either the per-execution counters or the distinct-test ones. That ordering is what makes a
    /// second guard on the distinct-test pass unnecessary — nothing can reach it.
    /// </summary>
    [Fact]
    public void Record_UnknownOutcome_RejectsBeforeRecordingAnything()
    {
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildAttempt("Subject", TestOutcome.Passed));

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.Record(BuildAttempt("Other", (TestOutcome)999)));

        Assert.Equal("execution", ex.ParamName);

        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Passed);
        Assert.Equal(1, snapshot.DistinctTests);
    }

    /// <summary>
    /// The report presents the per-outcome counters as a breakdown of <c>Total</c>, so an outcome
    /// that lands in no bucket reads as data loss. Recording one execution of every declared member
    /// keeps that invariant honest as members are added.
    /// </summary>
    [Fact]
    public void Record_EveryDeclaredOutcome_BucketsSumToTotal()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        int index = 0;
        foreach (TestOutcome outcome in Enum.GetValues<TestOutcome>())
        {
            accumulator.Record(BuildExecution($"T{index++}", outcome));
        }

        // Assert
        var snapshot = accumulator.GetSnapshot();
        int bucketed = snapshot.Passed + snapshot.Failed + snapshot.Skipped +
                       snapshot.Inconclusive + snapshot.NotExecuted + snapshot.Timeout;

        Assert.Equal(snapshot.Total, bucketed);

        int finalBucketed = snapshot.FinalPassed + snapshot.FinalFailed + snapshot.FinalSkipped +
                            snapshot.FinalInconclusive + snapshot.FinalNotExecuted + snapshot.FinalTimeout;

        Assert.Equal(snapshot.DistinctTests, finalBucketed);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — SuccessRate calculation
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_AllTestsPassed_SuccessRateIs1()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(1.0, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_NoTestsPassed_SuccessRateIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0.0, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_HalfTestsPassed_SuccessRateIsPoint5()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0.5, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_SuccessRateIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert — no division by zero
        Assert.Equal(0.0, snapshot.SuccessRate);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — duration calculations
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_SingleExecution_TotalDurationMatchesExecutionDuration()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        var duration = TimeSpan.FromMilliseconds(500);
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, duration));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(500L, snapshot.TotalDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_TotalDurationIsSumOfAllDurations()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(200)));
        accumulator.Record(BuildExecution("T3", TestOutcome.Passed, TimeSpan.FromMilliseconds(300)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(600L, snapshot.TotalDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_AverageDurationIsCorrect()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(300)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(200L, snapshot.AverageDurationMs);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_AverageDurationIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert — no division by zero
        Assert.Equal(0L, snapshot.AverageDurationMs);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — wall-clock duration
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_Parameterless_WallClockDurationMsIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0L, snapshot.WallClockDurationMs);
    }

    [Fact]
    public void GetSnapshot_WithPositiveElapsed_WallClockDurationMsMatchesElapsed()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot(TimeSpan.FromMilliseconds(1204));

        // Assert
        Assert.Equal(1204L, snapshot.WallClockDurationMs);
    }

    [Fact]
    public void GetSnapshot_WithZeroElapsed_WallClockDurationMsIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot(TimeSpan.Zero);

        // Assert
        Assert.Equal(0L, snapshot.WallClockDurationMs);
    }

    [Fact]
    public void GetSnapshot_WithNegativeElapsed_WallClockDurationMsIsClampedToZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act — simulates a backwards system clock jump
        var snapshot = accumulator.GetSnapshot(TimeSpan.FromMilliseconds(-500));

        // Assert
        Assert.Equal(0L, snapshot.WallClockDurationMs);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — slowest test tracking
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_SingleExecution_SlowestTestIsTheOnlyExecution()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("SlowTest", TestOutcome.Passed, TimeSpan.FromMilliseconds(750)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal("SlowTest", snapshot.SlowestTestName);
        Assert.Equal(750L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_SlowestTestHasLongestDuration()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("Fast", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("Slowest", TestOutcome.Passed, TimeSpan.FromMilliseconds(800)));
        accumulator.Record(BuildExecution("Medium", TestOutcome.Passed, TimeSpan.FromMilliseconds(400)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal("Slowest", snapshot.SlowestTestName);
        Assert.Equal(800L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_SlowestTestNameIsNull()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Null(snapshot.SlowestTestName);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_AllExecutionsHaveZeroDuration_SlowestDurationIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.Zero));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.Zero));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    // ---------------------------------------------------------------------------
    // Reset — resets all state
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_AfterRecordingExecutions_GetSnapshotReturnsAllZeros()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(500)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed, TimeSpan.FromMilliseconds(200)));

        // Act
        accumulator.Reset();
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
        Assert.Equal(0, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
        Assert.Equal(0.0, snapshot.SuccessRate);
        Assert.Equal(0, snapshot.DistinctTests);
        Assert.Equal(0, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
        Assert.Equal(0.0, snapshot.FinalSuccessRate);
        Assert.Equal(0L, snapshot.TotalDurationMs);
        Assert.Equal(0L, snapshot.WallClockDurationMs);
        Assert.Equal(0L, snapshot.AverageDurationMs);
        Assert.Null(snapshot.SlowestTestName);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void Reset_CanRecordNewExecutionsAfterReset()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));
        accumulator.Reset();

        // Act
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        var snapshot = accumulator.GetSnapshot();

        // Assert — only the post-reset execution should be reflected
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
    }

    [Fact]
    public void Reset_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var ex = Record.Exception(() => accumulator.Reset());
            Assert.Null(ex);
        }
    }

    // ---------------------------------------------------------------------------
    // Thread safety — concurrent Record calls produce consistent totals
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Record_ConcurrentCalls_TotalCountIsConsistent()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        const int parallelism = 8;
        const int recordsPerTask = 100;

        // Act — fire-and-forget tasks that all record executions concurrently
        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < recordsPerTask; i++)
                    accumulator.Record(BuildExecution("ConcurrentTest", TestOutcome.Passed));
            }));

        await Task.WhenAll(tasks);

        // Assert — every Record must be counted exactly once
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(parallelism * recordsPerTask, snapshot.Total);
        Assert.Equal(parallelism * recordsPerTask, snapshot.Passed);

        // Every execution named the same test, so they collapse to a single distinct test
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
    }

    [Fact]
    public async Task Record_ConcurrentAttemptsOfManyTests_DistinctTestCountIsConsistent()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        const int testCount = 50;
        const int attemptsPerTest = 4;

        // Act — every test records all of its attempts from its own task, concurrently
        var tasks = Enumerable.Range(0, testCount)
            .Select(testIndex => Task.Run(() =>
            {
                for (int attempt = 1; attempt <= attemptsPerTest; attempt++)
                {
                    accumulator.Record(BuildAttempt(
                        $"fingerprint-{testIndex}",
                        attempt == attemptsPerTest ? TestOutcome.Passed : TestOutcome.Failed,
                        attemptNumber: attempt));
                }
            }));

        await Task.WhenAll(tasks);

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(testCount * attemptsPerTest, snapshot.Total);
        Assert.Equal(testCount, snapshot.DistinctTests);
        Assert.Equal(testCount, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
        Assert.Equal(1.0, snapshot.FinalSuccessRate);
    }

    // ---------------------------------------------------------------------------
    // Distinct tests — final-attempt-per-test tallies
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_RetryThatPassesOnSecondAttempt_CountsOneDistinctTestAsFinalPassed()
    {
        // Arrange — the issue #132 reproduction, reduced to the one flaky test
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Failed, attemptNumber: 1));
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Passed, attemptNumber: 2));

        // Assert — the attempt-level view still reports both attempts
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(2, snapshot.Total);
        Assert.Equal(1, snapshot.Passed);
        Assert.Equal(1, snapshot.Failed);
        Assert.Equal(0.5, snapshot.SuccessRate);

        // Assert — the test-level view reports the green suite it actually was
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
        Assert.Equal(1.0, snapshot.FinalSuccessRate);
    }

    [Fact]
    public void Record_TestFailingOnEveryAttempt_CountsOneDistinctTestAsFinalFailed()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("broken", TestOutcome.Failed, attemptNumber: 1));
        accumulator.Record(BuildAttempt("broken", TestOutcome.Failed, attemptNumber: 2));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(0, snapshot.FinalPassed);
        Assert.Equal(1, snapshot.FinalFailed);
        Assert.Equal(0.0, snapshot.FinalSuccessRate);
    }

    [Fact]
    public void Record_ExecutionsWithoutRetryMetadata_CountEachFingerprintOnce()
    {
        // Arrange — Retry is nullable on builder-produced executions; the attempt number defaults to 1
        var accumulator = new RunningStatisticsAccumulator();

        TestExecution execution = new TestExecutionBuilder()
            .WithIdentity(new TestIdentityBuilder().WithTestFingerprint("no-retry-metadata").Build())
            .WithTestName("NoRetryMetadata")
            .WithOutcome(TestOutcome.Passed)
            .WithRetry(null)
            .Build();

        // Act
        accumulator.Record(execution);

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
    }

    [Fact]
    public void Record_EqualAttemptNumbers_LastRecordedOutcomeWins()
    {
        // Arrange — a detector that cannot infer the attempt number reports 1 for every attempt
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("undetected-retry", TestOutcome.Failed, attemptNumber: 1));
        accumulator.Record(BuildAttempt("undetected-retry", TestOutcome.Passed, attemptNumber: 1));

        // Assert — the later execution decides, so the recovered test still reads as passed
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
    }

    [Fact]
    public void Record_LowerAttemptRecordedAfterHigher_KeepsHighestAttemptOutcome()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act — attempt 2 arrives before attempt 1
        accumulator.Record(BuildAttempt("out-of-order", TestOutcome.Passed, attemptNumber: 2));
        accumulator.Record(BuildAttempt("out-of-order", TestOutcome.Failed, attemptNumber: 1));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
    }

    [Fact]
    public void Record_SameFingerprintInDifferentAssemblies_CountsTwoDistinctTests()
    {
        // Arrange — TestFingerprint hashes the name and parameters only, so it does not separate assemblies
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("shared", TestOutcome.Passed, assembly: "First.Tests"));
        accumulator.Record(BuildAttempt("shared", TestOutcome.Failed, assembly: "Second.Tests"));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(2, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(1, snapshot.FinalFailed);
    }

    [Fact]
    public void Record_ExecutionsWithoutIdentity_FallBackToTestNameForDistinctness()
    {
        // Arrange — the default TestIdentity carries an empty fingerprint and fully qualified name
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(3, snapshot.Total);
        Assert.Equal(2, snapshot.DistinctTests);
        Assert.Equal(2, snapshot.FinalPassed);
        Assert.Equal(0, snapshot.FinalFailed);
    }

    [Fact]
    public void Record_ExecutionsWithNoIdentifierAtAll_CountEachAsItsOwnDistinctTest()
    {
        // Arrange — nothing to group on, so merging would be a worse answer than not grouping
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(new TestExecutionBuilder().WithOutcome(TestOutcome.Passed).Build());
        accumulator.Record(new TestExecutionBuilder().WithOutcome(TestOutcome.Failed).Build());

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(2, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(1, snapshot.FinalFailed);
    }

    [Fact]
    public void GetSnapshot_WithEveryOutcome_FinalBucketsSumToDistinctTests()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildAttempt("passed", TestOutcome.Passed));
        accumulator.Record(BuildAttempt("failed", TestOutcome.Failed));
        accumulator.Record(BuildAttempt("skipped", TestOutcome.Skipped));
        accumulator.Record(BuildAttempt("inconclusive", TestOutcome.Inconclusive));
        accumulator.Record(BuildAttempt("notExecuted", TestOutcome.NotExecuted));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(5, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(1, snapshot.FinalFailed);
        Assert.Equal(1, snapshot.FinalSkipped);
        Assert.Equal(1, snapshot.FinalInconclusive);
        Assert.Equal(1, snapshot.FinalNotExecuted);
        Assert.Equal(
            snapshot.DistinctTests,
            snapshot.FinalPassed + snapshot.FinalFailed + snapshot.FinalSkipped +
            snapshot.FinalInconclusive + snapshot.FinalNotExecuted);
        Assert.Equal(0.2, snapshot.FinalSuccessRate, precision: 5);
    }

    [Fact]
    public void Reset_AfterRecordingRetries_ClearsDistinctTestState()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Failed, attemptNumber: 1));
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Passed, attemptNumber: 2));
        accumulator.Reset();

        // Act — a test that failed before the reset must not resurface as a distinct test
        accumulator.Record(BuildAttempt("other", TestOutcome.Passed));
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(1, snapshot.DistinctTests);
        Assert.Equal(1, snapshot.FinalPassed);
        Assert.Equal(1.0, snapshot.FinalSuccessRate);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshotByAssembly — the same reading, attributed
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshotByAssembly_WithNoRecordedExecutions_IsEmptyRatherThanNull()
    {
        var accumulator = new RunningStatisticsAccumulator();

        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.NotNull(breakdown);
        Assert.Empty(breakdown);
    }

    [Fact]
    public void GetSnapshotByAssembly_TwoAssemblies_ReportsEachOnItsOwnExecutions()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("A1", TestOutcome.Passed, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("A2", TestOutcome.Failed, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("B1", TestOutcome.Passed, assembly: "Billing.Tests"));

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(2, breakdown.Count);
        Assert.Equal(2, breakdown["Api.Tests"].Total);
        Assert.Equal(1, breakdown["Api.Tests"].Passed);
        Assert.Equal(1, breakdown["Api.Tests"].Failed);
        Assert.Equal(1, breakdown["Billing.Tests"].Total);
        Assert.Equal(1, breakdown["Billing.Tests"].Passed);
        Assert.Equal(0, breakdown["Billing.Tests"].Failed);
    }

    /// <summary>
    /// The breakdown exists to be attributable, which is only worth anything if it also adds back up
    /// to the host-wide reading it decomposes.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_EveryOutcome_EntriesSumToTheHostWideSnapshot()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act — every declared outcome, spread across two assemblies
        int index = 0;
        foreach (TestOutcome outcome in Enum.GetValues<TestOutcome>())
        {
            string assembly = index % 2 == 0 ? "Api.Tests" : "Billing.Tests";
            accumulator.Record(BuildAttempt($"T{index++}", outcome, assembly: assembly));
        }

        // Assert
        var snapshot = accumulator.GetSnapshot();
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(snapshot.Total, breakdown.Values.Sum(a => a.Total));
        Assert.Equal(snapshot.Passed, breakdown.Values.Sum(a => a.Passed));
        Assert.Equal(snapshot.Failed, breakdown.Values.Sum(a => a.Failed));
        Assert.Equal(snapshot.Skipped, breakdown.Values.Sum(a => a.Skipped));
        Assert.Equal(snapshot.Inconclusive, breakdown.Values.Sum(a => a.Inconclusive));
        Assert.Equal(snapshot.NotExecuted, breakdown.Values.Sum(a => a.NotExecuted));
        Assert.Equal(snapshot.Timeout, breakdown.Values.Sum(a => a.Timeout));
        Assert.Equal(snapshot.DistinctTests, breakdown.Values.Sum(a => a.DistinctTests));
        Assert.Equal(snapshot.TotalDurationMs, breakdown.Values.Sum(a => a.TotalDurationMs));

        // And each entry's own buckets remain a breakdown of its own Total
        foreach (AssemblyStatistics entry in breakdown.Values)
        {
            Assert.Equal(
                entry.Total,
                entry.Passed + entry.Failed + entry.Skipped +
                entry.Inconclusive + entry.NotExecuted + entry.Timeout);
        }
    }

    [Fact]
    public void GetSnapshotByAssembly_RetryInOneAssembly_ScopesTheDistinctTestCountersToIt()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act — Api retried a flaky test to green; Billing failed outright
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Failed, attemptNumber: 1, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("flaky", TestOutcome.Passed, attemptNumber: 2, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("broken", TestOutcome.Failed, assembly: "Billing.Tests"));

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        AssemblyStatistics api = breakdown["Api.Tests"];
        Assert.Equal(2, api.Total);
        Assert.Equal(1, api.DistinctTests);
        Assert.Equal(1, api.FinalPassed);
        Assert.Equal(0, api.FinalFailed);
        Assert.Equal(1.0, api.FinalSuccessRate);

        AssemblyStatistics billing = breakdown["Billing.Tests"];
        Assert.Equal(1, billing.Total);
        Assert.Equal(1, billing.DistinctTests);
        Assert.Equal(0, billing.FinalPassed);
        Assert.Equal(1, billing.FinalFailed);
        Assert.Equal(0.0, billing.FinalSuccessRate);
    }

    /// <summary>
    /// TestFingerprint hashes the fully qualified name and parameters only, so two assemblies can
    /// present the same one. Each must still report the test as its own.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_SameFingerprintInTwoAssemblies_CountsOneDistinctTestInEach()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("Shared", TestOutcome.Passed, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("Shared", TestOutcome.Passed, assembly: "Billing.Tests"));

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(1, breakdown["Api.Tests"].DistinctTests);
        Assert.Equal(1, breakdown["Billing.Tests"].DistinctTests);
        Assert.Equal(2, accumulator.GetSnapshot().DistinctTests);
    }

    /// <summary>
    /// The ratios are the reason this type exists: a rate copied from the host-wide statistics would
    /// describe every assembly but the one it is filed under.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_DerivedRatios_ComeFromThatAssemblysOwnCounters()
    {
        // Arrange — Api is all green, Billing all red, so the host-wide rate matches neither
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("A1", TestOutcome.Passed, assembly: "Api.Tests",
            duration: TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildAttempt("A2", TestOutcome.Passed, assembly: "Api.Tests",
            duration: TimeSpan.FromMilliseconds(300)));
        accumulator.Record(BuildAttempt("B1", TestOutcome.Failed, assembly: "Billing.Tests",
            duration: TimeSpan.FromMilliseconds(1000)));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(2.0 / 3.0, snapshot.SuccessRate, precision: 5);

        Assert.Equal(1.0, breakdown["Api.Tests"].SuccessRate);
        Assert.Equal(1.0, breakdown["Api.Tests"].FinalSuccessRate);
        Assert.Equal(200L, breakdown["Api.Tests"].AverageDurationMs);

        Assert.Equal(0.0, breakdown["Billing.Tests"].SuccessRate);
        Assert.Equal(0.0, breakdown["Billing.Tests"].FinalSuccessRate);
        Assert.Equal(1000L, breakdown["Billing.Tests"].AverageDurationMs);
    }

    [Fact]
    public void GetSnapshotByAssembly_SlowestTest_IsTheAssemblysOwnNotTheHosts()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("Quick", TestOutcome.Passed, assembly: "Api.Tests",
            duration: TimeSpan.FromMilliseconds(10)));
        accumulator.Record(BuildAttempt("Slower", TestOutcome.Passed, assembly: "Api.Tests",
            duration: TimeSpan.FromMilliseconds(50)));
        accumulator.Record(BuildAttempt("Slowest", TestOutcome.Passed, assembly: "Billing.Tests",
            duration: TimeSpan.FromMilliseconds(900)));

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal("Slowest", accumulator.GetSnapshot().SlowestTestName);

        Assert.Equal("Slower", breakdown["Api.Tests"].SlowestTestName);
        Assert.Equal(50L, breakdown["Api.Tests"].SlowestTestDurationMs);
        Assert.Equal("Slowest", breakdown["Billing.Tests"].SlowestTestName);
        Assert.Equal(900L, breakdown["Billing.Tests"].SlowestTestDurationMs);
    }

    /// <summary>
    /// An execution recorded before identity generation completed carries no assembly. It is real and
    /// must still be counted, but filing it under an empty key would invent an assembly that never
    /// existed — so the host-wide reading is where it lands, and the entries can sum to less than it.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_ExecutionNamingNoAssembly_CreatesNoEntryButStillCountsHostWide()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildAttempt("Attributed", TestOutcome.Passed, assembly: "Api.Tests"));
        accumulator.Record(BuildExecution("Unattributed", TestOutcome.Passed));

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(["Api.Tests"], breakdown.Keys);
        Assert.Equal(1, breakdown["Api.Tests"].Total);
        Assert.Equal(2, accumulator.GetSnapshot().Total);
    }

    /// <summary>
    /// Ordinal order, matching <c>SessionAssemblies.Of</c>, so two runs of the same solution serialize
    /// their assemblies identically however the host happened to interleave them.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_KeysAreInOrdinalOrder()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act — recorded in an order that is neither ordinal nor its reverse
        accumulator.Record(BuildAttempt("M1", TestOutcome.Passed, assembly: "Middle.Tests"));
        accumulator.Record(BuildAttempt("Z1", TestOutcome.Passed, assembly: "Zeta.Tests"));
        accumulator.Record(BuildAttempt("A1", TestOutcome.Passed, assembly: "Alpha.Tests"));

        // Assert
        Assert.Equal(
            ["Alpha.Tests", "Middle.Tests", "Zeta.Tests"],
            accumulator.GetSnapshotByAssembly().Keys);
    }

    /// <summary>
    /// The rejection happens before the assembly bucket is touched, so an execution that was never
    /// counted cannot conjure an entry for an assembly that has recorded nothing.
    /// </summary>
    [Fact]
    public void GetSnapshotByAssembly_UnknownOutcome_LeavesNoEntryBehind()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.Record(BuildAttempt("T1", (TestOutcome)999, assembly: "Api.Tests")));

        // Assert
        Assert.Empty(accumulator.GetSnapshotByAssembly());
    }

    [Fact]
    public void Reset_AfterRecordingAcrossAssemblies_ClearsTheBreakdown()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildAttempt("A1", TestOutcome.Passed, assembly: "Api.Tests"));
        accumulator.Record(BuildAttempt("B1", TestOutcome.Passed, assembly: "Billing.Tests"));
        accumulator.Reset();

        // Act
        accumulator.Record(BuildAttempt("C1", TestOutcome.Passed, assembly: "Catalog.Tests"));

        // Assert — an assembly recorded before the reset must not resurface
        Assert.Equal(["Catalog.Tests"], accumulator.GetSnapshotByAssembly().Keys);
    }

    [Fact]
    public async Task GetSnapshotByAssembly_ConcurrentRecordsAcrossAssemblies_EachEntryIsConsistent()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        string[] assemblies = ["Api.Tests", "Billing.Tests", "Catalog.Tests"];
        const int parallelism = 8;
        const int recordsPerTask = 100;

        // Act — every task records into every assembly, so the buckets are genuinely contended
        var tasks = Enumerable.Range(0, parallelism)
            .Select(task => Task.Run(() =>
            {
                for (int i = 0; i < recordsPerTask; i++)
                {
                    foreach (string assembly in assemblies)
                        accumulator.Record(BuildAttempt($"{assembly}-{task}-{i}", TestOutcome.Passed, assembly: assembly));
                }
            }));

        await Task.WhenAll(tasks);

        // Assert
        IReadOnlyDictionary<string, AssemblyStatistics> breakdown = accumulator.GetSnapshotByAssembly();

        Assert.Equal(assemblies.Length, breakdown.Count);

        foreach (string assembly in assemblies)
        {
            Assert.Equal(parallelism * recordsPerTask, breakdown[assembly].Total);
            Assert.Equal(parallelism * recordsPerTask, breakdown[assembly].Passed);
            Assert.Equal(parallelism * recordsPerTask, breakdown[assembly].DistinctTests);
        }

        Assert.Equal(
            accumulator.GetSnapshot().Total,
            breakdown.Values.Sum(a => a.Total));
    }
}
