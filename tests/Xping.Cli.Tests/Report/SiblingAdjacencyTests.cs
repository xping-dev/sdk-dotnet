/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Two findings about one test are two findings, and the report has to say so.
/// </summary>
/// <remarks>
/// The same test used to appear at rank 4 and rank 5 with nothing connecting the two, and on a
/// longer report at ranks four apart. The ranking between distinct tests is untouched by all of
/// this: only findings that name a test already named move, and they move only as far as the last
/// finding naming it.
/// </remarks>
public sealed class SiblingAdjacencyTests
{
    /// <summary>
    /// Two findings about one test end up next to each other, second one annotated.
    /// </summary>
    [Fact]
    public void TwoFindingsAboutOneTestAreAdjacent()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test("f_a", "fp-Alpha"),
            Test("f_b", "fp-Beta"),
            Test("f_c", "fp-Alpha")
        ]);

        Assert.Equal(["f_a", "f_c", "f_b"], ordered.Select(f => f.Id));
        Assert.Equal(
            new Dictionary<string, string> { ["f_c"] = "same test as #1" },
            EnvelopeBuilder.Annotations(ordered));
    }

    /// <summary>
    /// Three of them stay in rank order under the first, each pointing at the first.
    /// </summary>
    /// <remarks>
    /// At the first and not at the one above it. The row a reader scrolls back to is the one that
    /// ranked highest, and a chain of references pointing at references is a chain they have to
    /// walk.
    /// </remarks>
    [Fact]
    public void ThreeFindingsAboutOneTestAllPointAtTheFirst()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test("f_a", "fp-Alpha"),
            Test("f_b", "fp-Beta"),
            Test("f_c", "fp-Alpha"),
            Test("f_d", "fp-Alpha")
        ]);

        Assert.Equal(["f_a", "f_c", "f_d", "f_b"], ordered.Select(f => f.Id));

        Dictionary<string, string> annotations = EnvelopeBuilder.Annotations(ordered);

        Assert.Equal("same test as #1", annotations["f_c"]);
        Assert.Equal("same test as #1", annotations["f_d"]);
    }

    /// <summary>
    /// A sibling four ranks down moves up, and everything between it keeps its order.
    /// </summary>
    [Fact]
    public void ASiblingFourRanksDownMovesUpToItsAnchor()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test("f_1", "fp-Alpha"),
            Test("f_2", "fp-Beta"),
            Test("f_3", "fp-Gamma"),
            Test("f_4", "fp-Delta"),
            Test("f_5", "fp-Alpha")
        ]);

        Assert.Equal(["f_1", "f_5", "f_2", "f_3", "f_4"], ordered.Select(f => f.Id));

        // The anchor keeps the rank it earned, so the ranking between distinct tests is what it was.
        Assert.Equal("same test as #1", EnvelopeBuilder.Annotations(ordered)["f_5"]);
    }

    /// <summary>
    /// A row number never points past the end of a truncated report.
    /// </summary>
    /// <remarks>
    /// The property <c>--top</c> rests on, asserted over every cut rather than at one boundary: a
    /// sibling always follows its anchor, so a prefix holding the sibling holds the anchor too. The
    /// reordering is applied before the list is cut, which is what makes that true.
    /// </remarks>
    [Fact]
    public void NoCutEverSeparatesASiblingFromTheRowItNames()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test("f_1", "fp-Alpha"),
            Test("f_2", "fp-Beta"),
            Test("f_3", "fp-Gamma"),
            Test("f_4", "fp-Beta"),
            Test("f_5", "fp-Alpha")
        ]);

        Dictionary<string, string> annotations = EnvelopeBuilder.Annotations(ordered);

        for (int top = 1; top <= ordered.Count; top++)
        {
            foreach (Finding shown in ordered.Take(top).Where(f => annotations.ContainsKey(f.Id)))
            {
                int anchor = int.Parse(
                    annotations[shown.Id].Split('#')[^1], System.Globalization.CultureInfo.InvariantCulture);

                Assert.True(
                    anchor <= top,
                    $"'{shown.Id}' names row {anchor} in a report showing {top}");
            }
        }
    }

    /// <summary>
    /// A cluster is not deduplicated against the tests inside it.
    /// </summary>
    /// <remarks>
    /// A broken fixture and a flaky test it blocked are two findings about two different things.
    /// Pulling the second up under the first would assert a relationship the evidence does not make,
    /// and a cluster has no single test to be the same test as.
    /// </remarks>
    [Fact]
    public void AClusterNeitherMovesNorIsAnnotated()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test("f_1", "fp-Alpha"),
            Group("f_2", "fp-Alpha", "fp-Beta"),
            Test("f_3", "fp-Beta")
        ]);

        Assert.Equal(["f_1", "f_2", "f_3"], ordered.Select(f => f.Id));
        Assert.Empty(EnvelopeBuilder.Annotations(ordered));
    }

    /// <summary>
    /// Two clusters sharing nothing are left where the ranking put them.
    /// </summary>
    [Fact]
    public void TwoClustersAreNeverGroupedTogether()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Group("f_1", "fp-Alpha"),
            Test("f_2", "fp-Beta"),
            Group("f_3", "fp-Gamma")
        ]);

        Assert.Equal(["f_1", "f_2", "f_3"], ordered.Select(f => f.Id));
    }

    /// <summary>
    /// A report in which every finding names a different test is returned untouched.
    /// </summary>
    [Fact]
    public void AListWithNoSiblingsKeepsItsRankingExactly()
    {
        Finding[] ranked =
        [
            Test("f_1", "fp-Alpha"),
            Test("f_2", "fp-Beta"),
            Test("f_3", "fp-Gamma")
        ];

        Assert.Equal(
            ranked.Select(f => f.Id),
            FindingOrder.WithSiblingsAdjacent(ranked).Select(f => f.Id));

        Assert.Empty(EnvelopeBuilder.Annotations(ranked));
    }

    private static Finding Test(string id, string fingerprint) =>
        Build(id, new FindingSubject.SingleTest(Reference(fingerprint)));

    private static Finding Group(string id, params string[] fingerprints) =>
        Build(id, new FindingSubject.Group($"sig_{id}", [.. fingerprints.Select(Reference)]));

    private static Finding Build(string id, FindingSubject subject) =>
        new(
            id,
            FindingKind.Flaky,
            Severity.High,
            EvidenceLevel.Moderate,
            10,
            subject,
            new StubEvidence(),
            "xping report",
            0.5);

    private static TestReference Reference(string fingerprint) =>
        new(fingerprint, $"MyApp.Tests.SampleTests.{fingerprint}", fingerprint, null, null, "MyApp.Tests");

    private sealed record StubEvidence : FindingEvidence;
}
