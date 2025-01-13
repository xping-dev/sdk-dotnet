/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Represents a difference between two TestSession properties.
/// </summary>
/// <param name="PropertyName">Gets or sets the name of the property that differs.</param>
/// <param name="Value1">Gets or sets the value of the property in the first TestSession.</param>
/// <param name="Value2">Gets or sets the value of the property in the second TestSession.</param>
/// <param name="Type">Gets or sets the type of difference.</param>
public record Difference(
    string PropertyName,
    object? Value1,
    object? Value2,
    DifferenceType Type);
