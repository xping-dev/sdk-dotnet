/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// The words the text report prints for Xping Cloud's view of a test (cli-auth-cli-spec §11.3).
/// </summary>
/// <remarks>
/// <para>
/// Until cloud-data-in-cli-cli-spec phase 3 rewrites it, this reads the score out of the 1.28 digest
/// and prints what it printed before.
/// </para>
/// <para>
/// Composed at render time from <see cref="CloudDigestDto"/> rather than resolved into the envelope's
/// strings, so that in JSON every Cloud value stays under a <c>cloud</c> object and the local
/// <c>contrast</c> and <c>metrics</c> are never mixed with it. Every phrase says "cloud" or
/// "confidence", so no Cloud figure can be read as a local statistic.
/// </para>
/// <para>
/// Every word read from the DTO goes through <see cref="Recorded"/>: the Cloud is another writer of
/// text this process prints, and is trusted no more than the local store (D7).
/// </para>
/// </remarks>
internal static class CloudText
{
    private const string InsufficientData = "insufficient-data";

    /// <summary>
    /// Whether any slot prints something for <paramref name="cloud"/>: a score that is not
    /// "insufficient data". The header claims Cloud only on the same test the rows use.
    /// </summary>
    public static bool HasScore(CloudDigestDto? cloud) =>
        cloud?.Confidence?.Score is not null && cloud.Confidence.Category != InsufficientData;

    /// <summary>
    /// The row-trailer segment, <c>confidence 0.62 · moderately reliable</c> or, without the
    /// category, <c>confidence 0.62</c>; <see langword="null"/> when the Cloud has no score worth
    /// showing.
    /// </summary>
    public static string? Trailer(CloudDigestDto? cloud, string separator, bool withCategory, TerminalCharset charset)
    {
        if (!HasScore(cloud))
            return null;

        string score = $"confidence {Score(cloud!.Confidence!.Score!.Value)}";
        return withCategory && Words(cloud.Confidence.Category, charset) is { } category ? $"{score} {separator} {category}" : score;
    }

    /// <summary>
    /// The latest-run contrast, <c>confidence 0.94 over 812 runs</c>, or <see langword="null"/> when
    /// the local sentence should stay.
    /// </summary>
    public static string? Contrast(CloudDigestDto? cloud) =>
        HasScore(cloud) ? $"confidence {Score(cloud!.Confidence!.Score!.Value)} over {Runs(cloud.TotalRuns ?? 0)}" : null;

    /// <summary>
    /// The detail view's labelled pairs; empty without Cloud data.
    /// </summary>
    public static IReadOnlyList<(string Label, string? Value)> Pairs(CloudDigestDto? cloud, TerminalCharset charset)
    {
        if (cloud is not { State: "ok" })
            return [];

        string? confidence = cloud.Confidence?.Score is { } score
            ? Words(cloud.Confidence.Category, charset) is { } category ? $"{Score(score)} ({category})" : Score(score)
            : null;

        return
        [
            ("cloud confidence", confidence),
            ("cloud evidence", $"{Words(cloud.Confidence?.EvidenceLevel ?? "no-data", charset)}, {Runs(cloud.TotalRuns ?? 0)}"),
            ("cloud trend", Words(cloud.Confidence?.Trend, charset))
        ];
    }

    private static string Score(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Runs(int runs) =>
        runs == 1 ? "1 run" : $"{runs.ToString(CultureInfo.InvariantCulture)} runs";

    /// <summary><c>moderately-reliable</c> → <c>moderately reliable</c>.</summary>
    private static string? Words(string? kebab, TerminalCharset charset) =>
        string.IsNullOrEmpty(kebab) ? null : Recorded(kebab, charset).Replace('-', ' ');

    /// <summary>A value the Cloud sent, made safe for the terminal.</summary>
    private static string Recorded(string value, TerminalCharset charset) => TerminalText.Printable(value, charset);
}
