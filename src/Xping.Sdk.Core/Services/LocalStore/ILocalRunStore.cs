/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Persists and retrieves test runs on the developer's machine.
/// </summary>
/// <remarks>
/// Implementations must never throw as a result of storage problems. A read-only checkout, a full
/// disk or a locked file has to degrade to "no local history" — failing a developer's test run
/// because an observability side-channel could not write is never an acceptable trade.
/// </remarks>
public interface ILocalRunStore
{
    /// <summary>
    /// Gets a value indicating whether the store resolved to a usable location.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the store root directory, or <see langword="null"/> when unavailable.
    /// </summary>
    string? StorePath { get; }

    /// <summary>
    /// Writes a run and applies the retention policy.
    /// </summary>
    /// <param name="run">The run to persist.</param>
    /// <returns><see langword="true"/> when the run was written; otherwise <see langword="false"/>.</returns>
    bool Write(LocalRun run);

    /// <summary>
    /// Reads the most recent runs, newest last.
    /// </summary>
    /// <param name="maxRuns">Maximum number of runs to return.</param>
    /// <param name="assembly">
    /// When supplied, only runs recorded for this test assembly are returned.
    /// </param>
    /// <returns>
    /// The runs in chronological order, oldest first. Empty when the store is unavailable or holds
    /// no readable runs.
    /// </returns>
    /// <remarks>
    /// The assembly filter matters whenever a solution has more than one test project. All of them
    /// share one store, so without it a report would mix unrelated suites: an assembly's history
    /// would contain other assemblies' tests, and its run count would be the solution-wide total.
    /// </remarks>
    IReadOnlyList<LocalRun> ReadRecent(int maxRuns, string? assembly = null);
}
