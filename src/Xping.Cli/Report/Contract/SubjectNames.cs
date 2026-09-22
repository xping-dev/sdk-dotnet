/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// Resolves the name a subject is presented under.
/// </summary>
/// <remarks>
/// <para>
/// Resolved here, once, rather than in each renderer — the same rule <see cref="EvidenceHeadline"/>
/// follows for a finding's sentence. A renderer that chose between a subject's recorded names would
/// be deciding what a test is called, which is not a layout question, and the two renderers would
/// eventually call the same test two different things.
/// </para>
/// <para>
/// The identity comes from the fully qualified name and not from the display name. A display name is
/// runner-facing: for xUnit it is <c>[Fact(DisplayName = "…")]</c> prose, which is not greppable, is
/// not an identifier, and is not something <c>dotnet test --filter</c> accepts.
/// </para>
/// </remarks>
internal static class SubjectNames
{
    /// <summary>What a test is called when neither name was recorded.</summary>
    private const string Unnamed = "(unnamed)";

    /// <summary>What a cluster is called when its evidence named no cause.</summary>
    private const string UnrecordedCause = "(cause not recorded)";

    /// <summary>
    /// Resolves the short identity of one test.
    /// </summary>
    /// <param name="fullyQualifiedName">Namespace, class and method, as the adapter recorded it.</param>
    /// <param name="displayName">The name a runner would show, as the adapter recorded it.</param>
    /// <returns><c>Class.Method</c>, with an argument list where the case is parameterised.</returns>
    /// <remarks>
    /// <para>
    /// The fully qualified name is split at its <b>first</b> parenthesis before it is split on dots,
    /// because one adapter's qualified name already carries its arguments and one argument value in
    /// ten contains a decimal point: NUnit records <c>SampleApp.NUnit.SampleTests.Add(1.5, 2)</c>,
    /// which split on <c>.</c> first yields <c>5, 2)</c> and loses the class outright.
    /// </para>
    /// <para>
    /// Namespace is dropped and the last two segments kept. <c>Class.Method</c> is unambiguous
    /// within a suite, short enough never to truncate in practice, and pasteable straight after
    /// <c>dotnet test --filter FullyQualifiedName~</c>. A nested class arrives as
    /// <c>Outer+Inner.Method</c> from the qualified name's own encoding, which is correct.
    /// </para>
    /// </remarks>
    public static string ShortName(string? fullyQualifiedName, string? displayName)
    {
        if (fullyQualifiedName is not { Length: > 0 } qualified)
            return displayName is { Length: > 0 } recorded ? recorded : Unnamed;

        int paren = qualified.IndexOf('(', StringComparison.Ordinal);
        string name = paren < 0 ? qualified : qualified.Substring(0, paren);
        string arguments = paren < 0 ? string.Empty : qualified.Substring(paren);

        if (arguments.Length == 0 && displayName is { Length: > 0 } display)
            arguments = ArgumentsOf(display, LastSegment(name), qualified);

        return LastTwoSegments(name) + arguments;
    }

    /// <summary>
    /// Resolves what the members of a cluster have in common.
    /// </summary>
    /// <param name="evidence">The finding's evidence.</param>
    /// <returns>The cause, in the words the headline beneath it will use.</returns>
    /// <remarks>
    /// The first two cases are <see cref="EvidenceHeadline.FixtureSubject"/> itself rather than a
    /// copy of it, so that the subject line and the headline directly under it name the same member
    /// in the same words. A cluster nobody can name still lists its members, so saying the cause was
    /// not recorded costs a reader nothing — which is why the group's id is never the answer here.
    /// </remarks>
    public static string CauseLabel(FindingEvidence evidence) => evidence switch
    {
        BrokenFixtureEvidence fixtureFailure => EvidenceHeadline.FixtureSubject(fixtureFailure),

        SharedFailureEvidence shared when shared.Signature.ExceptionType is { Length: > 0 } type =>
            type,

        _ => UnrecordedCause
    };

    /// <summary>
    /// Takes the argument list a display name carries, where it is this method's.
    /// </summary>
    /// <param name="displayName">The name a runner would show.</param>
    /// <param name="method">The method segment of the qualified name.</param>
    /// <param name="qualified">The whole qualified name.</param>
    /// <returns>The argument list from its opening parenthesis, or empty where there is none.</returns>
    /// <remarks>
    /// <para>
    /// Both prefixes are required, because the three adapters build the name differently: xUnit's
    /// display name is prefixed with the whole qualified name, NUnit's and MSTest's with the method
    /// segment alone, and MSTest puts a space in front of the parenthesis. Matching the method
    /// segment alone would have cost every parameterised xUnit case its arguments.
    /// </para>
    /// <para>
    /// A theory given explicit prose produces <c>some prose(a: 1, b: 2)</c>, whose prefix matches
    /// neither, and its arguments are not shown. That is the correct outcome: the alternative is
    /// appending an argument list to a name that is not the method's.
    /// </para>
    /// <para>
    /// Appended exactly as the adapter wrote it. The three render arguments differently — named in
    /// xUnit, positional in the other two — and normalising them would invent a spelling no runner
    /// emits and no <c>--filter</c> accepts.
    /// </para>
    /// </remarks>
    private static string ArgumentsOf(string displayName, string method, string qualified)
    {
        int paren = displayName.IndexOf('(', StringComparison.Ordinal);
        if (paren < 0)
            return string.Empty;

        string prefix = displayName.Substring(0, paren).TrimEnd();

        return string.Equals(prefix, method, StringComparison.Ordinal) ||
               string.Equals(prefix, qualified, StringComparison.Ordinal)
            ? displayName.Substring(paren)
            : string.Empty;
    }

    /// <summary>
    /// Takes the last dot-separated segment of a name.
    /// </summary>
    private static string LastSegment(string name)
    {
        int last = name.LastIndexOf('.');
        return last < 0 ? name : name.Substring(last + 1);
    }

    /// <summary>
    /// Takes the last two dot-separated segments of a name, or all of it where there are fewer.
    /// </summary>
    private static string LastTwoSegments(string name)
    {
        int last = name.LastIndexOf('.');
        if (last <= 0)
            return name;

        int previous = name.LastIndexOf('.', last - 1);
        return previous < 0 ? name : name.Substring(previous + 1);
    }
}
