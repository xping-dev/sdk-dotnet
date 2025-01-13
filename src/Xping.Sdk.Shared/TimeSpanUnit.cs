/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Shared;

/// <summary>
/// Defines the units of time that can be used to represent a time interval.
/// </summary>
internal enum TimeSpanUnit
{
    /// <summary>
    /// Represents time in milliseconds.
    /// </summary>
    Millisecond,
    /// <summary>
    /// Represents time in seconds.
    /// </summary>
    Second,
    /// <summary>
    /// Represents time in minutes.
    /// </summary>
    Minute,
    /// <summary>
    /// Represents time in hours.
    /// </summary>
    Hour,
    /// <summary>
    /// Represents time in days.
    /// </summary>
    Day
}
