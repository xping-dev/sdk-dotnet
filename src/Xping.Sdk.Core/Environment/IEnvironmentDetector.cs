/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#nullable enable

namespace Xping.Sdk.Core.Environment;

using Xping.Sdk.Core.Models;

/// <summary>
/// Provides functionality to detect and collect environment information.
/// </summary>
public interface IEnvironmentDetector
{
    /// <summary>
    /// Detects the current environment information including OS, runtime, CI platform, and custom properties.
    /// </summary>
    /// <param name="collectNetworkMetrics">Whether to collect network reliability metrics (requires API endpoint).</param>
    /// <param name="apiEndpoint">The API endpoint to use for network metrics collection.</param>
    /// <returns>An <see cref="EnvironmentInfo"/> object containing detected environment details.</returns>
    EnvironmentInfo Detect(bool collectNetworkMetrics = false, string? apiEndpoint = null);
}
