/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// Thread-safe, incrementally updated implementation of <see cref="IRunningStatisticsAccumulator"/>.
/// Uses <see cref="Interlocked"/> operations for all scalar counters, a dedicated lock for the
/// compound slowest-test state, and a <see cref="ConcurrentDictionary{TKey,TValue}"/> holding the
/// final attempt of each distinct test.
/// </summary>
/// <remarks>
/// Every counter is kept twice: once for the whole test host process, and once per test assembly the
/// host ran. A solution-wide <c>dotnet test</c> batches several test projects into one host, so the
/// host-wide reading alone cannot be attributed to any of them. Both readings run through the same
/// <see cref="Tally"/>, so a <see cref="TestOutcome"/> added without a counter fails the same way in
/// both rather than balancing in one and silently not in the other.
/// </remarks>
internal sealed class RunningStatisticsAccumulator : IRunningStatisticsAccumulator, IWallClockAwareStatisticsAccumulator
{
    // The whole test host process.
    private readonly Counters _hostWide = new();

    // One entry per test assembly that recorded an execution. Executions naming no assembly are real
    // — identity generation can fail before the name is known — and are counted host-wide only,
    // never under an empty-string key, consistent with SessionAssemblies.Of skipping them.
    private readonly ConcurrentDictionary<string, Counters> _byAssembly = new(StringComparer.Ordinal);

    // Final attempt per distinct test — the scalar counters above cannot express this, because a
    // retried test arrives as several executions and only its last attempt decides whether it passed.
    // The key carries the assembly because TestFingerprint hashes the fully qualified name and the
    // parameters only, so identical names in two assemblies would otherwise collide. A ValueTuple key
    // keeps the per-execution path free of the string concatenation a composite key would need, and
    // it is what lets the distinct-test counters be grouped by assembly with no extra state.
    private readonly ConcurrentDictionary<(string Assembly, string Test), FinalAttempt> _finalByTest = new();

    /// <inheritdoc/>
    public void Record(TestExecution execution)
    {
        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        // Host-wide first, because Tally resolves the outcome's counter before mutating anything: an
        // outcome with no bucket throws with every total still where it was, and without having
        // created an assembly entry for an execution that was never counted.
        Tally(_hostWide, execution);

        string assembly = execution.Identity.Assembly;
        if (!string.IsNullOrEmpty(assembly))
            Tally(_byAssembly.GetOrAdd(assembly, static _ => new Counters()), execution);

        RecordFinalAttempt(execution);
    }

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot() => GetSnapshot(TimeSpan.Zero);

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot(TimeSpan wallClockElapsed)
    {
        long total = Interlocked.Read(ref _hostWide.Total);
        long passed = Interlocked.Read(ref _hostWide.Passed);
        long failed = Interlocked.Read(ref _hostWide.Failed);
        long skipped = Interlocked.Read(ref _hostWide.Skipped);
        long inconclusive = Interlocked.Read(ref _hostWide.Inconclusive);
        long notExecuted = Interlocked.Read(ref _hostWide.NotExecuted);
        long timeout = Interlocked.Read(ref _hostWide.Timeout);
        long durationTicks = Interlocked.Read(ref _hostWide.TotalDurationTicks);

        FinalCounts finalTally = TallyFinalAttempts();

        double successRate = total == 0 ? 0.0 : (double)passed / total;
        long totalMs = durationTicks / TimeSpan.TicksPerMillisecond;
        long averageMs = total == 0 ? 0L : totalMs / total;

        (long slowestTicks, string? slowestName) = ReadSlowest(_hostWide);

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
    public IReadOnlyDictionary<string, AssemblyStatistics> GetSnapshotByAssembly()
    {
        Dictionary<string, FinalCounts> finalByAssembly = TallyFinalAttemptsByAssembly();

        // Sorted so that two runs of the same solution serialize their assemblies in the same order
        // however the host happened to interleave them, matching SessionAssemblies.Of.
        var snapshot = new SortedDictionary<string, AssemblyStatistics>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, Counters> entry in _byAssembly)
        {
            // A concurrent Record adds the assembly's counters before its final attempt, so an entry
            // can briefly exist with no distinct-test tally yet. Reporting zeros for it is the same
            // answer GetSnapshot gives for an execution recorded midway through its own snapshot.
            finalByAssembly.TryGetValue(entry.Key, out FinalCounts? final);

            snapshot[entry.Key] = ToStatistics(entry.Value, final);
        }

        // Wrapped rather than returned directly: this value is handed to the session and travels to
        // the upload and the local store, and a snapshot a consumer can cast back and mutate is not
        // a snapshot. The wrapper keeps the sorted enumeration order underneath it.
        return new ReadOnlyDictionary<string, AssemblyStatistics>(snapshot);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _hostWide.Reset();
        _byAssembly.Clear();
        _finalByTest.Clear();
    }

