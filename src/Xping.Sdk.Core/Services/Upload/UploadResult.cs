/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Statistics;

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

    /// <summary>
    /// Gets or sets the aggregated test statistics from the finalized session.
    /// Populated only on the <see cref="Models.TestSessionState.Finalized"/> upload;
    /// <c>null</c> for intermediate flushes.
    /// </summary>
    public QuickStatistics? QuickStatistics { get; set; }
}
