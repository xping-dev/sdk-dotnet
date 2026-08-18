/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.RegularExpressions;

namespace Xping.Cli.Report.Signatures;

/// <summary>
/// The frames a signature was built from, and whether they are as good as intended.
/// </summary>
/// <param name="Frames">The frames, outermost first, method signature only.</param>
/// <param name="Degraded">
/// Whether the frames are worse than intended: framework frames used because no user frame was
/// found, or no frames at all.
/// </param>
internal sealed record FrameExtraction(IReadOnlyList<string> Frames, bool Degraded)
{
    /// <summary>Gets the result for a failure that carried no usable stack trace.</summary>
    public static FrameExtraction None { get; } = new([], true);
}

/// <summary>
/// Pulls the frames worth grouping a failure by out of a raw stack trace.
/// </summary>
/// <remarks>
/// <para>
/// File paths and line numbers are discarded. They change whenever anyone edits the file above the
/// failure, so keeping them would fragment one recurring failure into a new signature per commit —
/// which is precisely the history the report exists to accumulate.
/// </para>
/// <para>
/// Framework frames are dropped because they are the same for every assertion of the same kind:
/// signing a failure with <c>Xunit.Assert.True</c> would group every failed boolean assertion in the
/// suite into one cause.
/// </para>
/// </remarks>
internal static partial class StackFrameExtractor
{
    /// <summary>
    /// Extracts the frames a signature should be built from.
    /// </summary>
    /// <param name="stackTrace">The stack trace as the adapter recorded it, which may be absent.</param>
    /// <returns>The frames and whether they are degraded.</returns>
    public static FrameExtraction Extract(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return FrameExtraction.None;

        List<string> all = [];

        foreach (string line in stackTrace.Split('\n'))
        {
            // Anything that is not a frame is skipped rather than parsed: a real trace carries
            // "--- End of stack trace from previous location ---" between the halves of an awaited
            // call, and an exception's own message can precede the frames.
            Match match = FrameLine().Match(line.Trim());
            if (!match.Success)
                continue;

            string frame = SourceLocation().Replace(match.Groups["frame"].Value, string.Empty).Trim();
            if (frame.Length > 0)
                all.Add(frame);
        }

        if (all.Count == 0)
            return FrameExtraction.None;

        List<string> user = [.. all
            .Where(f => !FrameworkNamespaces.IsFramework(f))
            .Take(LocalAnalysisConstants.SignatureFrameCount)];

        // A trace made entirely of framework frames still says something — an assertion helper in a
        // shared base class, a failure inside the runner itself — so it is used rather than
        // discarded, and flagged so a reader knows the grouping is coarser than usual.
        return user.Count > 0
            ? new FrameExtraction(user, false)
            : new FrameExtraction(
                [.. all.Take(LocalAnalysisConstants.SignatureFrameCount)], true);
    }

    [GeneratedRegex(@"^at\s+(?<frame>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrameLine();

    [GeneratedRegex(@"\s+in\s+.+:line\s+\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceLocation();
}