    /// <summary>
    /// Adds one execution to a set of counters.
    /// </summary>
    /// <remarks>
    /// The outcome's counter is resolved before anything is mutated, so an outcome that lands in no
    /// bucket leaves the totals untouched. That ordering is what makes the distinct-test pass need
    /// no guard of its own.
    /// </remarks>
    private static void Tally(Counters counters, TestExecution execution)
    {
        ref long outcomeBucket = ref Bucket(counters, execution);

        long ticks = execution.Duration.Ticks;

        Interlocked.Increment(ref counters.Total);
        Interlocked.Increment(ref outcomeBucket);
        Interlocked.Add(ref counters.TotalDurationTicks, ticks);

        lock (counters.SlowestLock)
        {
            if (ticks > counters.SlowestDurationTicks)
            {
                counters.SlowestDurationTicks = ticks;
                counters.SlowestTestName = execution.TestName;
            }
        }
    }

    /// <summary>
    /// Returns the counter an execution's outcome belongs in.
    /// </summary>
    /// <remarks>
    /// One switch serves both the host-wide and the per-assembly counters. A second copy of it would
    /// let a newly added <see cref="TestOutcome"/> balance in one reading and silently not in the
    /// other, which reads as data loss in whichever one missed it.
    /// </remarks>
    private static ref long Bucket(Counters counters, TestExecution execution)
    {
        switch (execution.Outcome)
        {
            case TestOutcome.Passed:
                return ref counters.Passed;
            case TestOutcome.Failed:
                return ref counters.Failed;
            case TestOutcome.Skipped:
                return ref counters.Skipped;
            case TestOutcome.Inconclusive:
                return ref counters.Inconclusive;
            case TestOutcome.NotExecuted:
                return ref counters.NotExecuted;
            case TestOutcome.Timeout:
                return ref counters.Timeout;
            default:
                // Every outcome must land in exactly one bucket, because the report presents the
                // buckets as a breakdown of Total. A member added without a case here would inflate
                // Total and balance nowhere, which reads as data loss rather than as a missing arm.
                throw new ArgumentOutOfRangeException(
                    nameof(execution),
                    execution.Outcome,
                    "Unhandled TestOutcome; add a counter for it.");
        }
    }

    /// <summary>
    /// Reads the slowest-test pair atomically.
    /// </summary>
    private static (long Ticks, string? Name) ReadSlowest(Counters counters)
    {
        lock (counters.SlowestLock)
        {
            return (counters.SlowestDurationTicks, counters.SlowestTestName);
        }
    }

