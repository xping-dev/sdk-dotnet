/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;

namespace Xping.Sdk.Core.Services.Network;

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
