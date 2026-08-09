/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Retention settings for the local run store.
/// </summary>
/// <remarks>
/// All three limits are applied together after each write, oldest run first, until every limit holds.
/// </remarks>
public sealed class LocalStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of runs to retain. Defaults to 50.
    /// </summary>
    public int MaxRuns { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum total size of the store in bytes. Defaults to 50 MB.
    /// </summary>
    public long MaxBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum age of a retained run. Defaults to 30 days.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the number of runs the report analyses. Defaults to 12.
    /// </summary>
    public int AnalysisWindow { get; set; } = 12;
}
