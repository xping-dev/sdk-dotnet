/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.ComponentModel.DataAnnotations;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Represents the state of the <see cref="TestSession"/>.
/// </summary>
public enum TestSessionState
{
    /// <summary>
    /// The session is still being created.
    /// </summary>
    [Display(Name = "not started")] NotStarted,
    /// <summary>
    /// The session has been completed and no further modifications are allowed.
    /// </summary>
    [Display(Name = "completed")] Completed,
    /// <summary>
    /// The session has been declined by test agent.
    /// </summary>
    [Display(Name = "declined")] Declined,
}
