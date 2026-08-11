/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// Everything needed to identify and navigate to one test.
/// </summary>
/// <remarks>
/// <see cref="SourceFile"/> and <see cref="SourceLineNumber"/> are carried whenever the SDK
/// populated them and are never stripped for brevity — they are what turns a report into something
/// an agent can act on rather than something a human has to go looking with.
/// </remarks>
/// <param name="TestFingerprint">The stable identity used to join executions across sessions.</param>
/// <param name="FullyQualifiedName">Namespace, class and method.</param>
/// <param name="DisplayName">The name a test runner would show, including parameters.</param>
/// <param name="SourceFile">Source path, when the SDK captured one.</param>
/// <param name="SourceLineNumber">Line the test begins on, when the SDK captured one.</param>
/// <param name="Assembly">The test assembly this test belongs to.</param>
internal sealed record TestReference(
    string TestFingerprint,
    string FullyQualifiedName,
    string DisplayName,
    string? SourceFile,
    int? SourceLineNumber,
    string Assembly);

/// <summary>
/// What a finding is about: one test, or a set of them.
/// </summary>
internal abstract record FindingSubject
{
    private FindingSubject()
    {
    }

    /// <summary>
    /// Gets the value that orders findings about the same kind and impact deterministically.
    /// </summary>
    public abstract string SortKey { get; }

    /// <summary>
    /// Gets the tests this finding covers.
    /// </summary>
    public abstract IReadOnlyList<TestReference> Tests { get; }

    /// <summary>A finding about one test.</summary>
    /// <param name="Test">The test.</param>
    internal sealed record SingleTest(TestReference Test) : FindingSubject
    {
        /// <inheritdoc/>
        public override string SortKey => Test.TestFingerprint;

        /// <inheritdoc/>
        public override IReadOnlyList<TestReference> Tests => [Test];
    }

    /// <summary>A finding about a set of tests that share a cause.</summary>
    /// <param name="GroupId">Stable identifier for the cluster.</param>
    /// <param name="Members">The member tests.</param>
    internal sealed record Group(string GroupId, IReadOnlyList<TestReference> Members) : FindingSubject
    {
        /// <inheritdoc/>
        public override string SortKey => GroupId;

        /// <inheritdoc/>
        public override IReadOnlyList<TestReference> Tests => Members;
    }
}

/// <summary>
/// Base for the kind-specific evidence payload attached to a finding.
/// </summary>
/// <remarks>
/// <para>
/// Evidence states observations, never causes. <c>distinctSignatures: 3</c> is an observation;
/// <c>cause: "shared static state"</c> is a verdict, and naming a cause anchors the reader — human
/// or model — onto a guess and stops the investigation there.
/// </para>
/// <para>
/// A provider that could not evaluate a test emits no finding. It never emits a finding carrying
/// null evidence, because a consumer cannot tell that apart from "evaluated, found nothing".
/// </para>
/// </remarks>
internal abstract record FindingEvidence;

/// <summary>
/// One observation produced by local analysis.
/// </summary>
/// <param name="Id">Stable short identity; see <see cref="FindingId"/>.</param>
/// <param name="Kind">What the finding claims.</param>
/// <param name="Severity">Banded from <paramref name="Impact"/>.</param>
/// <param name="EvidenceLevel">How much data the claim rests on.</param>
/// <param name="Subject">The test or group the finding is about.</param>
/// <param name="Evidence">The kind-specific payload.</param>
/// <param name="DrillDownCommand">
/// The exact CLI invocation that expands this finding. Non-optional: it is how both a human and an
/// agent navigate without documentation.
/// </param>
/// <param name="Impact">
/// The score in [0,1] the severity band came from. Carried so ordering can break severity ties
/// meaningfully; deliberately not rendered, because a bare number invites false precision.
/// </param>
internal sealed record Finding(
    string Id,
    FindingKind Kind,
    Severity Severity,
    EvidenceLevel EvidenceLevel,
    FindingSubject Subject,
    FindingEvidence Evidence,
    string DrillDownCommand,
    double Impact);
