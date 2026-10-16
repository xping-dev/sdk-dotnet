/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Xping.Cli.Report.Store;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report;

/// <summary>
/// How a <c>--test</c> name resolved.
/// </summary>
internal enum TestResolutionKind
{
    /// <summary>Exactly one test matched.</summary>
    Found,

    /// <summary>Two or more tests matched in the winning tier.</summary>
    Ambiguous,

    /// <summary>No tier matched anything.</summary>
    NotFound
}

/// <summary>
/// One test the store holds, as a <c>--test</c> name can match it.
/// </summary>
/// <param name="Fingerprint">The test's fingerprint.</param>
/// <param name="ShortFingerprint">
/// The shortest prefix, of at least eight characters, that names this test alone in its assembly.
/// </param>
/// <param name="Assembly">The assembly the test belongs to.</param>
/// <param name="FullyQualifiedName">The test's newest fully qualified name.</param>
/// <param name="LastSeen">When the newest session the test ran in started.</param>
/// <param name="Runs">How many sessions of the read the test ran in.</param>
internal sealed record TestMatch(
    string Fingerprint,
    string ShortFingerprint,
    string Assembly,
    string FullyQualifiedName,
    DateTime LastSeen,
    int Runs);

/// <summary>
/// What a <c>--test</c> name resolved to over the store.
/// </summary>
/// <param name="Kind">Whether one test, several or none matched.</param>
/// <param name="Query">The name as the reader typed it.</param>
/// <param name="Assembly">The assembly the lookup was restricted to, when one was given.</param>
/// <param name="Candidates">Every test the winning tier matched, newest run first.</param>
/// <param name="AssemblyCount">How many assemblies the read covered.</param>
/// <param name="RunCount">How many sessions the read covered.</param>
internal sealed record TestResolution(
    TestResolutionKind Kind,
    string Query,
    string? Assembly,
    IReadOnlyList<TestMatch> Candidates,
    int AssemblyCount,
    int RunCount)
{
    /// <summary>Gets the one test matched, or <see langword="null"/> unless <see cref="Kind"/> is Found.</summary>
    public TestMatch? Match => Kind == TestResolutionKind.Found ? Candidates[0] : null;
}

/// <summary>
/// The store read a <c>--test</c> name was resolved over, and what it resolved to.
/// </summary>
/// <param name="Read">The whole-store read, carrying its failure and what it skipped.</param>
/// <param name="Resolution">The resolution, or <see langword="null"/> when the read failed.</param>
internal sealed record TestLookup(WindowResult Read, TestResolution? Resolution);

/// <summary>
/// Resolves the name a reader passed to <c>--test</c> to one test in the store.
/// </summary>
/// <remarks>
/// <para>
/// Three tiers, and the first that matches anything wins: exact name, then a segment suffix, then a
/// fingerprint prefix. Every comparison is ordinal and case-sensitive, so whether a name matches one
/// test never depends on what else happens to be in the store. Typing less is what the ambiguity
/// path is for.
/// </para>
/// <para>
/// Keyed on assembly and fingerprint together. A pinned fingerprint is only unique inside its
/// assembly, and the view that follows never joins two assemblies, so the same fingerprint in two
/// of them is two tests and an ambiguity, not one test with a merged history.
/// </para>
/// </remarks>
internal static class TestResolver
{
    // The shortest fingerprint prefix the third tier accepts, and the shortest a short fingerprint is
    // printed at. Eight hex characters of SHA-256 rarely collide in a store of a few thousand tests,
    // and a shorter prefix would let a typo match by accident.
    private const int MinimumPrefix = 8;

    /// <summary>
    /// Reads the whole store and resolves a name over it.
    /// </summary>
    /// <param name="resolver">The window resolver, which applies the clock-skew drop to the read.</param>
    /// <param name="source">Where sessions are read from.</param>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="assembly">The assembly to restrict the lookup to, or <see langword="null"/> for all.</param>
    /// <returns>The read and the resolution.</returns>
    /// <remarks>
    /// Read through the resolver's whole-store window rather than from the source directly, so the
    /// lookup sees the same sessions a window would: a session stamped ahead of this machine's clock
    /// must not name the test a reader gets an answer about. Unscoped unless the reader named an
    /// assembly, because a name is a stronger scope than the assembly that happened to run last.
    /// </remarks>
    public static TestLookup Resolve(
        IWindowResolver resolver, ISessionSource source, string query, string? assembly)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(source);

        WindowResult read = resolver.Resolve(
            source, new WindowRequest(Runs: null, Since: null, assembly, WholeStore: true));

