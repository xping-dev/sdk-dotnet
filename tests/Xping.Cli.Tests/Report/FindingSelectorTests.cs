/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// <c>--id</c> resolves against the list the full report prints, and says where a moved claim went.
/// </summary>
public sealed class FindingSelectorTests
{
    [Fact]
    public void AReportedIdIsSelectedWithItsRow()
    {
        Finding alpha = Test(FindingKind.Flaky, "fp-Alpha");
        Finding beta = Test(FindingKind.Vanished, "fp-Beta");

        FindingSelection selection = FindingSelector.Select([alpha, beta], beta.Id);

        Assert.Equal(beta.Id, selection.Id);
        Assert.Same(beta, selection.Finding);
        Assert.Equal(2, selection.Row);
        Assert.Equal(FindingKind.Vanished, selection.Kind);
        Assert.Empty(selection.SameSubject);
    }

    [Fact]
    public void AnIdMatchingNoSubjectResolvesToNothing()
    {
        Finding alpha = Test(FindingKind.Flaky, "fp-Alpha");
        string elsewhere = FindingId.Compute(FindingKind.Flaky, "fp-Gone");

        FindingSelection selection = FindingSelector.Select([alpha], elsewhere);

        Assert.Equal(elsewhere, selection.Id);
        Assert.Null(selection.Finding);
        Assert.Null(selection.Row);
        Assert.Null(selection.Kind);
        Assert.Empty(selection.SameSubject);
    }

    /// <summary>
    /// A cluster promoted to a broken fixture keeps its subject and changes its id.
    /// </summary>
    /// <remarks>
    /// Not redirected: the selection says the old id named a shared failure and where the cluster is
    /// reported now, and names no finding of its own.
    /// </remarks>
    [Fact]
    public void APromotedClusterIsFoundUnderTheKindItWasPromotedFrom()
    {
        Finding flaky = Test(FindingKind.Flaky, "fp-Alpha");
        Finding fixture = Group(FindingKind.BrokenFixture, "sig_abc", "fp-Beta", "fp-Gamma");
        string shared = FindingId.Compute(FindingKind.SharedFailure, "sig_abc");

        FindingSelection selection = FindingSelector.Select([flaky, fixture], shared);

        Assert.Null(selection.Finding);
        Assert.Null(selection.Row);
        Assert.Equal(FindingKind.SharedFailure, selection.Kind);
        Assert.Equal([(fixture, 2)], selection.SameSubject);
    }

    [Fact]
    public void ARegressionHandedOverToInstabilityIsFoundUnderTheRegression()
    {
        Finding unstable = Test(FindingKind.DurationUnstable, "fp-Alpha");
        Finding other = Test(FindingKind.Flaky, "fp-Beta");
        string regression = FindingId.Compute(FindingKind.DurationRegression, "fp-Alpha");

        FindingSelection selection = FindingSelector.Select([other, unstable], regression);

        Assert.Null(selection.Finding);
        Assert.Equal(FindingKind.DurationRegression, selection.Kind);
        Assert.Equal([(unstable, 2)], selection.SameSubject);
    }

    [Fact]
    public void AReportedFindingListsItsSiblingsInListOrder()
    {
        Finding flaky = Test(FindingKind.Flaky, "fp-Alpha");
        Finding beta = Test(FindingKind.Vanished, "fp-Beta");
        Finding slower = Test(FindingKind.DurationRegression, "fp-Alpha");
        Finding retried = Test(FindingKind.RetryMasked, "fp-Alpha");

        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent([flaky, beta, slower, retried]);

        FindingSelection selection = FindingSelector.Select(ordered, slower.Id);

        Assert.Equal(2, selection.Row);
        Assert.Equal([(flaky, 1), (retried, 3)], selection.SameSubject);
    }

    /// <summary>
    /// A cluster naming a test is not that test's sibling, the way the full report never annotates it
    /// as one.
    /// </summary>
    [Fact]
    public void AClusterIsNoTestsSibling()
    {
        Finding flaky = Test(FindingKind.Flaky, "fp-Alpha");
        Finding cluster = Group(FindingKind.SharedFailure, "sig_abc", "fp-Alpha", "fp-Beta");

        Assert.Empty(FindingSelector.Select([flaky, cluster], flaky.Id).SameSubject);
        Assert.Empty(FindingSelector.Select([flaky, cluster], cluster.Id).SameSubject);
    }

    [Fact]
    public void UpperCaseHexSelectsTheSameFinding()
    {
        Finding alpha = Test(FindingKind.Flaky, "fp-Alpha");
        string shouted = "f_" + alpha.Id[2..].ToUpperInvariant();

        FindingSelection selection = FindingSelector.Select([alpha], shouted);

        Assert.Equal(alpha.Id, selection.Id);
        Assert.Same(alpha, selection.Finding);
    }

    /// <summary>
    /// The row a selection reports is the row the full report prints, after siblings have moved.
    /// </summary>
    [Fact]
    public void RowsAgreeWithTheEnvelopesNumbering()
    {
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(
        [
            Test(FindingKind.Flaky, "fp-Alpha"),
            Test(FindingKind.Flaky, "fp-Beta"),
            Test(FindingKind.Vanished, "fp-Gamma"),
            Test(FindingKind.DurationRegression, "fp-Beta"),
            Test(FindingKind.RetryMasked, "fp-Alpha")
        ]);

        Dictionary<string, string> annotations = EnvelopeBuilder.Annotations(ordered);

        // The reordering is what this is about; a list it leaves alone would prove nothing.
        Assert.Equal(2, annotations.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            FindingSelection selection = FindingSelector.Select(ordered, ordered[i].Id);

            Assert.Equal(i + 1, selection.Row);

            if (annotations.TryGetValue(ordered[i].Id, out string? annotation))
                Assert.EndsWith($"#{selection.SameSubject[0].Row}", annotation, StringComparison.Ordinal);
        }
    }

    private static Finding Test(FindingKind kind, string fingerprint) =>
        Build(kind, new FindingSubject.SingleTest(Reference(fingerprint)));

    private static Finding Group(FindingKind kind, string groupId, params string[] fingerprints) =>
        Build(kind, new FindingSubject.Group(groupId, [.. fingerprints.Select(Reference)]));

    private static Finding Build(FindingKind kind, FindingSubject subject) =>
        new(
            FindingId.Compute(kind, subject.SortKey),
            kind,
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
