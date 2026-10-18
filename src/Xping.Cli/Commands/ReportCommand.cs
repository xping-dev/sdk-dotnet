/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Cloud;
using Xping.Cli.Hosting;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Report.Store;
using Xping.Cli.Report.Windowing;
using Xping.Cli.Reporting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Commands;

/// <summary>
/// Runs local analysis and renders the result.
/// </summary>
/// <remarks>
/// Wiring only: resolve a window, build the context, run the providers, add Cloud data, render,
/// choose an exit code. Every decision worth arguing about lives in one of those steps rather than
/// here, which is what lets each be tested without a command line.
/// </remarks>
internal sealed class ReportCommand(
    ILocalSessionStoreFactory sessionStoreFactory,
    IWindowResolver windowResolver,
    FindingCoordinator coordinator,
    CloudEnricher enricher,
    GlobalOptions globalOptions,
    ConsoleIO io)
{
    /// <summary>
    /// Runs the report.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="cancellationToken">Stops the Cloud enrichment.</param>
    /// <returns>Process exit code.</returns>
    public async Task<int> RunAsync(ReportOptions options, CancellationToken cancellationToken)
    {
        string startDirectory = options.Directory ?? Directory.GetCurrentDirectory();

        ILocalSessionStore store = sessionStoreFactory.Create(startDirectory: startDirectory);
        var source = new LocalSessionSource(store);

        if (options.Test is { } query)
            return await RunTestAsync(options, query, startDirectory, store, source, cancellationToken).ConfigureAwait(false);

        // Auto-scope to the newest assembly unless the caller named one. Every test project in a
        // solution shares one store, so an unscoped report would pool unrelated suites: one
        // assembly's history would contain another's tests and its session count would be the
        // solution-wide total.
        string? assembly = options.Assembly;

        if (assembly == null)
        {
            // Asked of the resolver rather than of the store: "newest" has to mean the same thing
            // here as it does at the window bounds, or a run from a fast clock chooses which suite
            // the report is about — the one decision no later filtering can undo.
            assembly = windowResolver.ScopeAssembly(source);

            // Every report is scoped to exactly one assembly, so there is no unscoped fallback to
            // fall back to. A store holding runs that none of them attributes to an assembly has
            // nothing to scope by, and reporting on them anyway would pool every suite together and
            // produce numbers no --assembly value can reproduce.
            if (assembly == null && source.Read(1, null).Sessions.Count > 0)
                return ReportNothingAttributable(store);
        }

        WindowResult resolved = windowResolver.Resolve(
            source, new WindowRequest(options.Runs, options.Since, assembly));

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, options);

        (ReportEnvelope envelope, AnalysisResult analysis) = Analyze(
            options, resolved, assembly, assemblyGiven: options.Assembly != null);

        // On standard error even though the report also carries the count, and on every format:
        // a wrong clock is a defect on the machine rather than a fact about the suite, and the
        // developer reading JSON through a pipe is exactly the one who will not see the caveat line.
        WriteClockWarning(resolved);

        // The report ran and the finding is not in it. Said on standard error, with the envelope on
        // standard output only where a script is reading one: a text report of nothing would be a
        // fence around an absence the message already describes.
        if (envelope.Selection is { Reported: false } selection)
        {
            WriteNotReported(selection, envelope, source, assembly);

            if (options.Format == ReportFormat.Json)
                new JsonReportRenderer().Render(envelope, io.Output);

            return ExitCodes.NotFound;
        }

        return await EnrichAndRenderAsync(
            options, envelope, analysis, resolved, startDirectory, store, source, assembly, options.Assembly != null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the history of the one test a name resolves to, or says why it resolves to none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is looked up across the whole store before any window is chosen, because the name
    /// is what picks the assembly: a test is reached by what it is called, not by which suite ran
    /// last. Once it resolves, the command is that assembly's report over the whole-store window,
    /// with the findings about that test in it.
    /// </para>
    /// <para>
    /// A name that resolves to no test still opens a window, so a bad <c>--since</c> is reported as
    /// such in every format. Only a script reading JSON gets a report around the answer
    /// (Amendment 5A); in text the one line on standard error is the whole answer, and nothing is
    /// analysed for it.
    /// </para>
    /// </remarks>
    private async Task<int> RunTestAsync(
        ReportOptions options,
        string query,
        string startDirectory,
        ILocalSessionStore store,
        LocalSessionSource source,
        CancellationToken cancellationToken)
    {
        TestLookup lookup = TestResolver.Resolve(windowResolver, source, query, options.Assembly);

        if (lookup.Read.Window == null || lookup.Resolution is not { } resolution)
            return ReportUnavailable(lookup.Read, store, options);

        // The same refusal report makes, for the same reason: runs that name no assembly cannot be
        // scoped, so "no test matching" would blame the name for what is a property of the store.
        if (options.Assembly == null && resolution.AssemblyCount == 0)
            return ReportNothingAttributable(store);

        if (resolution.Match is not { } match)
        {
            // Every candidate's command names its own assembly and nothing of the window, so the
            // scope it is built from needs only the directory.
            TestHistoryDto history = resolution.Kind == TestResolutionKind.Ambiguous
                ? TestHistoryBuilder.Ambiguous(
                    resolution,
                    new ReportScope(
                        options.Assembly, lookup.Read.Window.Resolution, lookup.Read.Window.SessionCount,
                        AssemblyGiven: true, Directory: options.Directory))
                : TestHistoryBuilder.NotFound(query);

            return Unresolved(
                options,
                history,
                options.Assembly ?? windowResolver.ScopeAssembly(source),
                store,
                source,
                resolution.Kind == TestResolutionKind.Ambiguous
                    ? () => WriteAmbiguous(query, history.Candidates)
                    : () => io.Error.WriteLine(TestResolver.NotFoundMessage(resolution)),
                resolution.Kind == TestResolutionKind.Ambiguous ? ExitCodes.TestAmbiguous : ExitCodes.NotFound);
        }

        WindowResult resolved = windowResolver.Resolve(source, TestWindow(options, match.Assembly));

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, options);

        if (!TestResolver.InWindow(match, resolved.Window))
            return OutsideWindow(options, query, match, resolved);

        (ReportEnvelope envelope, AnalysisResult analysis) = Analyze(
            options,
            resolved,
            match.Assembly,

            // The assembly came from the name. A printed command that left it out would scope
            // itself to whichever assembly ran last (Amendment 5B).
            assemblyGiven: true,
            (context, _) => TestHistoryBuilder.Found(context, query, match));

        WriteClockWarning(resolved);

        return await EnrichAndRenderAsync(
            options, envelope, analysis, resolved, startDirectory, store, source, match.Assembly, assemblyGiven: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a name whose test ran, but in none of the runs the narrowed window holds.
    /// </summary>
    /// <remarks>
    /// Over the test's own assembly and that window, which are what the reader asked about: the
    /// name chose the assembly, and the flag chose the window (Amendment 5A).
    /// </remarks>
    private int OutsideWindow(ReportOptions options, string query, TestMatch match, WindowResult resolved)
    {
        AnalysisWindow window = resolved.Window
            ?? throw new ArgumentException("The window did not resolve.", nameof(resolved));

        WriteClockWarning(resolved);
        io.Error.WriteLine(TestResolver.OutsideWindowMessage(query, match, window));

        if (options.Format == ReportFormat.Json)
            RenderUnresolved(options, resolved, match.Assembly, TestHistoryBuilder.NotFound(query));

        return ExitCodes.NotFound;
    }

    /// <summary>
    /// Answers a name that resolved to no test: the message, and for a script the report around it.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="history">The history that records what was asked and what matched.</param>
    /// <param name="assembly">The assembly <c>report</c> would scope to, or <see langword="null"/>.</param>
    /// <param name="store">The store, for the messages that name it.</param>
    /// <param name="source">Where sessions are read from.</param>
    /// <param name="writeMessage">Writes the answer to standard error.</param>
    /// <param name="exitCode">The code the answer exits with.</param>
    /// <returns>The exit code.</returns>
    private int Unresolved(
        ReportOptions options,
        TestHistoryDto history,
        string? assembly,
        ILocalSessionStore store,
        LocalSessionSource source,
        Action writeMessage,
        int exitCode)
    {
        if (assembly == null)
            return ReportNothingAttributable(store);

        WindowResult resolved = windowResolver.Resolve(source, TestWindow(options, assembly));

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, options);

        WriteClockWarning(resolved);
        writeMessage();

        if (options.Format == ReportFormat.Json)
            RenderUnresolved(options, resolved, assembly, history);

        return exitCode;
    }

    /// <summary>
    /// Writes the report around a name that resolved to no test, for a script to read.
    /// </summary>
    /// <remarks>
    /// The full report over that scope, as <c>--id</c>'s not-reported path writes it, so the summary
    /// and truncation describe the same report a script would get without the name.
    /// </remarks>
    private void RenderUnresolved(ReportOptions options, WindowResult resolved, string assembly, TestHistoryDto history)
    {
        (ReportEnvelope envelope, _) = Analyze(
            options, resolved, assembly, assemblyGiven: true, (_, _) => history);

        new JsonReportRenderer().Render(envelope, io.Output);
    }

    /// <summary>
    /// The window <c>--test</c> reads: the whole store, unless <c>--runs</c> or <c>--since</c>
    /// replaces it.
    /// </summary>
    private static WindowRequest TestWindow(ReportOptions options, string assembly) =>
        new(options.Runs, options.Since, assembly, WholeStore: options.Runs == null && options.Since == null);

    /// <summary>
    /// Runs the providers over a resolved window and builds the envelope.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="resolved">The window, which must have resolved.</param>
    /// <param name="assembly">The assembly the report covers.</param>
    /// <param name="assemblyGiven">Whether every printed command has to name the assembly.</param>
    /// <param name="history">
    /// Builds what <c>--test</c> resolved to over the window, or <see langword="null"/> when the
    /// command is not <c>--test</c>.
    /// </param>
    /// <returns>The envelope, and the analysis behind it.</returns>
    /// <remarks>
    /// One sequence for <c>report</c>, <c>--id</c> and <c>--test</c>, so the findings a selection
    /// answers about are always the full report's findings, computed the same way.
    /// </remarks>
    private (ReportEnvelope Envelope, AnalysisResult Analysis) Analyze(
        ReportOptions options,
        WindowResult resolved,
        string? assembly,
        bool assemblyGiven,
        Func<AnalysisContext, ReportScope, TestHistoryDto>? history = null)
    {
        AnalysisWindow window = resolved.Window
            ?? throw new ArgumentException("The window did not resolve.", nameof(resolved));

        var context = new AnalysisContext(window, RevisionContext.FromNewest(window.Sessions, assembly));

        // Never narrowed under --id or --test. The parser rejects --kind beside either, and this
        // holds whatever built the options: narrowing only ever removes findings — a handover
        // silenced by a filter on its alternative's kind is one — so a narrowed run could miss a
        // finding the full report reports, and a selection has to answer for the full report.
        IReadOnlySet<FindingKind>? kinds =
            options.Id != null || options.Test != null || options.Kinds.Count == 0 ? null : options.Kinds.ToHashSet();

        AnalysisResult analysis = coordinator.Run(context, kinds, io.Error);

        // Beside the coordinator and not inside it: these rows are observations of one session,
        // not hypotheses, and must never be ranked, corrected or graded with the findings. A null
        // --top is --all, and --all lifts this section's cap along with the findings'.
        //
        // The findings passed here are the ones --kind asked for, so --kind narrows what the
        // section defers to rather than what it reports: a test whose only finding was filtered
        // out is listed as a row instead of counted on the closing line. The row states a true
        // thing either way, and recovering the unfiltered set would mean running every provider a
        // second time to change one line of prose.
        LatestRunAnalysis? latestRun = LatestRunAnalyzer.Analyze(
            context, analysis.Findings, showAll: options.Top == null);

        // What every printed command has to repeat to land on this report again.
        var scope = new ReportScope(
            assembly,
            window.Resolution,
            window.SessionCount,
            assemblyGiven,
            options.Runs,
            options.Since,
            options.Directory);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context,
            analysis,

            // A session that never finalised is never written, so the only way to lose one is for
            // its file to be unreadable — which is counted separately. This stays zero until the
            // store gains a way to persist a run that did not finish.
            incompleteSessions: 0,
            resolved.UnreadableSessions,
            resolved.SkewedSessions,
            options.Top,
            latestRun,
            options.Id,
            scope,
            history?.Invoke(context, scope));

        return (envelope, analysis);
    }

    /// <summary>
    /// Adds Cloud data to a report the local analysis has finished deciding, then writes it.
    /// </summary>
    private async Task<int> EnrichAndRenderAsync(
        ReportOptions options,
        ReportEnvelope envelope,
        AnalysisResult analysis,
        WindowResult resolved,
        string startDirectory,
        ILocalSessionStore store,
        LocalSessionSource source,
        string? assembly,
        bool assemblyGiven,
        CancellationToken cancellationToken)
    {
        AnalysisWindow window = resolved.Window
            ?? throw new ArgumentException("The window did not resolve.", nameof(resolved));

        // After every exit path the local analysis decides, so Cloud can never change one. The
        // one-line summary has no slot for Cloud data, so it is not worth a request.
        if (options.Format != ReportFormat.Summary)
        {
            var request = new EnrichmentRequest(startDirectory, options.Project, SessionPins(window.Sessions));
            EnrichmentResult enriched = await enricher.EnrichAsync(envelope, request, cancellationToken).ConfigureAwait(false);

            WriteCloudHints(enriched.Hints);
            envelope = enriched.Envelope;
        }

        return Render(options, envelope, analysis, window, store, source, assembly, assemblyGiven);
    }

    /// <summary>
    /// Writes the report and the notices around it, and chooses the exit code.
    /// </summary>
    private int Render(
        ReportOptions options,
        ReportEnvelope envelope,
        AnalysisResult analysis,
        AnalysisWindow window,
        ILocalSessionStore store,
        LocalSessionSource source,
        string? assembly,
        bool assemblyGiven)
    {
        // Resolved once and shared. Asking the console twice is how a report ends up drawn for a
        // terminal and decorated for a pipe, or the reverse.
        OutputCapabilities capabilities = OutputCapabilities.Resolve(
            options.Ascii, options.NoColor, redirected: !io.IsTerminal, Environment.GetEnvironmentVariable);

        // A blank line either side, for a person only. It separates the report from the command that
        // produced it and from whatever comes next, which a terminal gives no other help with — and
        // it is decoration, so a redirected stream carries the report and nothing around it.
        bool pad = options.Format == ReportFormat.Text && capabilities.Decorate;

        if (pad)
            io.Output.WriteLine();

        Renderer(options, capabilities).Render(envelope, io.Output);

        // Anything beyond the report itself is for a person at a terminal. A redirected stream is on
        // its way to a clipboard, a file or a script, and none of them asked for a scope notice.
        if (pad)
        {
            WriteScopeNotice(source, assembly, assemblyGiven);
            WriteCloudInvitation(store, analysis, window.Sessions, envelope, capabilities);

            // After the notices rather than after the report: the trailing blank closes the whole
            // block, and one wedged in the middle of it would separate nothing.
            io.Output.WriteLine();
        }

        // --fail-on is rejected beside --id and --test, so neither selection ever fails the command.
        return envelope.Selection != null || envelope.Test != null
            ? ExitCodes.Success
            : ExitCodes.ForReport(analysis.Findings, options.FailOn);
    }

    /// <summary>
    /// Says why Cloud data is missing, on standard error.
    /// </summary>
    /// <remarks>
    /// Quiet on a pipe so <c>report --json | jq</c> stays clean, except for the expired sign-in: that
    /// one has a fix only a human can apply, and the human may only ever see a log.
    /// </remarks>
    private void WriteCloudHints(IReadOnlyList<CloudHint> hints)
    {
        foreach (CloudHint hint in hints)
        {
            if (hint.Always || io.IsErrorTerminal || globalOptions.Verbose)
                io.Error.WriteLine(hint.Text);
        }
    }

    /// <summary>
    /// The project pin of each assembly's newest session, where that session recorded one.
    /// </summary>
    /// <remarks>
    /// Only the newest session speaks for an assembly. A pin that was removed since must not be
    /// revived from an older run: the newer runs went to the project the Cloud derives.
    /// </remarks>
    private static Dictionary<string, string> SessionPins(IReadOnlyList<TestSession> sessions)
    {
        var pins = new Dictionary<string, string>(StringComparer.Ordinal);
        var decided = new HashSet<string>(StringComparer.Ordinal);

        // Newest first, so the first session seen for an assembly is the one that decides.
        foreach (TestSession session in sessions)
        {
            string? pin = session.EnvironmentInfo?.CustomProperties?.GetValueOrDefault(LocalSessionProperties.ProjectId);

            foreach (string assembly in SessionAssemblies.Of(session))
            {
                if (decided.Add(assembly) && pin is { Length: > 0 })
                    pins[assembly] = pin;
            }
        }

        return pins;
    }

    private static IReportRenderer Renderer(ReportOptions options, OutputCapabilities capabilities) =>
        options.Format switch
        {
            ReportFormat.Json => new JsonReportRenderer(),
            ReportFormat.Summary => new SummaryReportRenderer(),
            // No detail line on a --kind-narrowed report: its rows are numbered against a list the
            // detail view does not use, and its row 1 is not the detail view's row 1.
            _ => new TextReportRenderer(capabilities, detailCommand: options.Kinds.Count == 0)
        };

    /// <summary>
    /// Explains why no report could be produced.
    /// </summary>
    /// <remarks>
    /// Always exit code 2, never 1. A build step has to tell "I looked and found problems" apart
    /// from "I could not look", and every case here is the second.
    /// </remarks>
    private int ReportUnavailable(WindowResult result, ILocalSessionStore store, ReportOptions options)
    {
        TextWriter error = io.Error;

        switch (result.Failure)
        {
            case WindowFailure.NoStore:
                error.WriteLine("No Xping local store found.");
                error.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
                break;

            case WindowFailure.ShaNotFound:
            case WindowFailure.SinceNotUnderstood:
                error.WriteLine(result.FailureDetail);
                break;

            default:
                error.WriteLine(options.Assembly == null
                    ? $"No runs recorded yet in {store.StorePath}"
                    : $"No runs recorded for assembly '{options.Assembly}' in {store.StorePath}");

                if (result.FailureDetail is { Length: > 0 } detail)
                    error.WriteLine(detail);

                error.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
                break;
        }

        if (result.UnreadableSessions > 0)
            error.WriteLine($"warning: {result.UnreadableSessions} unreadable run(s) skipped.");

        WriteClockWarning(result);

        return ExitCodes.InsufficientData;
    }

    /// <summary>
    /// Says that a clock somewhere disagrees with this machine's.
    /// </summary>
    /// <param name="result">What the window resolved to.</param>
    /// <remarks>
    /// Names the clock rather than the runs, because the clock is the defect. A run stamped in the
    /// future is not a corrupt file to be shrugged at; it is a machine writing into this store whose
    /// time is wrong, and until that is fixed the same runs will keep arriving undatable.
    /// </remarks>
    private void WriteClockWarning(WindowResult result)
    {
        if (result.WholeStoreSkewed)
        {
            // Said two ways, because this runs on the failure path too. There is no report to be
            // dated against anything when no window resolved, and a warning that describes one is
            // the wrong thing to hand a developer who was just told they have nothing to look at.
            io.Error.WriteLine(
                "warning: every recorded run is stamped ahead of this machine's clock. " +
                (result.Window == null
                    ? "Check the clock on the machine that recorded them."
                    : "The report is dated against the clock that recorded them."));
            return;
        }

        if (result.SkewedSessions > 0)
        {
            io.Error.WriteLine(
                $"warning: {result.SkewedSessions} run(s) stamped ahead of this machine's clock " +
                "were excluded. Check the clock on the machine that recorded them.");
        }
    }

    /// <summary>
    /// Explains that the store holds runs but nothing that can be attributed to a test assembly.
    /// </summary>
    /// <remarks>
    /// Exit code 2 for the same reason every other unavailable case uses it: the report could not
    /// be produced, which a build step has to tell apart from a report that found problems.
    /// </remarks>
    private int ReportNothingAttributable(ILocalSessionStore store)
    {
        TextWriter error = io.Error;

        error.WriteLine($"No runs in {store.StorePath} record which test assembly they belong to.");
        error.WriteLine(
            "A report always covers one assembly, so there is nothing to report on. " +
            "Run your tests again with the Xping SDK installed, then try again.");

        return ExitCodes.InsufficientData;
    }

    /// <summary>
    /// Lists the tests a name matched, and the command that names the first of them alone.
    /// </summary>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="candidates">Every test it matched, newest run first.</param>
    /// <remarks>
    /// Capped, because a name like <c>Add</c> can match a parameterised test's every case, and past
    /// ten the reader needs a longer name rather than a longer list. Each name is broken as the
    /// report breaks one, so the list survives being pasted into a chat.
    /// </remarks>
    private void WriteAmbiguous(string query, IReadOnlyList<TestCandidateDto> candidates)
    {
        const int Shown = 10;
        const int NameIndent = 4;

        TextWriter error = io.Error;

        error.WriteLine($"'{query}' matches {candidates.Count} tests. Name one by its fingerprint:");

        foreach (TestCandidateDto candidate in candidates.Take(Shown))
        {
            error.WriteLine($"  {candidate.FingerprintPrefix}  {candidate.Assembly}");

            foreach (string line in TextReportRenderer.WrapValue(
                candidate.FullyQualifiedName, TextReportRenderer.FenceWidth - NameIndent))
            {
                error.WriteLine(new string(' ', NameIndent) + line);
            }
        }

        if (candidates.Count > Shown)
            error.WriteLine($"  +{candidates.Count - Shown} more");

        error.WriteLine(candidates[0].Command);
    }

    /// <summary>
    /// Says that the finding <c>--id</c> named is not in this report, and where its subject went.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Not reported in this window", never "not found" or "error": an id that was on screen
    /// yesterday and is absent today is the report working. The window is named because it is the
    /// honest boundary — a finding that needs twenty runs is absent from five — and the other
    /// assemblies are counted because the likeliest reason an id is missing is that the report
    /// scoped itself to a different suite.
    /// </para>
    /// <para>
    /// Names the current id rather than showing it. A promoted cluster is a different claim, and
    /// rendering it under the id of the one it replaced would say otherwise.
    /// </para>
    /// </remarks>
    private void WriteNotReported(
        SelectionDto selection, ReportEnvelope envelope, LocalSessionSource source, string? assembly)
    {
        var message = new StringBuilder("Finding ").Append(selection.Id);

        if (selection.Kind is { } kind)
            message.Append(" (").Append(ReportVocabulary.LabelFor(kind)).Append(')');

        int sessions = envelope.Window.SessionCount;
        string runs = sessions == 1 ? "1 run" : $"{sessions} runs";

        message.Append(" is not reported in this window (")
               .Append(assembly == null ? runs : $"{assembly}, {runs}")
               .Append(").");

        if (selection.SameSubject.Count > 0)
        {
            message.Append(" Its subject is reported as ")
                   .AppendJoin(", ", selection.SameSubject.Select(same =>
                       $"{same.Id} ({ReportVocabulary.LabelFor(same.Kind)}, row {same.Row})"))
                   .Append('.');
        }

        int others = OtherAssemblies(source, assembly);

        if (others > 0)
        {
            message.Append(' ')
                   .Append(others)
                   .Append(others == 1 ? " other assembly" : " other assemblies")
                   .Append(" in this store (use --assembly to switch).");
        }

        io.Error.WriteLine(message.ToString());
    }

    /// <summary>
    /// Says which assembly the report covers when the store holds more than one.
    /// </summary>
    /// <remarks>
    /// Auto-scoping keeps the numbers honest, but silently omitting other suites would be worse than
    /// the problem it solves. Naming the scope makes the omission visible and points at the flag.
    /// </remarks>
    private void WriteScopeNotice(LocalSessionSource source, string? assembly, bool explicitlyChosen)
    {
        if (assembly == null || explicitlyChosen)
            return;

        int others = OtherAssemblies(source, assembly);

        if (others == 0)
            return;

        io.Output.WriteLine(
            $"Reporting on {assembly} {(char)0x00B7} {others} other " +
            (others == 1 ? "assembly" : "assemblies") +
            " in this store (use --assembly to switch).");
    }

    /// <summary>
    /// Counts the assemblies in the store other than the one the report covers.
    /// </summary>
    private static int OtherAssemblies(LocalSessionSource source, string? assembly) =>
        source.KnownAssemblies().Count(a => !string.Equals(a, assembly, StringComparison.Ordinal));

    /// <summary>
    /// Prints the cloud invitation, at most once a day and only when it is relevant.
    /// </summary>
    /// <remarks>
    /// Whether the project is already cloud-connected travels on the newest session's environment
    /// (<see cref="LocalSessionProperties.Mode"/>), and a report that just read Cloud data says so
    /// itself. Skipping either check would mean pitching the cloud to people who already use it.
    /// </remarks>
    private void WriteCloudInvitation(
        ILocalSessionStore store,
        AnalysisResult analysis,
        IReadOnlyList<TestSession> sessions,
        ReportEnvelope envelope,
        OutputCapabilities capabilities)
    {
        bool isConnected = (sessions.Count > 0 && LocalSessionProperties.IsConnected(sessions[0]))
            || envelope.Context?.Cloud?.Status is "ok" or "partial";

        if (!CtaThrottle.ShouldShow(store.StorePath, analysis.Findings.Count > 0, isConnected))
            return;

        io.Output.WriteLine();
        io.Output.WriteLine(
            "See flakiness across CI and your whole team " +
            $"{capabilities.Glyphs.Arrow} https://xping.io/start");
    }
}
