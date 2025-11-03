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
    /// Uploads a batch of test executions.
    /// </summary>
    /// <param name="executions">The test executions to upload.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the upload result.</returns>
    Task<UploadResult> UploadAsync(
        IEnumerable<TestExecution> executions,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of an upload operation.
/// </summary>
public sealed class UploadResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the upload was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the number of executions uploaded.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the receipt ID from the server.
    /// </summary>
    public string? ReceiptId { get; set; }
}
