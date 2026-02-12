/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Upload;

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
