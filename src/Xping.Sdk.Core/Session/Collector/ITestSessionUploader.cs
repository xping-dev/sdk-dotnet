/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Collector;

/// <summary>
/// Interface for uploading test sessions to the xping.io server.
/// </summary>
/// <remarks>
/// The uploaded test session data is used to create a comprehensive dashboard, enabling further analysis and 
/// maintaining historical data for performance tracking and comparison.
/// </remarks>
public interface ITestSessionUploader
{
    /// <summary>
    /// Asynchronously uploads a test session to the xping.io server.
    /// </summary>
    /// <param name="testSession">The test session to upload.</param>
    /// <param name="apiKey">The API key for authentication. If null, no authentication header will be added.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous upload operation, containing the upload result with status information.
    /// </returns>
    Task<UploadResult> UploadAsync(TestSession testSession, string? apiKey = null, CancellationToken cancellationToken = default);
}
