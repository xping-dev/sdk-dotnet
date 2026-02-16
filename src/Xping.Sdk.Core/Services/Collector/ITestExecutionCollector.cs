/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Collector;

/// <summary>
/// Defines a contract for collecting and managing test execution data.
/// </summary>
public interface ITestExecutionCollector : IDisposable
{
    /// <summary>
    /// Raised when the internal buffer reaches its configured capacity, as determined by
    /// <see cref="Configuration.XpingConfiguration.BatchSize"/>.
    /// Consumers can handle this event to react to buffer pressure.
    /// </summary>
    event EventHandler BufferFull;

    /// <summary>
    /// Records a test execution for later upload.
    /// </summary>
    /// <param name="execution">The test execution data to record.</param>
    void RecordTest(TestExecution execution);

    /// <summary>
    /// Removes and returns buffered test executions. The number of items returned is determined by
    /// <see cref="Configuration.XpingConfiguration.BatchSize"/> or the current buffer size, whichever is smaller.
    /// </summary>
    IReadOnlyList<TestExecution> Drain();

    /// <summary>
    /// Retrieves statistics about the collector's current state.
    /// </summary>
    /// <returns>A task containing the collector statistics.</returns>
    Task<CollectorStats> GetStatsAsync();
}
