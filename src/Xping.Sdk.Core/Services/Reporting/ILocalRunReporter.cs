/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Reporting;

/// <summary>
/// Persists the finished run and prints the end-of-run local summary.
/// </summary>
public interface ILocalRunReporter
{
    /// <summary>
    /// Stores the run, analyses recent history, and writes the report to the terminal.
    /// </summary>
    /// <param name="run">The run that just completed.</param>
    /// <param name="stats">Session statistics for the run.</param>
    void Report(LocalRun run, QuickStatistics? stats);
}
