/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report;

/// <summary>
/// What <c>--id</c> resolved to, over the ordered finding list.
/// </summary>
/// <param name="Id">The id as requested, normalised to lower case.</param>
/// <param name="Finding">The finding with that id, or <see langword="null"/> when it is not reported.</param>
/// <param name="Row">The finding's position, from 1, in the ordered list; null when not reported.</param>
/// <param name="Kind">
/// The kind the id names: the finding's own when reported; the kind the reverse lookup established
/// when not; null when the lookup found nothing.
/// </param>
/// <param name="SameSubject">
/// Every other finding about the subject the id names, with its row, in list order. When reported,
/// its siblings; when not, where the claim went.
/// </param>
internal sealed record FindingSelection(
    string Id,
    Finding? Finding,
    int? Row,
    FindingKind? Kind,
    IReadOnlyList<(Finding Finding, int Row)> SameSubject);

/// <summary>
/// Picks the finding a reader asked to see out of the coordinator's output.
/// </summary>
/// <remarks>
/// <para>
/// Over the same list the full report prints, in the same order, so the row it reports is the row
/// the reader saw. Selecting after the coordinator rather than narrowing what it runs is the point:
/// an <c>Instead</c> handover crosses a kind filter in both directions, and a detail view that
/// disagrees with the full report about what is reported is wrong in the one way a drill-down must
/// never be.
/// </para>
/// <para>
/// An absent id is not redirected. The kind is half the hash, so a subject whose claim changed kind
/// carries a new id; the reverse lookup names the kind the old id claimed and where the subject is
/// reported now, and leaves the reader to ask for it. A promoted cluster is a different claim, and
/// showing it under the id of the one it replaced would say otherwise.
/// </para>
/// </remarks>
internal static class FindingSelector
{
    /// <summary>
    /// Resolves an id against the ordered findings.
    /// </summary>
    /// <param name="ordered">
    /// Every finding produced, as <see cref="FindingOrder.WithSiblingsAdjacent"/> orders them.
    /// </param>
    /// <param name="id">The requested id, already known to be well formed.</param>
    /// <returns>The selection.</returns>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "FindingId renders the hash in lower-case hex, so lower case is the form ids " +
                        "are compared in and the form the selection reports back.")]
    public static FindingSelection Select(IReadOnlyList<Finding> ordered, string id)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(id);

        // The id is a hash rendered in lower-case hex; nothing else in the tool is case-sensitive
        // about it, so neither is this.
        string requested = id.ToLowerInvariant();

        for (int i = 0; i < ordered.Count; i++)
        {
            Finding finding = ordered[i];

            if (!string.Equals(finding.Id, requested, StringComparison.Ordinal))
                continue;

            // The siblings the full report annotates, by the key it annotates them by. A cluster has
            // no single test to share, so it has none.
            string? test = FindingOrder.Test(finding);

            IReadOnlyList<(Finding Finding, int Row)> siblings = test == null
                ? []
                : About(ordered, other =>
                    !ReferenceEquals(other, finding)
                    && string.Equals(FindingOrder.Test(other), test, StringComparison.Ordinal));

            return new FindingSelection(requested, finding, i + 1, finding.Kind, siblings);
        }

        foreach (Finding finding in ordered)
        {
            string subject = finding.Subject.SortKey;

            foreach (FindingKind kind in Enum.GetValues<FindingKind>())
            {
                if (kind == finding.Kind
                    || !string.Equals(FindingId.Compute(kind, subject), requested, StringComparison.Ordinal))
                {
                    continue;
                }

                return new FindingSelection(
                    requested,
                    Finding: null,
                    Row: null,
                    kind,
                    About(ordered, other => string.Equals(other.Subject.SortKey, subject, StringComparison.Ordinal)));
            }
        }

        return new FindingSelection(requested, Finding: null, Row: null, Kind: null, SameSubject: []);
    }

    private static List<(Finding Finding, int Row)> About(
        IReadOnlyList<Finding> ordered, Func<Finding, bool> matches)
    {
        var found = new List<(Finding Finding, int Row)>();

        for (int i = 0; i < ordered.Count; i++)
        {
            if (matches(ordered[i]))
                found.Add((ordered[i], i + 1));
        }

        return found;
    }
}