        return read.Window == null
            ? new TestLookup(read, null)
            : new TestLookup(read, Resolve(read.Window.Sessions, query, assembly));
    }

    /// <summary>
    /// Resolves a name over sessions already read.
    /// </summary>
    /// <param name="sessions">The sessions, newest first.</param>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="assembly">The assembly the sessions were restricted to, when one was given.</param>
    /// <returns>The resolution.</returns>
    public static TestResolution Resolve(IReadOnlyList<TestSession> sessions, string query, string? assembly)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(query);

        List<Entry> entries = Index(sessions);

        List<Entry> matched = entries.Where(e => string.Equals(e.FullyQualifiedName, query, StringComparison.Ordinal)).ToList();

        if (matched.Count == 0)
        {
            string segments = Normalise(query);
            matched = entries.Where(e => EndsWithSegments(e.FullyQualifiedName, segments)).ToList();
        }

        if (matched.Count == 0)
            matched = ByFingerprint(entries, query);

        // A computed fingerprint is lower-case hex, so a prefix copied from somewhere that upper-cases
        // it is still the reader's intent. Only for hex: a pinned fingerprint is any case, and
        // lowering one would match a different pin.
        if (matched.Count == 0 && IsHex(query))
            matched = ByFingerprint(entries, Lower(query));

        TestResolutionKind kind = matched.Count switch
        {
            0 => TestResolutionKind.NotFound,
            1 => TestResolutionKind.Found,
            _ => TestResolutionKind.Ambiguous
        };

        List<TestMatch> candidates = matched
            .OrderBy(e => e.NewestPosition)
            .ThenBy(e => e.Assembly, StringComparer.Ordinal)
            .ThenBy(e => e.FullyQualifiedName, StringComparer.Ordinal)
            .ThenBy(e => e.Fingerprint, StringComparer.Ordinal)
            .Select(e => new TestMatch(
                e.Fingerprint,
                ShortFingerprint(e, entries),
                e.Assembly,
                e.FullyQualifiedName,
                e.LastSeen,
                e.Runs))
            .ToList();

        int assemblyCount = entries.Select(e => e.Assembly).Distinct(StringComparer.Ordinal).Count();

        return new TestResolution(kind, query, assembly, candidates, assemblyCount, sessions.Count);
    }

    /// <summary>
    /// Returns whether a window holds any run of a matched test.
    /// </summary>
    /// <param name="match">The test.</param>
    /// <param name="window">The window resolved for it, scoped to its assembly.</param>
    /// <returns><see langword="true"/> when at least one session in the window ran it.</returns>
    public static bool InWindow(TestMatch match, AnalysisWindow window)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(window);

        return window.Sessions.Any(session => session.Executions.Any(e =>
            string.Equals(e.Identity.TestFingerprint, match.Fingerprint, StringComparison.Ordinal)
            && string.Equals(e.Identity.Assembly, match.Assembly, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Builds the line that says no test matched.
    /// </summary>
    /// <param name="resolution">A resolution that found nothing.</param>
    /// <returns>The message.</returns>
    public static string NotFoundMessage(TestResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        string runs = Count(resolution.RunCount, "run");

        return resolution.Assembly is { } assembly
            ? $"No test matching '{resolution.Query}' in {assembly} ({runs})."
            : $"No test matching '{resolution.Query}' in this store ({Count(resolution.AssemblyCount, "assembly", "assemblies")}, {runs}).";
    }

    /// <summary>
    /// Builds the line that says a test matched but the narrowed window holds none of its runs.
    /// </summary>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="match">The test it matched.</param>
    /// <param name="window">The window that holds none of its runs.</param>
    /// <returns>The message.</returns>
    /// <remarks>
    /// Names the flag that narrowed the window, because that flag is the whole explanation: the
    /// lookup read the whole store, so every run it found is outside the window and older than it.
    /// </remarks>
    public static string OutsideWindowMessage(string query, TestMatch match, AnalysisWindow window)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(window);

        string? flag = window.Resolution switch
        {
            WindowResolution.Runs => "--runs",
            WindowResolution.SinceDate or WindowResolution.SinceSha => "--since",
            _ => null
        };

        string earlier = $"It ran in {Count(match.Runs, "earlier run")}";

        return flag == null
            ? $"No run of '{query}' in this window. {earlier}."
            : $"No run of '{query}' in this window ({flag} {window.ResolutionArgument}). {earlier}; drop {flag} to see them.";
    }

    /// <summary>
    /// Groups the read into one entry per assembly and fingerprint.
    /// </summary>
    /// <remarks>
    /// Sessions are walked newest first, so the first identity seen is the newest one, as
    /// <see cref="Indexes.TestIndex"/> takes it: a renamed test is found under what it is called now.
    /// An execution with no assembly is left out, since the view that follows has to be scoped to one.
    /// </remarks>
    private static List<Entry> Index(IReadOnlyList<TestSession> sessions)
    {
        var entries = new Dictionary<(string Assembly, string Fingerprint), Entry>();
        var ordered = new List<Entry>();

        for (int position = 0; position < sessions.Count; position++)
        {
            TestSession session = sessions[position];

            foreach (TestExecution execution in session.Executions)
            {
                TestIdentity identity = execution.Identity;
                if (string.IsNullOrEmpty(identity.TestFingerprint) || string.IsNullOrEmpty(identity.Assembly))
                    continue;

                var key = (identity.Assembly, identity.TestFingerprint);

                if (!entries.TryGetValue(key, out Entry? entry))
                {
                    entry = new Entry(identity.Assembly, identity.TestFingerprint, identity.FullyQualifiedName, position, session.StartedAt);
                    entries[key] = entry;
                    ordered.Add(entry);
                }

                // Retries put several executions of one test in a session; a run is a session.
                if (entry.LastPosition != position)
                {
                    entry.Runs++;
                    entry.LastPosition = position;
                }
            }
        }

        return ordered;
    }

    private static List<Entry> ByFingerprint(List<Entry> entries, string query)
    {
        // A whole fingerprint names its test alone, even when it is a prefix of another: a pinned
        // test that later became parameterised leaves both `login` and `login:<hash>` in the store,
        // and `login` would otherwise have no name that resolves (Amendment 3A).
        List<Entry> exact = entries.Where(e => string.Equals(e.Fingerprint, query, StringComparison.Ordinal)).ToList();
        if (exact.Count > 0 || query.Length < MinimumPrefix)
            return exact;

        return entries.Where(e => e.Fingerprint.StartsWith(query, StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// Returns the shortest prefix, from eight characters, that names one fingerprint in its assembly.
    /// </summary>
    /// <remarks>
    /// Within the assembly only, because every command that prints it also carries
    /// <c>--assembly</c>. A prefix that never becomes unique — the fingerprint is a prefix of another —
    /// widens to the whole fingerprint, which still resolves under the third tier's exact rule.
    /// </remarks>
    private static string ShortFingerprint(Entry entry, List<Entry> entries)
    {
        string fingerprint = entry.Fingerprint;

        var siblings = entries
            .Where(e => string.Equals(e.Assembly, entry.Assembly, StringComparison.Ordinal))
            .Select(e => e.Fingerprint)
            .ToList();

        for (int length = Math.Min(MinimumPrefix, fingerprint.Length); length < fingerprint.Length; length++)
        {
            string prefix = fingerprint[..length];

            if (siblings.Count(f => f.StartsWith(prefix, StringComparison.Ordinal)) == 1)
                return prefix;
        }

        return fingerprint;
    }

    /// <summary>
    /// Returns whether a fully qualified name ends with the given segments.
    /// </summary>
    /// <remarks>
    /// Tried as stored and with a trailing argument list removed, so <c>Add</c> matches every case of
    /// <c>Add(…)</c> while <c>Add(1,2)</c> still names one. All of the segments count too, which the
    /// first tier cannot catch once a nested type or an argument list is involved (Amendment 3B).
    /// </remarks>
    private static bool EndsWithSegments(string fullyQualifiedName, string segments)
    {
        string name = Normalise(fullyQualifiedName);

        return IsTrailing(name, segments)
            || (WithoutArguments(name) is { } bare && IsTrailing(bare, segments));
    }

    private static bool IsTrailing(string name, string segments) =>
        name.EndsWith(segments, StringComparison.Ordinal)
        && (name.Length == segments.Length || name[name.Length - segments.Length - 1] == '.');

    // NUnit and MSTest write a nested type as Outer+Inner; a reader types the dot.
    private static string Normalise(string name) => name.Replace('+', '.');

    /// <summary>
    /// Removes the argument list that closes a name, or returns <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// Matched from the closing parenthesis back to its opener rather than cut at the first one, so a
    /// parameterised fixture's arguments survive: <c>Fixture(1).Method(2)</c> becomes
    /// <c>Fixture(1).Method</c>, and arguments that hold parentheses of their own stay whole.
    /// </remarks>
    private static string? WithoutArguments(string name)
    {
        if (!name.EndsWith(')'))
            return null;

        int depth = 0;

        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (name[i] == ')')
            {
                depth++;
            }
            else if (name[i] == '(' && --depth == 0)
            {
                return name[..i];
            }
        }

        return null;
    }

    private static bool IsHex(string value) => value.Length > 0 && value.All(Uri.IsHexDigit);

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "A computed fingerprint is lower-case SHA-256 hex, so lower case is the form " +
                        "a hex prefix is compared in.")]
    private static string Lower(string value) => value.ToLowerInvariant();

    private static string Count(int count, string noun, string? plural = null) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? noun : plural ?? noun + "s")}");

    /// <summary>
    /// One assembly and fingerprint, accumulated over the read.
    /// </summary>
    private sealed class Entry(
        string assembly, string fingerprint, string fullyQualifiedName, int newestPosition, DateTime lastSeen)
    {
        public string Assembly { get; } = assembly;

        public string Fingerprint { get; } = fingerprint;

        public string FullyQualifiedName { get; } = fullyQualifiedName;

        public int NewestPosition { get; } = newestPosition;

        public DateTime LastSeen { get; } = lastSeen;

        public int Runs { get; set; }

        public int LastPosition { get; set; } = -1;
    }
}
