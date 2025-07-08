/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Interface for detecting the geographic location of test execution.
/// </summary>
public interface ILocationDetectionService
{
    /// <summary>
    /// Detects the current location where the test is being executed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>TestLocation information or null if detection fails.</returns>
    Task<TestLocation?> DetectLocationAsync(CancellationToken cancellationToken = default);
}
