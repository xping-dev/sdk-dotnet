/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Persistence;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Provides an offline queue for storing test executions when uploads fail.
/// Implementations should be thread-safe and handle file system errors gracefully.
/// </summary>
public interface IOfflineQueue
{
    /// <summary>
    /// Enqueues test executions to the offline queue.
    /// </summary>
    /// <param name="executions">The test executions to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnqueueAsync(IEnumerable<TestExecution> executions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues a batch of test executions from the queue.
    /// </summary>
    /// <param name="maxCount">The maximum number of executions to dequeue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of test executions, or empty if queue is empty.</returns>
    Task<IEnumerable<TestExecution>> DequeueAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current size of the queue (number of executions).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of executions in the queue.</returns>
    Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all items from the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup operations such as removing old queue files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
