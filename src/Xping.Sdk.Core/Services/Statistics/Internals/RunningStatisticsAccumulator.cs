/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// Thread-safe, incrementally updated implementation of <see cref="IRunningStatisticsAccumulator"/>.
/// Uses <see cref="Interlocked"/> operations for all scalar counters, a dedicated lock for the
/// compound slowest-test state, and a <see cref="ConcurrentDictionary{TKey,TValue}"/> holding the
/// final attempt of each distinct test.
/// </summary>
internal sealed class RunningStatisticsAccumulator : IRunningStatisticsAccumulator, IWallClockAwareStatisticsAccumulator
{
    // Outcome counters — stored as long for Interlocked.Read/Add compatibility
    private long _total;
    private long _passed;
    private long _failed;
    private long _skipped;
    private long _inconclusive;
    private long _notExecuted;
    private long _timeout;
    private long _totalDurationTicks;

    // Final attempt per distinct test — the scalar counters above cannot express this, because a
    // retried test arrives as several executions and only its last attempt decides whether it passed.
    // The key carries the assembly because TestFingerprint hashes the fully qualified name and the
    // parameters only, so identical names in two assemblies would otherwise collide. A ValueTuple key
    // keeps the per-execution path free of the string concatenation a composite key would need.
    private readonly ConcurrentDictionary<(string Assembly, string Test), FinalAttempt> _finalByTest = new();

    // Slowest test — requires a lock because name + duration must update atomically
    private readonly object _slowestLock = new();
    private long _slowestDurationTicks;
    private string? _slowestTestName;

    /// <inheritdoc/>
    public void Record(TestExecution execution)
    {
        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        Interlocked.Increment(ref _total);

        switch (execution.Outcome)
        {
            case TestOutcome.Passed:
                Interlocked.Increment(ref _passed);
                break;
            case TestOutcome.Failed:
                Interlocked.Increment(ref _failed);
                break;
            case TestOutcome.Skipped:
                Interlocked.Increment(ref _skipped);
                break;
            case TestOutcome.Inconclusive:
                Interlocked.Increment(ref _inconclusive);
                break;
            case TestOutcome.NotExecuted:
                Interlocked.Increment(ref _notExecuted);
                break;
            case TestOutcome.Timeout:
                Interlocked.Increment(ref _timeout);
                break;
            default:
                // Every outcome must land in exactly one bucket, because the report presents the
                // buckets as a breakdown of Total. A member added without a case here would inflate
                // Total and balance nowhere, which reads as data loss rather than as a missing arm.
                throw new ArgumentOutOfRangeException(
                    nameof(execution),
                    execution.Outcome,
                    "Unhandled TestOutcome; add a counter for it.");
        }

        RecordFinalAttempt(execution);

        long ticks = execution.Duration.Ticks;
        Interlocked.Add(ref _totalDurationTicks, ticks);

        lock (_slowestLock)
        {
            if (ticks > _slowestDurationTicks)
            {
                _slowestDurationTicks = ticks;
                _slowestTestName = execution.TestName;
            }
        }
    }

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot() => GetSnapshot(TimeSpan.Zero);

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot(TimeSpan wallClockElapsed)
    {
        long total = Interlocked.Read(ref _total);
        long passed = Interlocked.Read(ref _passed);
        long failed = Interlocked.Read(ref _failed);
        long skipped = Interlocked.Read(ref _skipped);
        long inconclusive = Interlocked.Read(ref _inconclusive);
        long notExecuted = Interlocked.Read(ref _notExecuted);
        long timeout = Interlocked.Read(ref _timeout);
        long durationTicks = Interlocked.Read(ref _totalDurationTicks);

        FinalTally finalTally = TallyFinalAttempts();

        double successRate = total == 0 ? 0.0 : (double)passed / total;
        long totalMs = durationTicks / TimeSpan.TicksPerMillisecond;
        long averageMs = total == 0 ? 0L : totalMs / total;

        long slowestTicks;
        string? slowestName;
        lock (_slowestLock)
        {
            slowestTicks = _slowestDurationTicks;
            slowestName = _slowestTestName;
        }

        // Clamp to zero: a caller computing elapsed via DateTime.UtcNow - startedAt could pass a
        // negative value if the system clock jumps backwards.
        long wallClockMs = wallClockElapsed > TimeSpan.Zero
            ? wallClockElapsed.Ticks / TimeSpan.TicksPerMillisecond
            : 0L;

        return new QuickStatistics(
            total: (int)total,
            passed: (int)passed,
            failed: (int)failed,
            skipped: (int)skipped,
            inconclusive: (int)inconclusive,
            notExecuted: (int)notExecuted,
            timeout: (int)timeout,
            successRate: successRate,
            totalDurationMs: totalMs,
            wallClockDurationMs: wallClockMs,
            averageDurationMs: averageMs,
            slowestTestName: slowestName,
            slowestTestDurationMs: slowestTicks / TimeSpan.TicksPerMillisecond)
        {
            DistinctTests = finalTally.DistinctTests,
            FinalPassed = finalTally.Passed,
            FinalFailed = finalTally.Failed,
            FinalSkipped = finalTally.Skipped,
            FinalInconclusive = finalTally.Inconclusive,
            FinalNotExecuted = finalTally.NotExecuted,
            FinalTimeout = finalTally.Timeout,
            FinalSuccessRate = finalTally.DistinctTests == 0
                ? 0.0
                : (double)finalTally.Passed / finalTally.DistinctTests
        };
    }

