/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Collector.Internals;

/// <summary>
/// A no-op implementation of <see cref="ITestExecutionCollector"/> used when the SDK
/// is disabled or configuration validation fails. All operations are no-ops, allowing
/// tests to run without observability tracking.
/// </summary>
internal sealed class NoOpTestExecutionCollector : ITestExecutionCollector
{
    /// <inheritdoc/>
    public event EventHandler BufferFull
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public void RecordTest(TestExecution execution)
    {
        // No-op: discard the test execution
    }

    /// <inheritdoc/>
    public IReadOnlyList<TestExecution> Drain()
    {
        return Array.Empty<TestExecution>();
    }

    /// <inheritdoc/>
    public Task<CollectorStats> GetStatsAsync()
    {
        return Task.FromResult(new CollectorStats
        {
            TotalRecorded = 0,
            BufferCount = 0,
            TotalSampled = 0
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No-op: nothing to dispose
    }
}
