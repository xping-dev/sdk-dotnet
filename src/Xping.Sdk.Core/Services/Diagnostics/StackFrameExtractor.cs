/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xping.Sdk.Core.Services.Diagnostics;

/// <summary>
/// The frames a signature was built from, and whether they are as good as intended.
/// </summary>
/// <param name="Frames">The frames, outermost first, method signature only.</param>
/// <param name="Degraded">
/// Whether the frames are worse than intended: framework frames used because no user frame was
/// found, or no frames at all.
/// </param>
public sealed record FrameExtraction(IReadOnlyList<string> Frames, bool Degraded)
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
public static class StackFrameExtractor
{
    /// <summary>
    /// The most frames a failure is grouped by (5).
    /// </summary>
    public const int MaxFrames = 5;

    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // "at Type.Method(args) in /file.cs:line 12". The words "at", "in" and "line" are localized
    // ("bei … in …:Zeile 12", "à … dans …:ligne 12"), so the shape is matched instead: one word, an
    // identifier containing a dot, an argument list, and then either nothing or a location ending
    // ":word number". The location is what keeps a prose line such as "Expected Foo.Bar(x) to …"
    // from being read as a frame.
    private static readonly Regex FrameLine = new(
        @"^\p{L}+\s+(?<identifier>[^\s(]*\.[^\s(]+)(?<arguments>\([^)]*\))(?:$|\s+\S+\s+.*:\S+\s+\d+$)",
        Options);

    /// <summary>
    /// Extracts the frames a signature should be built from.
    /// </summary>
    /// <param name="stackTrace">The stack trace as the adapter recorded it, which may be absent.</param>
    /// <returns>The frames and whether they are degraded.</returns>
    public static FrameExtraction Extract(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return FrameExtraction.None;

        List<string> user = [];
        List<string> framework = [];

        foreach (string line in stackTrace!.Split('\n'))
        {
            // Anything that is not a frame is skipped rather than parsed: a real trace carries
            // "--- End of stack trace from previous location ---" between the halves of an awaited
            // call, and an exception's own message can precede the frames.
            Match match = FrameLine.Match(line.Trim());
            if (!match.Success)
                continue;

            // Compiler-generated names carry ordinals ("<Place>d__5", "<>c__DisplayClass4_0") that
            // renumber when a method or lambda is added above, so they are rewritten to the method
            // the author declared.
            string frame = StackFrameLookup.Normalize(match.Groups["identifier"].Value) +
                           match.Groups["arguments"].Value;

            if (!FrameworkNamespaces.IsFramework(frame))
            {
                user.Add(frame);

                // A stack overflow can leave thousands of frames, and nothing past these is used.
                if (user.Count == MaxFrames)
                    break;
            }
            else if (framework.Count < MaxFrames)
            {
                framework.Add(frame);
            }
        }

        // A trace made entirely of framework frames still says something — an assertion helper in a
        // shared base class, a failure inside the runner itself — so it is used rather than
        // discarded, and flagged so a reader knows the grouping is coarser than usual.
        if (user.Count > 0)
            return new FrameExtraction(user, false);

        return framework.Count > 0 ? new FrameExtraction(framework, true) : FrameExtraction.None;
    }
}
