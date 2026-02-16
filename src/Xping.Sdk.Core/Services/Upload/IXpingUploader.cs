/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.Upload;

/// <summary>
/// Defines a contract for uploading test execution results.
/// </summary>
public interface IXpingUploader
{
    /// <summary>
    /// Uploads a test session to the Xping Cloud Platform.
    /// </summary>
    /// <param name="testSession">The test session to upload.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the upload result.</returns>
    Task<UploadResult> UploadAsync(
        TestSession testSession,
        CancellationToken cancellationToken = default);
}
