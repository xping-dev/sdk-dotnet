/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Retention settings for the local session store.
/// </summary>
/// <remarks>
/// All three limits are applied together after each write, oldest run first, until every limit holds.
/// The defaults keep well over one <see cref="ReportWindow"/> per assembly, so the report's default
/// window always fills and <c>--runs</c>, <c>--since</c> and <c>--test</c> have history beyond it.
/// </remarks>
public sealed class LocalStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of runs to retain for each test assembly. Defaults to 100.
    /// </summary>
    /// <remarks>
    /// Counted per assembly, not per store: several test projects share one store, and a store-wide
    /// count would let the busiest one push the others' history out. A run covering several
    /// assemblies counts towards each of them and is kept while any of them still needs it.
    /// </remarks>
    public int MaxRuns { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum total size of the store in bytes. Defaults to 100 MB.
    /// </summary>
    public long MaxBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum age of a retained run. Defaults to 30 days.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);
}
