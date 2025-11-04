/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Upload;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Defines a contract for uploading test execution results.
/// </summary>
public interface ITestResultUploader
{
    /// <summary>
    /// Uploads a test session with environment information.
    /// This should be called once per session, typically at the start.
    /// </summary>
    /// <param name="session">The test session to upload.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the upload result.</returns>
    Task<UploadResult> UploadSessionAsync(
        TestSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a batch of test executions.
    /// </summary>
    /// <param name="executions">The test executions to upload.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the upload result.</returns>
    Task<UploadResult> UploadAsync(
        IEnumerable<TestExecution> executions,
        CancellationToken cancellationToken = default);
}
