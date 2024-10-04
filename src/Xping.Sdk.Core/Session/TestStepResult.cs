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
