/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

/// <summary>
/// Represents the outcome of a test execution.
/// </summary>
public enum TestOutcome
{
    /// <summary>
    /// The test passed successfully.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// The test failed due to an assertion failure or unexpected exception.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The test was skipped and not executed.
    /// </summary>
    Skipped = 2,

    /// <summary>
    /// The test completed but the result was inconclusive.
    /// </summary>
    Inconclusive = 3,

    /// <summary>
    /// The test was not executed.
    /// </summary>
    NotExecuted = 4
}