    /// <summary>
    /// Reduces one assembly's counters and distinct-test tally to its published statistics.
    /// </summary>
    private static AssemblyStatistics ToStatistics(Counters counters, FinalCounts? final)
    {
        long total = Interlocked.Read(ref counters.Total);
        long durationTicks = Interlocked.Read(ref counters.TotalDurationTicks);

        (long slowestTicks, string? slowestName) = ReadSlowest(counters);

        return new AssemblyStatistics
        {
            Total = (int)total,
            Passed = (int)Interlocked.Read(ref counters.Passed),
            Failed = (int)Interlocked.Read(ref counters.Failed),
            Skipped = (int)Interlocked.Read(ref counters.Skipped),
            Inconclusive = (int)Interlocked.Read(ref counters.Inconclusive),
            NotExecuted = (int)Interlocked.Read(ref counters.NotExecuted),
            Timeout = (int)Interlocked.Read(ref counters.Timeout),
            DistinctTests = final?.DistinctTests ?? 0,
            FinalPassed = final?.Passed ?? 0,
            FinalFailed = final?.Failed ?? 0,
            FinalSkipped = final?.Skipped ?? 0,
            FinalInconclusive = final?.Inconclusive ?? 0,
            FinalNotExecuted = final?.NotExecuted ?? 0,
            FinalTimeout = final?.Timeout ?? 0,
            TotalDurationMs = durationTicks / TimeSpan.TicksPerMillisecond,
            SlowestTestName = slowestName,
            SlowestTestDurationMs = slowestTicks / TimeSpan.TicksPerMillisecond
        };
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
    private FinalCounts TallyFinalAttempts()
    {
        var counts = new FinalCounts();

        foreach (KeyValuePair<(string Assembly, string Test), FinalAttempt> entry in _finalByTest)
            counts.Add(entry.Value.Outcome);

        return counts;
    }

    /// <summary>
    /// Tallies the recorded final attempts into per-outcome counts, grouped by the assembly each test
    /// belongs to. The key already carries the assembly, so no state beyond this pass is involved.
    /// </summary>
    /// <remarks>
    /// Tests whose execution named no assembly are left out entirely rather than collected under an
    /// empty key: an execution recorded before identity generation completed cannot be attributed,
    /// and the host-wide statistics are where it is counted.
    /// </remarks>
    private Dictionary<string, FinalCounts> TallyFinalAttemptsByAssembly()
    {
        var byAssembly = new Dictionary<string, FinalCounts>(StringComparer.Ordinal);

        foreach (KeyValuePair<(string Assembly, string Test), FinalAttempt> entry in _finalByTest)
        {
            string assembly = entry.Key.Assembly;

            if (string.IsNullOrEmpty(assembly))
                continue;

            if (!byAssembly.TryGetValue(assembly, out FinalCounts? counts))
                byAssembly[assembly] = counts = new FinalCounts();

            counts.Add(entry.Value.Outcome);
        }

        return byAssembly;
    }

    /// <summary>
    /// The execution-level counters for one scope — the whole host, or one test assembly.
    /// </summary>
    /// <remarks>
    /// Fields rather than properties: <see cref="Interlocked"/> needs a <see langword="ref"/> to the
    /// storage itself.
    /// </remarks>
    private sealed class Counters
    {
        // Outcome counters — stored as long for Interlocked.Read/Add compatibility
        internal long Total;
        internal long Passed;
        internal long Failed;
        internal long Skipped;
        internal long Inconclusive;
        internal long NotExecuted;
        internal long Timeout;
        internal long TotalDurationTicks;

        // Slowest test — requires a lock because name + duration must update atomically
        internal readonly object SlowestLock = new();
        internal long SlowestDurationTicks;
        internal string? SlowestTestName;

        internal void Reset()
        {
            Interlocked.Exchange(ref Total, 0L);
            Interlocked.Exchange(ref Passed, 0L);
            Interlocked.Exchange(ref Failed, 0L);
            Interlocked.Exchange(ref Skipped, 0L);
            Interlocked.Exchange(ref Inconclusive, 0L);
            Interlocked.Exchange(ref NotExecuted, 0L);
            Interlocked.Exchange(ref Timeout, 0L);
            Interlocked.Exchange(ref TotalDurationTicks, 0L);

            lock (SlowestLock)
            {
                SlowestDurationTicks = 0L;
                SlowestTestName = null;
            }
        }
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
    /// The distinct-test counts derived from <see cref="_finalByTest"/> in a single pass, for the
    /// whole host or for one assembly.
    /// </summary>
    private sealed class FinalCounts
    {
        public int DistinctTests { get; private set; }

        public int Passed { get; private set; }

        public int Failed { get; private set; }

        public int Skipped { get; private set; }

        public int Inconclusive { get; private set; }

        public int NotExecuted { get; private set; }

        public int Timeout { get; private set; }

        public void Add(TestOutcome outcome)
        {
            DistinctTests++;

            switch (outcome)
            {
                case TestOutcome.Passed:
                    Passed++;
                    break;
                case TestOutcome.Failed:
                    Failed++;
                    break;
                case TestOutcome.Skipped:
                    Skipped++;
                    break;
                case TestOutcome.Inconclusive:
                    Inconclusive++;
                    break;
                case TestOutcome.NotExecuted:
                    NotExecuted++;
                    break;
                case TestOutcome.Timeout:
                    Timeout++;
                    break;
            }

            // No default arm above, deliberately. Nothing reaches _finalByTest except through
            // RecordFinalAttempt, which Record calls only after Bucket has rejected an outcome it
            // does not recognise — so a guard here could never fire. An unreachable throw would read
            // as protection while being dead code that can never be exercised.
        }
    }
}
