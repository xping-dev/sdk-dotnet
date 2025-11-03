/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Network;

using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Provides functionality to collect network reliability metrics.
/// </summary>
public interface INetworkMetricsCollector
{
    /// <summary>
    /// Collects network reliability metrics asynchronously.
    /// </summary>
    /// <param name="apiEndpoint">The API endpoint to measure latency against.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the collected network metrics.</returns>
    Task<NetworkMetrics> CollectAsync(string apiEndpoint, CancellationToken cancellationToken = default);
}
