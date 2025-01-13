/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Validations.TextUtils;

/// <summary>
/// Encapsulates options for text matching in HTML element location. This class provides configuration for text-based 
/// queries, allowing for precise or flexible matching criteria.
/// </summary>
public record TextOptions(
    bool MatchWholeWord = false,
    bool MatchCase = false)
{
    /// <summary>
    /// Indicates whether to perform a whole word match of the text. The default value is false.
    /// </summary>
    public bool MatchWholeWord { get; init; } = MatchWholeWord;

    /// <summary>
    /// Indicates whether to perform a case-sensitive comparison. The default value is false.
    /// </summary>
    public bool MatchCase { get; init; } = MatchCase;

    /// <summary>
    /// Returns a string that represents the current TextOptions object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return 
            $"{nameof(MatchWholeWord)}={MatchWholeWord}, " +
            $"{nameof(MatchCase)}={MatchCase}, ";
    }
}
