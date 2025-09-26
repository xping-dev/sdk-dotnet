/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Shared;

/// <summary>
/// Provides extension methods for integer formatting and manipulation.
/// </summary>
internal static class IntegerExtensions
{
    /// <summary>
    /// Converts an integer to its ordinal representation (1st, 2nd, 3rd, 4th, etc.).
    /// </summary>
    /// <param name="index">The integer to convert to ordinal format.</param>
    /// <returns>A string representing the ordinal form of the integer.</returns>
    /// <example>
    /// <code>
    /// var first = 1.ToOrdinal(); // "1st"
    /// var second = 2.ToOrdinal(); // "2nd"
    /// var third = 3.ToOrdinal(); // "3rd"
    /// var fourth = 4.ToOrdinal(); // "4th"
    /// var eleventh = 11.ToOrdinal(); // "11th"
    /// var twentyFirst = 21.ToOrdinal(); // "21st"
    /// </code>
    /// </example>
    public static string ToOrdinal(this int index)
    {
        var absIndex = Math.Abs(index);
        var suffix = (absIndex % 10) switch
        {
            1 when absIndex % 100 != 11 => "st",
            2 when absIndex % 100 != 12 => "nd",
            3 when absIndex % 100 != 13 => "rd",
            _ => "th",
        };
        return $"{index}{suffix}";
    }
}
