/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Enumerates the types of differences that can exist between two TestSession properties.
/// </summary>
public enum DifferenceType
{
    /// <summary>
    /// Indicates that a property or value has been added to the TestSession that was not present in the other.
    /// </summary>
    Added,

    /// <summary>
    /// Indicates that a property or value present in one TestSession is missing from the other.
    /// </summary>
    Removed,

    /// <summary>
    /// Indicates that a property or value in one TestSession has a different value from the other.
    /// </summary>
    Changed
}
