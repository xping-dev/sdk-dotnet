/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.ComponentModel.DataAnnotations;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// The TestStepType enum is used to specify the type of <see cref="TestComponent"/>, whether it is an action step or 
/// a validation step. An action step is used to create an action for instance retrieve data, while a validation step is 
/// used to validate retrieved data for its correctness. 
/// </summary>
public enum TestStepType
{
    /// <summary>
    /// Represents action step.
    /// </summary>
    [Display(Name = "action step")] ActionStep = 0,

    /// <summary>
    /// Represents validate step.
    /// </summary>
    [Display(Name = "validate step")] ValidateStep = 1,

    /// <summary>
    /// Represents the step as a composition of multiple tests.
    /// </summary>
    [Display(Name = "composite step")] CompositeStep = 2,
}
