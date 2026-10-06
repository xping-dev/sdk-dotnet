/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Xping.Cli.Auth.Discovery;

/// <summary>
/// A SemVer 2.0.0 version, compared by the precedence rules of its §11.
/// </summary>
/// <remarks>
/// Only what <c>xping_cli_min_version</c> needs: build metadata is accepted and ignored, and a
/// prerelease sorts below its release (<c>1.0.0-beta.1 &lt; 1.0.0</c>).
/// </remarks>
internal sealed partial class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _prerelease;

    private SemanticVersion(string text, long major, long minor, long patch, string[] prerelease)
    {
        Text = text;
        Major = major;
        Minor = minor;
        Patch = patch;
        _prerelease = prerelease;
    }

    public long Major { get; }

    public long Minor { get; }

    public long Patch { get; }

    /// <summary>
    /// Gets the version as it was parsed.
    /// </summary>
    public string Text { get; }

    public static bool TryParse(string? value, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (value is null)
            return false;

        Match match = Pattern().Match(value);
        if (!match.Success
            || !TryParseNumber(match.Groups["major"].Value, out long major)
            || !TryParseNumber(match.Groups["minor"].Value, out long minor)
            || !TryParseNumber(match.Groups["patch"].Value, out long patch))
        {
            return false;
        }

        string[] prerelease = match.Groups["pre"].Success ? match.Groups["pre"].Value.Split('.') : [];
        version = new SemanticVersion(value, major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
            return 1;

        int result = Major.CompareTo(other.Major);
        if (result == 0)
            result = Minor.CompareTo(other.Minor);
        if (result == 0)
            result = Patch.CompareTo(other.Patch);

        return result != 0 ? result : ComparePrerelease(_prerelease, other._prerelease);
    }

    public override string ToString() => Text;

    public override bool Equals(object? obj) => obj is SemanticVersion other && CompareTo(other) == 0;

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, string.Join('.', _prerelease));

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);

    // SemVer §11.4: a version without a prerelease ranks higher; identifiers compare one by one,
    // numeric ones numerically and below alphanumeric ones, and a longer list wins a shared prefix.
    private static int ComparePrerelease(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0)
            return right.Length.CompareTo(left.Length);

        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool leftNumeric = TryParseNumber(left[i], out long leftNumber);
            bool rightNumeric = TryParseNumber(right[i], out long rightNumber);

            int result = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i])
            };

            if (result != 0)
                return result;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static bool TryParseNumber(string value, out long number) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    // The grammar of semver.org, without leading zeros in numeric parts.
    [GeneratedRegex(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
        @"(?:-(?<pre>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
        @"(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
