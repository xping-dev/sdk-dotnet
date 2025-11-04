/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Collection;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Defines a contract for collecting and managing test execution data.
/// </summary>
public interface ITestExecutionCollector : IAsyncDisposable
{
    /// <summary>
    /// Sets the current test session for this collector.
    /// This session will be uploaded automatically before test executions during flush.
    /// </summary>
    /// <param name="session">The test session to associate with this collector.</param>
    void SetSession(TestSession session);

    /// <summary>
    /// Records a test execution for later upload.
    /// </summary>
    /// <param name="execution">The test execution data to record.</param>
    void RecordTest(TestExecution execution);

    /// <summary>
    /// Manually triggers a flush of buffered test executions to the uploader.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves statistics about the collector's current state.
    /// </summary>
    /// <returns>A task containing the collector statistics.</returns>
    Task<CollectorStats> GetStatsAsync();
}
