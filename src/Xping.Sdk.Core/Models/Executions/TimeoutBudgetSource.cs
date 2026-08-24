/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Where a test's declared timeout budget came from.
/// </summary>
/// <remarks>
/// Recorded alongside <see cref="TestExecution.TimeoutBudget"/> so that "no budget was declared" —
/// which leaves the budget null — stays distinguishable from "a budget was declared, and it was
/// unlimited". The two say different things about the author's intent, and a reader who sees only a
/// null cannot tell them apart.
/// </remarks>
public enum TimeoutBudgetSource
{
    /// <summary>The test declared a finite timeout, which <see cref="TestExecution.TimeoutBudget"/> holds.</summary>
    Declared = 0,

    /// <summary>
    /// The test explicitly declared that it may run without limit, so no budget applies.
    /// </summary>
    Infinite = 1
}
