/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
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
}
