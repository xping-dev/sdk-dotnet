/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Xping.Cli.Report.Signatures;

/// <summary>
/// Strips the detail that varies between runs out of a failure message.
/// </summary>
/// <remarks>
/// <para>
/// Without this, the same failure produces a new signature every run and every genuinely repeated
/// failure looks novel. A real example from a recorded session, two runs of one test:
/// </para>
/// <code>
/// Watchdog (126 ms) fired before the simulated service responded (202 ms). …
/// Watchdog (109 ms) fired before the simulated service responded (189 ms). …
/// </code>
/// <para>
/// The rules run in a fixed order, and the order carries meaning: timestamps are replaced before
/// numbers because the numeric rule would otherwise eat their digits and leave
/// <c>&lt;num&gt;-&lt;num&gt;-&lt;num&gt;T…</c> behind, and case is folded last so the earlier
/// patterns can stay case-sensitive about things like the <c>T</c> and <c>Z</c> in ISO-8601.
/// </para>
/// <para>
/// Type names, member names and quoted string literals containing no digits are deliberately left
/// alone. They are the diagnostic signal — normalising them collapses genuinely different failures
/// into one signature, which is a far more expensive mistake than failing to group two identical
/// ones.
/// </para>
/// </remarks>
internal static partial class MessageNormaliser
{
    private const string GuidToken = "<guid>";
    private const string UriToken = "<uri>";
    private const string PathToken = "<path>";
    private const string TimeToken = "<time>";
    private const string HexToken = "<hex>";
    private const string NumberToken = "<num>";

    /// <summary>
    /// Reduces a raw failure message to its stable form.
    /// </summary>
    /// <param name="message">The message as the adapter recorded it, which may be absent.</param>
    /// <returns>
    /// The normalised message, or an empty string when there was nothing to normalise. Empty rather
    /// than null so that a caller composing a signature never has to distinguish the two.
    /// </returns>
    public static string Normalise(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        // Line endings first. The xUnit adapter joins an exception's messages with
        // Environment.NewLine, so the same failure recorded on Windows and on macOS differs by a
        // carriage return before anything else has had a chance to run.
        string text = message.Replace("\r\n", "\n", StringComparison.Ordinal)
                             .Replace('\r', '\n');

        text = WhitespaceRuns().Replace(text, " ").Trim();

        var result = new StringBuilder(text.Length);
        int position = 0;

        // Quoted literals are copied through untouched rather than substituted into. Everything
        // between them goes through the rules. Splitting the string this way rather than masking the
        // literals with placeholders avoids the placeholders themselves matching a later rule.
        foreach (Match literal in QuotedLiteral().Matches(text))
        {
            // A quoted literal containing a digit is left to the rules: an id or a timestamp inside
            // quotes varies between runs exactly like one outside them.
            if (ContainsDigit().IsMatch(literal.Value))
                continue;

            result.Append(Substitute(text.AsSpan(position, literal.Index - position).ToString()));
            result.Append(literal.Value);
            position = literal.Index + literal.Length;
        }

        result.Append(Substitute(text[position..]));

        return result.ToString();
    }

    /// <summary>
    /// Applies the substitution rules to one unprotected span, in specification order.
    /// </summary>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "The specification defines the signature's normalised form as lower case. " +
                        "The result is a grouping key compared only against other results of this " +
                        "method, never round-tripped, security-scoped or shown as a user identifier.")]
    private static string Substitute(string text)
    {
        if (text.Length == 0)
            return text;

        text = Guids().Replace(text, GuidToken);

        // URIs before paths: a file:// URI ends in something the path rule would happily claim half
        // of, leaving <uri> and <path> glued together where one token belongs.
        text = Uris().Replace(text, UriToken);
        text = Paths().Replace(text, PathToken);

        text = Timestamps().Replace(text, TimeToken);
        text = HexLiterals().Replace(text, HexToken);
        text = Numbers().Replace(text, NumberToken);

        return text.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRuns();

    [GeneratedRegex("\"[^\"]*\"|'[^']*'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedLiteral();

    [GeneratedRegex(@"\d", RegexOptions.CultureInvariant)]
    private static partial Regex ContainsDigit();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex Guids();

    [GeneratedRegex(@"\b[a-zA-Z][a-zA-Z0-9+.\-]*://[^\s""'<>]+", RegexOptions.CultureInvariant)]
    private static partial Regex Uris();

    // Anchored on a boundary so that "and/or" and a bare "/" are not read as paths. Two segments are
    // required for the same reason.
    [GeneratedRegex(
        @"(?<=^|[\s(\[=,:;])(?:[A-Za-z]:[\\/]|\\\\|/)[A-Za-z0-9_.~%+$\-]+(?:[\\/][A-Za-z0-9_.~%+$\-]+)*",
        RegexOptions.CultureInvariant)]
    private static partial Regex Paths();

    // ISO-8601 first, then a bare date, then a clock or TimeSpan reading. Ordered longest-first so a
    // full timestamp is never left as a date followed by a stray <time>.
    [GeneratedRegex(
        @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+\-]\d{2}:\d{2})?" +
        @"|\d{4}-\d{2}-\d{2}" +
        @"|\b\d{1,2}/\d{1,2}/\d{2,4}\b" +
        @"|\b\d{1,2}:\d{2}:\d{2}(?:\.\d+)?\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex Timestamps();

    [GeneratedRegex(@"\b0[xX][0-9a-fA-F]+\b|\b[0-9a-fA-F]{16,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HexLiterals();

    // The lookarounds keep digits that belong to a name: the 1 in Nullable`1, the 256 in SHA256, the
    // 47 in a compiler-generated DisplayClass47_0. Those identify the code, not the run.
    [GeneratedRegex(@"(?<![\w.`])\d+(?:[.,]\d+)*(?![\w])", RegexOptions.CultureInvariant)]
    private static partial Regex Numbers();
}
