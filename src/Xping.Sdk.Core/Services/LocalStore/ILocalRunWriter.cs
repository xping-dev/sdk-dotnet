/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Persists a finished test run to the local store.
/// </summary>
/// <remarks>
/// The SDK records runs; it does not interpret them. Flakiness analysis and report rendering live in
/// the Xping CLI, which reads the same store. Keeping interpretation out of the test host means the
/// SDK adds no analysis cost to a test run and never has to fight the test runner for the terminal.
/// </remarks>
public interface ILocalRunWriter
{
    /// <summary>
    /// Writes a completed run to the local store.
    /// </summary>
    /// <param name="run">The run that just finished.</param>
    /// <returns><see langword="true"/> when the run was persisted; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Implementations must not throw as a result of storage problems. A read-only checkout or a
    /// full disk has to degrade to "no local history", never to a failed test run.
    /// </remarks>
    bool Write(LocalRun run);
}
