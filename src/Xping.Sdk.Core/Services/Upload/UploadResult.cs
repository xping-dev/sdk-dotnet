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
    /// Gets or sets the number of test executions recorded.
    /// </summary>
    public int TotalRecordsCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the receipt ID from the server.
    /// </summary>
    public string? ReceiptId { get; set; }

    /// <summary>
    /// Gets or sets the HTTP round-trip duration in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the uncompressed payload size in bytes.
    /// </summary>
    public long PayloadSizeBytes { get; set; }
}