    /// <inheritdoc/>
    public void Reset()
    {
        Interlocked.Exchange(ref _total, 0L);
        Interlocked.Exchange(ref _passed, 0L);
        Interlocked.Exchange(ref _failed, 0L);
        Interlocked.Exchange(ref _skipped, 0L);
        Interlocked.Exchange(ref _inconclusive, 0L);
        Interlocked.Exchange(ref _notExecuted, 0L);
        Interlocked.Exchange(ref _timeout, 0L);
        Interlocked.Exchange(ref _totalDurationTicks, 0L);

        _finalByTest.Clear();

        lock (_slowestLock)
        {
            _slowestDurationTicks = 0L;
            _slowestTestName = null;
        }
    }

    /// <summary>
    /// Keeps the highest-numbered attempt of the test this execution belongs to. On an equal attempt
    /// number the later-recorded execution wins: the per-framework detectors infer attempt numbers
    /// heuristically, and when that inference degrades to 1 for every attempt, taking the last one
    /// still reports a suite that recovered on retry as recovered.
    /// </summary>
    private void RecordFinalAttempt(TestExecution execution)
    {
        (string Assembly, string Test) key = (execution.Identity.Assembly, ResolveTestKey(execution));
        var attempt = new FinalAttempt(execution.Retry?.AttemptNumber ?? 1, execution.Outcome);

        _finalByTest.AddOrUpdate(
            key,
            attempt,
            (_, existing) => attempt.AttemptNumber >= existing.AttemptNumber ? attempt : existing);
    }

    /// <summary>
    /// Resolves the value identifying the test an execution belongs to, falling back through the
    /// weaker identifiers when the fingerprint is absent. The final fallback is the execution's own
    /// id, which makes an execution carrying no identity at all count as its own distinct test rather
    /// than merging with every other such execution.
    /// </summary>
    private static string ResolveTestKey(TestExecution execution)
    {
        TestIdentity identity = execution.Identity;

        if (!string.IsNullOrEmpty(identity.TestFingerprint))
            return identity.TestFingerprint;

        if (!string.IsNullOrEmpty(identity.FullyQualifiedName))
            return identity.FullyQualifiedName;

        return !string.IsNullOrEmpty(execution.TestName)
            ? execution.TestName
            : execution.ExecutionId.ToString();
    }

    /// <summary>
    /// Tallies the recorded final attempts into per-outcome counts. The distinct-test count comes from
    /// the same enumeration as the buckets, so the two always agree even if a concurrent
    /// <see cref="Record"/> adds an entry midway.
    /// </summary>
    private FinalTally TallyFinalAttempts()
    {
        int distinct = 0;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int inconclusive = 0;
        int notExecuted = 0;
        int timeout = 0;

        foreach (KeyValuePair<(string Assembly, string Test), FinalAttempt> entry in _finalByTest)
        {
            distinct++;

            switch (entry.Value.Outcome)
            {
                case TestOutcome.Passed:
                    passed++;
                    break;
                case TestOutcome.Failed:
                    failed++;
                    break;
                case TestOutcome.Skipped:
                    skipped++;
                    break;
                case TestOutcome.Inconclusive:
                    inconclusive++;
                    break;
                case TestOutcome.NotExecuted:
                    notExecuted++;
                    break;
                case TestOutcome.Timeout:
                    timeout++;
                    break;
            }

            // No default arm above, deliberately. Nothing reaches _finalByTest except through
            // RecordFinalAttempt, which Record calls only after its own switch has rejected an
            // outcome it does not recognise — so a guard here could never fire. An unreachable throw
            // would read as protection while being dead code that can never be exercised.
        }

        return new FinalTally(distinct, passed, failed, skipped, inconclusive, notExecuted, timeout);
    }

    /// <summary>
    /// The outcome of the highest-numbered attempt seen for one test.
    /// </summary>
    private readonly struct FinalAttempt(int attemptNumber, TestOutcome outcome)
    {
        public int AttemptNumber { get; } = attemptNumber;

        public TestOutcome Outcome { get; } = outcome;
    }

    /// <summary>
    /// The distinct-test counts derived from <see cref="_finalByTest"/> in a single pass.
    /// </summary>
    private readonly struct FinalTally(
        int distinctTests,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        int notExecuted,
        int timeout)
    {
        public int DistinctTests { get; } = distinctTests;

        public int Passed { get; } = passed;

        public int Failed { get; } = failed;

        public int Skipped { get; } = skipped;

        public int Inconclusive { get; } = inconclusive;

        public int NotExecuted { get; } = notExecuted;

        public int Timeout { get; } = timeout;
    }
}
