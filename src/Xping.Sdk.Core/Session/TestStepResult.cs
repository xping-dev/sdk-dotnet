/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.ComponentModel.DataAnnotations;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Represents a test step result.
/// </summary>
public enum TestStepResult
{
    /// <summary>
    /// Represents a successful test result.
    /// </summary>
    [Display(Name = "succeeded")] Succeeded,

    /// <summary>
    /// Represents a failed test result.
    /// </summary>
    [Display(Name = "failed")] Failed,
}
