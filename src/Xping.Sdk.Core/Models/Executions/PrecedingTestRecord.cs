/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// A captured snapshot of a completed test, retained per worker to provide the preceding test context
/// for the next execution on that worker.
/// </summary>
public sealed class PrecedingTestRecord
{
    /// <summary>
    /// Gets or sets the stable test identifier.
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test display name.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test outcome.
    /// </summary>
    public TestOutcome Outcome { get; set; }
}
