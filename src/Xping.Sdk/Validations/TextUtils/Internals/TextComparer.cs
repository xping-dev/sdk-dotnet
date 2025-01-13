/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.RegularExpressions;

namespace Xping.Sdk.Validations.TextUtils.Internals;

internal class TextComparer(TextOptions? options)
{
    private readonly TextOptions _options = options ?? new TextOptions();

    public bool Compare(string? str1, string? str2)
    {
        if (str1 == null && str2 == null)
        {
            return true;
        }

        if (str1 == null || str2 == null)
        {
            return false;
        }

        var comparisonType = _options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (_options.MatchWholeWord)
        {
            return string.Equals(str1.Trim(), str2.Trim(), comparisonType);
        }

        return str1.Trim().Contains(str2.Trim(), comparisonType);
    }

    public bool CompareWithRegex(string text, string regexPattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(regexPattern))
        {
            return false;
        }

        var regexOptions = _options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        var regex = new Regex(regexPattern, regexOptions);

        if (_options.MatchWholeWord)
        {
            var match = regex.Match(text);
            return match.Success && match.Value == text.Trim();
        }

        return regex.IsMatch(text);
    }
}
