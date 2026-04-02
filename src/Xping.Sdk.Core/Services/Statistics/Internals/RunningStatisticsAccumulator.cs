/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// Thread-safe, incrementally updated implementation of <see cref="IRunningStatisticsAccumulator"/>.
/// Uses <see cref="Interlocked"/> operations for all scalar counters and a dedicated lock
/// for the compound slowest-test state.
/// </summary>
internal sealed class RunningStatisticsAccumulator : IRunningStatisticsAccumulator
{
    // Outcome counters — stored as long for Interlocked.Read/Add compatibility
    private long _total;
    private long _passed;
    private long _failed;
    private long _skipped;
    private long _inconclusive;
    private long _notExecuted;
    private long _totalDurationTicks;

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
        }

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
    public QuickStatistics GetSnapshot()
    {
        long total = Interlocked.Read(ref _total);
        long passed = Interlocked.Read(ref _passed);
        long failed = Interlocked.Read(ref _failed);
        long skipped = Interlocked.Read(ref _skipped);
        long inconclusive = Interlocked.Read(ref _inconclusive);
        long notExecuted = Interlocked.Read(ref _notExecuted);
        long durationTicks = Interlocked.Read(ref _totalDurationTicks);

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

        return new QuickStatistics(
            total: (int)total,
            passed: (int)passed,
            failed: (int)failed,
            skipped: (int)skipped,
            inconclusive: (int)inconclusive,
            notExecuted: (int)notExecuted,
            successRate: successRate,
            totalDurationMs: totalMs,
            averageDurationMs: averageMs,
            slowestTestName: slowestName,
            slowestTestDurationMs: slowestTicks / TimeSpan.TicksPerMillisecond);
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
        Interlocked.Exchange(ref _totalDurationTicks, 0L);

        lock (_slowestLock)
        {
            _slowestDurationTicks = 0L;
            _slowestTestName = null;
        }
    }
}
