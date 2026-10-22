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
using Xping.Sdk.Core.Services.LocalStore;

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
/// What a <c>--test</c> name resolved to, and what the reads behind it skipped.
/// </summary>
/// <param name="Read">
/// The reads' failure and what they skipped. It never carries a window: the lookup reads one
/// assembly at a time and keeps none of them.
/// </param>
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
    /// <para>
    /// Read through the resolver rather than from the source directly, so the lookup sees the same
    /// sessions a window would: a session stamped ahead of this machine's clock must not name the
    /// test a reader gets an answer about. Every assembly unless the reader named one, because a name
    /// is a stronger scope than the assembly that happened to run last.
    /// </para>
    /// <para>
    /// One assembly at a time, each read whole and dropped before the next, so memory peaks at one
    /// assembly's history however many share the store, and no fixed ceiling cuts the oldest runs
    /// off. Only identities survive a read, and they are all the name needs.
    /// </para>
    /// <para>
    /// What the reads skipped is counted by file and by run, not summed per read: a file that names no
    /// assembly is opened by every scoped read, and a run covering two assemblies is read twice.
    /// </para>
    /// </remarks>
    public static TestLookup Resolve(
        IWindowResolver resolver, ISessionSource source, string query, string? assembly)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        if (!source.IsAvailable)
            return new TestLookup(WindowResult.Failed(WindowFailure.NoStore), null);

        IReadOnlyList<string> assemblies = assembly is null ? source.KnownAssemblies() : [assembly];

        var entries = new List<Entry>();
        var runs = new HashSet<Guid>();
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        var skewed = new HashSet<Guid>();
        bool anyRead = false;
        bool everyReadSkewed = true;

        foreach (string scope in assemblies)
        {
            StoreRead read = resolver.ReadStore(source, scope);

            unreadable.UnionWith(read.UnreadableFiles);
            skewed.UnionWith(read.SkewedSessionIds);

            if (read.Sessions.Count == 0)
                continue;

            anyRead = true;
            everyReadSkewed &= read.WholeStoreSkewed;

            entries.AddRange(Index(read.Sessions, scope));
            runs.UnionWith(read.Sessions.Select(s => s.SessionId));
        }

        // A run left out of one assembly's read but kept in another's, whose every run was ahead of
        // the clock, was not left out of the lookup.
        skewed.ExceptWith(runs);

        bool wholeStoreSkewed = anyRead && everyReadSkewed;

        if (!anyRead)
        {
            // Runs that name no assembly have no read of their own. They are told apart from an empty
            // store so the caller can say which it is: "nothing attributable", not "no runs".
            if (assembly is null && source.Read(1, null).Sessions.Count > 0)
            {
                return new TestLookup(
                    Warnings(null, unreadable, skewed, wholeStoreSkewed),
                    Resolve(entries, query, assembly, runCount: 0));
            }

            return new TestLookup(Warnings(WindowFailure.EmptyStore, unreadable, skewed, wholeStoreSkewed), null);
        }

        return new TestLookup(
            Warnings(null, unreadable, skewed, wholeStoreSkewed),
            Resolve(entries, query, assembly, runs.Count));
    }

    /// <summary>
    /// Resolves a name over sessions already read.
    /// </summary>
    /// <param name="sessions">The sessions, newest first.</param>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="assembly">The assembly to restrict the lookup to, or <see langword="null"/> for all.</param>
    /// <returns>The resolution.</returns>
    public static TestResolution Resolve(IReadOnlyList<TestSession> sessions, string query, string? assembly)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(query);

        int runCount = assembly == null
            ? sessions.Count
            : sessions.Count(s => SessionAssemblies.Covers(s, assembly));

        return Resolve(Index(sessions, assembly), query, assembly, runCount);
    }

    private static WindowResult Warnings(
        WindowFailure? failure, HashSet<string> unreadable, HashSet<Guid> skewed, bool wholeStoreSkewed) =>
        new(null, failure, null, unreadable.Count, skewed.Count, wholeStoreSkewed);

    private static TestResolution Resolve(List<Entry> entries, string query, string? assembly, int runCount)
    {
        List<Entry> matched = Match(entries, query);

        TestResolutionKind kind = matched.Count switch
        {
            0 => TestResolutionKind.NotFound,
            1 => TestResolutionKind.Found,
            _ => TestResolutionKind.Ambiguous
        };

        var siblings = new ShortFingerprints(entries);

        List<TestMatch> candidates = matched
            // The order the store reads runs in: newest first, session id breaking ties. Taken from the
            // run itself rather than its place in a read, since candidates come from separate reads.
            .OrderByDescending(e => e.LastSeen)
            .ThenBy(e => e.NewestSessionId.ToString("N"), StringComparer.Ordinal)
            .ThenBy(e => e.Assembly, StringComparer.Ordinal)
            .ThenBy(e => e.FullyQualifiedName, StringComparer.Ordinal)
            .ThenBy(e => e.Fingerprint, StringComparer.Ordinal)
            .Select(e => new TestMatch(
                e.Fingerprint,
                siblings.For(e),
                e.Assembly,
                e.FullyQualifiedName,
                e.LastSeen,
                e.Runs))
            .ToList();

        int assemblyCount = entries.Select(e => e.Assembly).Distinct(StringComparer.Ordinal).Count();

        return new TestResolution(kind, query, assembly, candidates, assemblyCount, runCount);
    }

    /// <summary>
    /// Works out one test's short fingerprint over sessions already read.
    /// </summary>
    /// <param name="sessions">The assembly's whole-store read, newest first.</param>
    /// <param name="assembly">The assembly.</param>
    /// <param name="fingerprint">The test's full fingerprint.</param>
    /// <returns>Its short fingerprint, or <see langword="null"/> when it did not run in the read.</returns>
    /// <remarks>
    /// The prefix <see cref="Resolve(IReadOnlyList{TestSession}, string, string?)"/> gives the same
    /// test as a candidate over the same read, so a command printed from any other view names the
    /// test the way <c>--test</c> resolves it. Only the one entry is worked out: each costs a name
    /// match over the whole assembly.
    /// </remarks>
    public static string? ShortFingerprintOf(
        IReadOnlyList<TestSession> sessions, string assembly, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(fingerprint);

        List<Entry> entries = Index(sessions, assembly);
        Entry? entry = entries.Find(e => string.Equals(e.Fingerprint, fingerprint, StringComparison.Ordinal));

        return entry == null ? null : new ShortFingerprints(entries).For(entry);
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

        return OutsideWindowMessage(query, match, flag, window.ResolutionArgument);
    }

    /// <summary>
    /// Builds the line that says a test matched but the narrowed window holds none of its runs, for a
    /// window that could not be opened at all.
    /// </summary>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="match">The test it matched.</param>
    /// <param name="flag">The flag that narrowed the window, or <see langword="null"/> for none.</param>
    /// <param name="argument">The value the reader gave that flag.</param>
    /// <returns>The message.</returns>
    /// <remarks>
    /// A <c>--since</c> later than every run of the assembly leaves no session to open a window
    /// over. That is still this answer: the test is in the store, and the flag is what hid it.
    /// </remarks>
    public static string OutsideWindowMessage(string query, TestMatch match, string? flag, string? argument)
    {
        ArgumentNullException.ThrowIfNull(match);

        string earlier = $"It ran in {Count(match.Runs, "earlier run")}";

        return flag == null
            ? $"No run of '{query}' in this window. {earlier}."
            : $"No run of '{query}' in this window ({flag} {argument}). {earlier}; drop {flag} to see them.";
    }

    /// <summary>
    /// Groups the read into one entry per assembly and fingerprint.
    /// </summary>
    /// <remarks>
    /// Sessions are walked newest first, so the first identity seen is the newest one, as
    /// <see cref="Indexes.TestIndex"/> takes it: a renamed test is found under what it is called now.
    /// An execution with no assembly is left out, since the view that follows has to be scoped to one.
    /// </remarks>
    private static List<Entry> Index(IReadOnlyList<TestSession> sessions, string? assembly)
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

                if (assembly != null && !string.Equals(identity.Assembly, assembly, StringComparison.Ordinal))
                    continue;

                var key = (identity.Assembly, identity.TestFingerprint);

                if (!entries.TryGetValue(key, out Entry? entry))
                {
                    entry = new Entry(identity.Assembly, identity.TestFingerprint, identity.FullyQualifiedName, session.SessionId, session.StartedAt);
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

    /// <summary>
    /// Runs the three tiers; the first that matches anything wins.
    /// </summary>
    private static List<Entry> Match(List<Entry> entries, string query)
    {
        List<Entry> matched = ByName(entries, query);

        if (matched.Count == 0)
            matched = ByFingerprint(entries, query);

        // A computed fingerprint is lower-case hex, so a prefix copied from somewhere that upper-cases
        // it is still the reader's intent. Only for hex, and only at a length that can prefix a
        // computed fingerprint: a pin is any case, and lowering one would match a different pin.
        if (matched.Count == 0 && query.Length >= MinimumPrefix && IsHex(query))
            matched = ByFingerprint(entries, Lower(query));

        return matched;
    }

    /// <summary>
    /// Runs the first two tiers: the exact name, then a run of trailing segments.
    /// </summary>
    private static List<Entry> ByName(List<Entry> entries, string query)
    {
        List<Entry> matched = entries
            .Where(e => string.Equals(e.FullyQualifiedName, query, StringComparison.Ordinal))
            .ToList();

        if (matched.Count > 0)
            return matched;

        string segments = Normalise(query);
        return entries.Where(e => EndsWithSegments(e.FullyQualifiedName, segments)).ToList();
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

    /// <summary>
    /// Reads a nested type's <c>+</c> as <c>.</c>, outside argument lists only.
    /// </summary>
    /// <remarks>
    /// NUnit and MSTest write a nested type as <c>Outer+Inner</c>, and a reader types the dot. Inside
    /// an argument list a <c>+</c> is data: rewriting it would make <c>Concat("a+b")</c> and
    /// <c>Concat("a.b")</c> one case.
    /// </remarks>
    private static string Normalise(string name)
    {
        if (!name.Contains('+', StringComparison.Ordinal))
            return name;

        char[] chars = name.ToCharArray();
        var scanner = new NameScanner();

        for (int i = 0; i < chars.Length; i++)
        {
            if (scanner.Next(chars[i]) && scanner.Depth == 0 && chars[i] == '+')
                chars[i] = '.';
        }

        return new string(chars);
    }

    /// <summary>
    /// Removes the argument list that closes a name, or returns <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// Walked forward with string and character literals skipped, so an argument holding a
    /// parenthesis of its own — <c>Smile(":)")</c> — does not hide where the list opens. Only the list
    /// that closes the name is removed, so a parameterised fixture's arguments survive:
    /// <c>Fixture(1).Method(2)</c> becomes <c>Fixture(1).Method</c>.
    /// </remarks>
    private static string? WithoutArguments(string name)
    {
        if (!name.EndsWith(')'))
            return null;

        var scanner = new NameScanner();
        int open = -1;

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            int depth = scanner.Depth;

            if (!scanner.Next(c))
                continue;

            if (c == '(' && depth == 0)
                open = i;
            else if (c == ')' && scanner.Depth == 0 && i == name.Length - 1)
                return name[..open];
            else if (scanner.Depth < 0)
                return null;
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
    /// Works out each candidate's short fingerprint: the shortest prefix, from eight characters, that
    /// <c>--test</c> resolves back to that test alone within its assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Within the assembly only, because every command that prints it also carries
    /// <c>--assembly</c>. Uniqueness among fingerprints comes from the sorted neighbours: a prefix
    /// one longer than the longer common prefix with either neighbour is unique, and a fingerprint
    /// that is a prefix of another widens to the whole of it, which the third tier's exact rule still
    /// resolves.
    /// </para>
    /// <para>
    /// Unique is not enough. The name tiers run first, so a prefix that happens to be a method name,
    /// or a pin equal to one, would resolve to that method. Such a prefix is widened until the name
    /// tiers no longer take it. A whole pin that still collides is printed anyway: no fingerprint
    /// alone can name that test, and <c>--test</c> then answers with the ambiguity.
    /// </para>
    /// </remarks>
    private sealed class ShortFingerprints(List<Entry> entries)
    {
        private readonly Dictionary<string, (string[] Sorted, List<Entry> Entries)> _byAssembly = entries
            .GroupBy(e => e.Assembly, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (g.Select(e => e.Fingerprint).Order(StringComparer.Ordinal).ToArray(), g.ToList()),
                StringComparer.Ordinal);

        public string For(Entry entry)
        {
            (string[] sorted, List<Entry> siblings) = _byAssembly[entry.Assembly];
            string fingerprint = entry.Fingerprint;
            int index = Array.BinarySearch(sorted, fingerprint, StringComparer.Ordinal);

            int shared = 0;
            if (index > 0)
                shared = CommonPrefix(fingerprint, sorted[index - 1]);
            if (index < sorted.Length - 1)
                shared = Math.Max(shared, CommonPrefix(fingerprint, sorted[index + 1]));

            int from = Math.Max(Math.Min(MinimumPrefix, fingerprint.Length), shared + 1);

            for (int length = Math.Min(from, fingerprint.Length); length < fingerprint.Length; length++)
            {
                string prefix = fingerprint[..length];
                List<Entry> named = ByName(siblings, prefix);

                if (named.Count == 0 || (named.Count == 1 && ReferenceEquals(named[0], entry)))
                    return prefix;
            }

            return fingerprint;
        }

        private static int CommonPrefix(string a, string b)
        {
            int length = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < length && a[i] == b[i])
                i++;
            return i;
        }
    }

    /// <summary>
    /// Tracks parenthesis depth through a name, skipping string and character literals.
    /// </summary>
    private struct NameScanner
    {
        private char _quote;
        private bool _escaped;

        /// <summary>Gets the parenthesis depth after the last structural character.</summary>
        public int Depth { get; private set; }

        /// <summary>
        /// Advances over one character.
        /// </summary>
        /// <returns><see langword="true"/> when the character is outside every literal.</returns>
        public bool Next(char c)
        {
            if (_quote != '\0')
            {
                if (_escaped)
                    _escaped = false;
                else if (c == '\\')
                    _escaped = true;
                else if (c == _quote)
                    _quote = '\0';

                return false;
            }

            if (c is '"' or '\'')
            {
                _quote = c;
                return false;
            }

            if (c == '(')
                Depth++;
            else if (c == ')')
                Depth--;

            return true;
        }
    }

    /// <summary>
    /// One assembly and fingerprint, accumulated over the read.
    /// </summary>
    private sealed class Entry(
        string assembly, string fingerprint, string fullyQualifiedName, Guid newestSessionId, DateTime lastSeen)
    {
        public string Assembly { get; } = assembly;

        public string Fingerprint { get; } = fingerprint;

        public string FullyQualifiedName { get; } = fullyQualifiedName;

        public Guid NewestSessionId { get; } = newestSessionId;

        public DateTime LastSeen { get; } = lastSeen;

        public int Runs { get; set; }

        public int LastPosition { get; set; } = -1;
    }
}
