/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Auth.Browser;
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
using Xping.Sdk.Core.Services.Environment;
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
    ConsoleIO io,
    IEnvironmentVariableProvider environment,
    CtaThrottle ctaThrottle)
{
    // Once per run, so every command the report prints is quoted for the same shell.
    private readonly (ShellDialect Dialect, string? Warning) _shell =
        ShellDialects.Resolve(environment.GetVariable(ShellDialects.Variable), HostOsDetector.Current);

    /// <summary>
    /// Runs the report.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="cancellationToken">Stops the Cloud enrichment.</param>
    /// <returns>Process exit code.</returns>
    public async Task<int> RunAsync(ReportOptions options, CancellationToken cancellationToken)
    {
        if (_shell.Warning is { } warning)
            await io.Error.WriteLineAsync(warning).ConfigureAwait(false);

        string startDirectory = options.Directory ?? Directory.GetCurrentDirectory();

        ILocalSessionStore store = sessionStoreFactory.Create(startDirectory: startDirectory);
        var source = new LocalSessionSource(store);

        // Resolved once and shared. Asking the console twice is how a report ends up drawn for a
        // terminal and decorated for a pipe, or the reverse.
        OutputCapabilities capabilities = OutputCapabilities.Resolve(
            options.Ascii, options.NoColor, redirected: !io.IsTerminal, environment.GetVariable);

        // What the messages around the report may print of a recorded name or path: what the report
        // itself would print it with (D7).
        TerminalCharset charset = capabilities.Glyphs.Charset;

        if (options.Test is { } query)
            return await RunTestAsync(options, query, startDirectory, store, source, capabilities, cancellationToken).ConfigureAwait(false);

        // Every test project in a solution shares one store, so a report that pooled them would mix
        // unrelated suites: one assembly's history would hold another's tests and its session count
        // would be the solution-wide total. A report covers one assembly, and a store with several
        // answers a bare command with one row for each (report-format Amendment 6A).
        string? assembly = options.Assembly;

        if (assembly == null)
        {
            IReadOnlyList<string> assemblies = source.KnownAssemblies();

            // A store holding runs that none of them attributes to an assembly has nothing to scope
            // by, and reporting on them anyway would produce numbers no --assembly value reproduces.
            if (assemblies.Count == 0 && source.Read(1, null).Sessions.Count > 0)
                return ReportNothingAttributable(store, charset);

            if (assemblies.Count > 1)
            {
                return options.Id is { } id
                    ? await RunIdAcrossAsync(options, id, assemblies, startDirectory, store, source, capabilities, cancellationToken).ConfigureAwait(false)
                    : RunOverview(options, assemblies, store, source, capabilities);
            }

            assembly = assemblies.Count == 1 ? assemblies[0] : null;
        }

        WindowResult resolved = windowResolver.Resolve(
            source, new WindowRequest(options.Runs, options.Since, assembly));

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, options.Assembly, charset);

        (ReportEnvelope envelope, AnalysisResult analysis) = Analyze(options, resolved, assembly, source);

        // On standard error even though the report also carries the count, and on every format:
        // a wrong clock is a defect on the machine rather than a fact about the suite, and the
        // developer reading JSON through a pipe is exactly the one who will not see the caveat line.
        WriteClockWarning(resolved);

        // The report ran and the finding is not in it. Said on standard error, with the envelope on
        // standard output only where a script is reading one: a text report of nothing would be a
        // fence around an absence the message already describes.
        if (envelope.Selection is { Reported: false } selection)
        {
            WriteNotReported(selection, envelope, assembly, charset);

            if (options.Format == ReportFormat.Json)
                new JsonReportRenderer().Render(envelope, io.Output);

            return ExitCodes.NotFound;
        }

        return await EnrichAndRenderAsync(
            options, envelope, analysis, resolved, startDirectory, store, capabilities, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one row per assembly: what each one's own report would say in its header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every row is that assembly's report, built the way <c>--assembly</c> builds it and then
    /// reduced to its window and summary. Computing the numbers any other way would let the row and
    /// the report its command opens disagree, and a reader who sees two numbers for one suite stops
    /// trusting both (report-format Amendment 6B).
    /// </para>
    /// <para>
    /// Never enriched: the header a row copies is local, and Cloud data the report it opens did not
    /// headline would be exactly the disagreement above.
    /// </para>
    /// </remarks>
    private int RunOverview(
        ReportOptions options,
        IReadOnlyList<string> assemblies,
        ILocalSessionStore store,
        LocalSessionSource source,
        OutputCapabilities capabilities)
    {
        TerminalCharset charset = capabilities.Glyphs.Charset;

        // Each describes a list of findings, and the overview has none. Running anyway would print
        // rows the flag had no effect on, which reads as the flag being honoured.
        if (ListFlags(options) is { Length: > 0 } flags)
        {
            io.Error.WriteLine(
                $"{flags} needs --assembly: this store holds {assemblies.Count} assemblies, and a list of findings covers one.");
            io.Error.WriteLine("Run xping report to see them, then add --assembly <name>.");
            return ExitCodes.InsufficientData;
        }

        IReadOnlyList<AssemblyAnalysis> analyses = AnalyzeEach(options, assemblies, source, charset);
        LookupWarnings warnings = OverviewWarnings(analyses);

        if (analyses.All(a => a.Envelope == null))
            return ReportNoWindow(analyses, store, warnings, charset);

        WriteLookupWarnings(warnings, charset);

        if (options.Format == ReportFormat.Summary)
        {
            var summary = new SummaryReportRenderer(charset);

            foreach (AssemblyAnalysis analysis in OverviewBuilder.Order(analyses, a => a.Assembly, a => a.Envelope?.Summary))
            {
                if (analysis.Envelope is { } envelope)
                    summary.Render(envelope, io.Output);
                else
                    io.Output.WriteLine($"Xping: {TerminalText.Printable(analysis.Assembly, charset)}: {TerminalText.Printable(analysis.Unavailable!, charset)}");
            }
        }
        else
        {
            OverviewEnvelope overview = OverviewBuilder.Build(
                [.. analyses.Select(a => new OverviewRow(a.Assembly, a.Envelope, a.Unavailable))],
                assembly => new ReportScope(
                    assembly,
                    WindowResolution.Default,
                    0,
                    _shell.Dialect,
                    options.Runs,
                    options.Since,
                    options.Directory));

            if (options.Format == ReportFormat.Json)
            {
                JsonReportRenderer.Render(overview, io.Output);
            }
            else
            {
                // A blank line either side for a person only, as around the full report.
                bool pad = capabilities.Decorate;

                if (pad)
                    io.Output.WriteLine();

                new OverviewTextRenderer(capabilities).Render(overview, io.Output);

                if (pad)
                    io.Output.WriteLine();
            }
        }

        return ExitCodes.ForOverview(analyses.Select(a => a.Analysis?.Findings ?? []), options.FailOn);
    }

    /// <summary>
    /// Shows the finding an id names, looked for in every assembly's window.
    /// </summary>
    /// <remarks>
    /// Each window is opened as <c>--assembly</c> would open it, so the finding found is the one that
    /// assembly's report holds. An id can be reported by two assemblies when both ran the same test
    /// source; the reader then names one, as with a <c>--test</c> name that matched two tests
    /// (finding-detail Amendment 10A).
    /// </remarks>
    private async Task<int> RunIdAcrossAsync(
        ReportOptions options,
        string id,
        IReadOnlyList<string> assemblies,
        string startDirectory,
        ILocalSessionStore store,
        LocalSessionSource source,
        OutputCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        TerminalCharset charset = capabilities.Glyphs.Charset;
        IReadOnlyList<AssemblyAnalysis> analyses = AnalyzeEach(options, assemblies, source, charset);
        LookupWarnings warnings = OverviewWarnings(analyses);

        if (analyses.All(a => a.Envelope == null))
            return ReportNoWindow(analyses, store, warnings, charset);

        WriteLookupWarnings(warnings, charset);

        List<AssemblyAnalysis> reported =
            [.. analyses.Where(a => a.Envelope?.Selection is { Reported: true })];

        if (reported.Count == 1)
        {
            AssemblyAnalysis found = reported[0];

            return await EnrichAndRenderAsync(
                options, found.Envelope!, found.Analysis!, found.Resolved, startDirectory, store, capabilities, cancellationToken)
                .ConfigureAwait(false);
        }

        if (reported.Count > 1)
        {
            await io.Error.WriteLineAsync(
                $"Finding {TerminalText.Printable(id, charset)} is reported in {reported.Count} assemblies. Name one:").ConfigureAwait(false);

            // Each its own line, ending on the command, so any one of them can be copied and run.
            foreach (AssemblyAnalysis candidate in reported)
                await io.Error.WriteLineAsync("  " + TerminalText.Printable(candidate.Envelope!.Findings[0].DrillDown, TerminalCharset.Command)).ConfigureAwait(false);

            return ExitCodes.TestAmbiguous;
        }

        // Not reported anywhere. Standard output stays empty whatever the format: there is no one
        // report to hand a script, as for a --test name that resolved to no test (finding-detail
        // Amendment 10A).
        WriteNotReportedAnywhere(id, analyses, charset);

        return ExitCodes.NotFound;
    }

    /// <summary>
    /// One assembly's report, built for the overview or for an id looked for in every assembly.
    /// </summary>
    /// <param name="Assembly">The assembly.</param>
    /// <param name="Resolved">What its window resolved to.</param>
    /// <param name="Envelope">Its report; null when the window could not be opened.</param>
    /// <param name="Analysis">The analysis behind the report; null with it.</param>
    /// <param name="Unavailable">Why the window could not be opened; null when it was.</param>
    private sealed record AssemblyAnalysis(
        string Assembly, WindowResult Resolved, ReportEnvelope? Envelope, AnalysisResult? Analysis, string? Unavailable);

    /// <summary>
    /// Builds every assembly's report over the caller's window, as <c>--assembly</c> would build it.
    /// </summary>
    /// <remarks>
    /// A provider's warning is written with the assembly it is about: over several reports, a
    /// warning that named none could be about any of them.
    /// </remarks>
    private List<AssemblyAnalysis> AnalyzeEach(
        ReportOptions options, IReadOnlyList<string> assemblies, LocalSessionSource source, TerminalCharset charset)
    {
        var analyses = new List<AssemblyAnalysis>(assemblies.Count);

        foreach (string assembly in assemblies)
        {
            WindowResult resolved = windowResolver.Resolve(
                source, new WindowRequest(options.Runs, options.Since, assembly));

            if (resolved.Window == null)
            {
                analyses.Add(new AssemblyAnalysis(
                    assembly, resolved, null, null, resolved.FailureDetail ?? "No runs recorded."));
                continue;
            }

            using var warnings = new StringWriter();
            (ReportEnvelope envelope, AnalysisResult analysis) = Analyze(options, resolved, assembly, source, warnings);

            foreach (string line in warnings.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                io.Error.WriteLine($"{TerminalText.Printable(assembly, charset)}: {TerminalText.Printable(line, charset)}");

            analyses.Add(new AssemblyAnalysis(assembly, resolved, envelope, analysis, null));
        }

        return analyses;
    }

    /// <summary>
    /// What every assembly's read could not use, each file and each run counted once.
    /// </summary>
    /// <remarks>
    /// A file whose name carries no tags is opened by every assembly's read, and a run that covered
    /// two assemblies is read by both, so neither a sum nor a maximum of the reads' counts is the
    /// number of files or runs. An assembly whose every run is ahead is named on its own, as the
    /// <c>--test</c> lookup names it.
    /// </remarks>
    private static LookupWarnings OverviewWarnings(IReadOnlyList<AssemblyAnalysis> analyses)
    {
        bool everyAhead = analyses.All(a => a.Resolved.WholeStoreSkewed);

        return new LookupWarnings(
            analyses.SelectMany(a => a.Resolved.UnreadableFiles).Distinct(StringComparer.Ordinal).Count(),
            analyses.Where(a => !a.Resolved.WholeStoreSkewed).SelectMany(a => a.Resolved.SkewedSessionIds).Distinct().Count(),
            everyAhead ? [] : [.. analyses.Where(a => a.Resolved.WholeStoreSkewed).Select(a => a.Assembly)],
            everyAhead);
    }

    /// <summary>
    /// Explains why no assembly's window could be opened.
    /// </summary>
    /// <remarks>
    /// Every window was asked for with the same <c>--runs</c> or <c>--since</c>, so a reason all of
    /// them share is said once, as for one assembly. Reasons that differ are each said with the
    /// assembly they are about.
    /// </remarks>
    private int ReportNoWindow(
        IReadOnlyList<AssemblyAnalysis> analyses, ILocalSessionStore store, LookupWarnings warnings, TerminalCharset charset)
    {
        if (analyses.Select(a => (a.Resolved.Failure, a.Resolved.FailureDetail)).Distinct().Count() == 1)
            return ReportUnavailable(analyses[0].Resolved, store, null, charset, warnings);

        foreach (AssemblyAnalysis analysis in analyses)
            io.Error.WriteLine($"{TerminalText.Printable(analysis.Assembly, charset)}: {TerminalText.Printable(analysis.Unavailable!, charset)}");

        io.Error.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
        WriteLookupWarnings(warnings, charset);

        return ExitCodes.InsufficientData;
    }

    /// <summary>
    /// The finding-list flags the caller gave, as they would type them; empty when none.
    /// </summary>
    private static string ListFlags(ReportOptions options)
    {
        var flags = new List<string>(2);

        if (options.TopGiven)
            flags.Add(options.Top == null ? "--all" : "--top");

        if (options.Kinds.Count > 0)
            flags.Add("--kind");

        return string.Join(" and ", flags);
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
    /// A name that resolves to no test is answered from the lookup alone. Only a script reading JSON
    /// gets a report around the answer (Amendment 5A), and a window that cannot be opened for that
    /// report never replaces the answer: it is about the name, and the window is not.
    /// </para>
    /// </remarks>
    private async Task<int> RunTestAsync(
        ReportOptions options,
        string query,
        string startDirectory,
        ILocalSessionStore store,
        LocalSessionSource source,
        OutputCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        TerminalCharset charset = capabilities.Glyphs.Charset;
        TestLookup lookup = TestResolver.Resolve(windowResolver, source, query, options.Assembly);

        if (lookup.Resolution is not { } resolution)
        {
            return ReportUnavailable(
                WindowResult.Failed(lookup.Failure!.Value), store, options.Assembly, charset, lookup.Warnings);
        }

        // The same refusal report makes, for the same reason: runs that name no assembly cannot be
        // scoped, so "no test matching" would blame the name for what is a property of the store.
        if (options.Assembly == null && resolution.AssemblyCount == 0)
            return ReportNothingAttributable(store, charset);

        if (resolution.Match is not { } match)
            return Unresolved(options, resolution, lookup.Warnings, source, charset);

        WindowResult resolved = windowResolver.Resolve(source, TestWindow(options, match.Assembly));

        // A --since later than every run of the assembly opens no window at all. That is still a
        // test outside the window: the lookup just found it, so "no runs recorded" would be false.
        if (resolved.Failure == WindowFailure.EmptyStore && options.Since != null)
            return OutsideWindow(options, query, match, resolved, charset);

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, match.Assembly, charset);

        if (!TestResolver.InWindow(match, resolved.Window))
            return OutsideWindow(options, query, match, resolved, charset);

        (ReportEnvelope envelope, AnalysisResult analysis) = Analyze(
            options,
            resolved,
            match.Assembly,
            source,
            history: (context, _) => TestHistoryBuilder.Found(context, query, match));

        WriteClockWarning(resolved);

        return await EnrichAndRenderAsync(
            options, envelope, analysis, resolved, startDirectory, store, capabilities, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a name whose test ran, but in none of the runs the narrowed window holds.
    /// </summary>
    /// <remarks>
    /// Over the test's own assembly and that window, which are what the reader asked about: the
    /// name chose the assembly, and the flag chose the window (Amendment 5A). When the flag left no
    /// session at all there is no report to write around the answer, so a script gets the exit
    /// code and nothing on standard output.
    /// </remarks>
    private int OutsideWindow(
        ReportOptions options, string query, TestMatch match, WindowResult resolved, TerminalCharset charset)
    {
        WriteReadWarnings(resolved);

        // The whole line: it echoes the query and a --since value, and carries no command.
        io.Error.WriteLine(TerminalText.Printable(
            resolved.Window is { } window
                ? TestResolver.OutsideWindowMessage(query, match, window)
                : TestResolver.OutsideWindowMessage(query, match, "--since", options.Since),
            charset));

        if (options.Format == ReportFormat.Json && resolved.Window != null)
            RenderUnresolved(options, resolved, match.Assembly, TestHistoryBuilder.NotFound(query));

        return ExitCodes.NotFound;
    }

    /// <summary>
    /// Answers a name that resolved to no test: the message, and for a script the report around it.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="resolution">The ambiguous or empty resolution.</param>
    /// <param name="lookup">What the read the name was resolved over could not use.</param>
    /// <param name="source">Where sessions are read from.</param>
    /// <param name="charset">What the messages may print of a recorded name.</param>
    /// <returns>The exit code.</returns>
    /// <remarks>
    /// The lookup's warnings are repeated because they can be the whole explanation: a test whose
    /// every run sits in an unreadable file is not found, and saying so without the count would
    /// tell the reader it never ran.
    /// </remarks>
    private int Unresolved(
        ReportOptions options,
        TestResolution resolution,
        LookupWarnings lookup,
        LocalSessionSource source,
        TerminalCharset charset)
    {
        // Every candidate's command names its own assembly and nothing of the window, so the scope
        // it is built from needs only the directory.
        (TestHistoryDto history, int exitCode) = resolution.Kind == TestResolutionKind.Ambiguous
            ? (TestHistoryBuilder.Ambiguous(
                    resolution,
                    new ReportScope(
                        options.Assembly, WindowResolution.Store, resolution.RunCount, _shell.Dialect,
                        Directory: options.Directory)),
                ExitCodes.TestAmbiguous)
            : (TestHistoryBuilder.NotFound(resolution.Query), ExitCodes.NotFound);

        WriteLookupWarnings(lookup, charset);

        if (history.Candidates.Count > 0)
            WriteAmbiguous(resolution.Query, history.Candidates, charset);
        else
            io.Error.WriteLine(TerminalText.Printable(TestResolver.NotFoundMessage(resolution), charset));

        if (options.Format != ReportFormat.Json)
            return exitCode;

        // Report's own scope over the --test window (Amendment 5A): the named assembly, or the store's
        // only one. A store with several has no one report to write around the answer (Amendment
        // 12A), and where the scope has no window to open, the answer stands alone rather than being
        // replaced by a failure about a window the reader never asked a question of.
        if ((options.Assembly ?? OnlyAssembly(source)) is { } assembly
            && windowResolver.Resolve(source, TestWindow(options, assembly)) is { Window: not null } resolved)
        {
            RenderUnresolved(options, resolved, assembly, history);
        }

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
        (ReportEnvelope envelope, _) = Analyze(options, resolved, assembly, source: null, history: (_, _) => history);

        new JsonReportRenderer().Render(envelope, io.Output);
    }

    /// <summary>
    /// Works out one test's short fingerprint, for <c>--id</c>'s history command.
    /// </summary>
    /// <remarks>
    /// Over the whole store and not the report's window, because that is what <c>--test</c> resolves
    /// the printed prefix over: one unique among the window's tests could match an older test too.
    /// Read only when the selection is about one test, so any other <c>--id</c> pays nothing.
    /// </remarks>
    private string? ShortFingerprintOf(LocalSessionSource source, string assembly, string fingerprint) =>
        windowResolver.Resolve(source, new WindowRequest(Runs: null, Since: null, assembly, WholeStore: true)).Window
            is { } store
            ? TestResolver.ShortFingerprintOf(store.Sessions, assembly, fingerprint)
            : null;

    /// <summary>
    /// The store's one assembly, or <see langword="null"/> when it holds none or several.
    /// </summary>
    private static string? OnlyAssembly(LocalSessionSource source) =>
        source.KnownAssemblies() is [var only] ? only : null;

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
    /// <param name="source">
    /// Where sessions are read from, for <c>--id</c>'s history command; <see langword="null"/> when
    /// the report answers no id.
    /// </param>
    /// <param name="warnings">Where a provider's failure is said; standard error when omitted.</param>
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
        LocalSessionSource? source,
        TextWriter? warnings = null,
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

        AnalysisResult analysis = coordinator.Run(context, kinds, warnings ?? io.Error);

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
            _shell.Dialect,
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
            scope,
            options.Id,
            history?.Invoke(context, scope),

            // Only when the selection is about one test, so any other report pays nothing for it.
            options.Id != null && source != null && assembly != null
                ? fingerprint => ShortFingerprintOf(source, assembly, fingerprint)
                : null);

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
        OutputCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        AnalysisWindow window = resolved.Window
            ?? throw new ArgumentException("The window did not resolve.", nameof(resolved));

        // After every exit path the local analysis decides, so Cloud can never change one. The
        // one-line summary has no slot for Cloud data, so it is not worth a request.
        if (options.Format != ReportFormat.Summary)
        {
            // Under --id and --test the page holds the selection only. Asking about every finding
            // behind it would spend the budget the subject's run history needs on rows nobody sees.
            IReadOnlyList<Finding> findings = envelope.Selection is null && envelope.Test is null
                ? analysis.Findings
                : [.. analysis.Findings.Where(f => envelope.Findings.Any(shown => shown.Id == f.Id))];

            var request = new EnrichmentRequest(startDirectory, options.Project, SessionPins(window.Sessions), window.Sessions, findings);
            EnrichmentResult enriched = await enricher.EnrichAsync(envelope, request, cancellationToken).ConfigureAwait(false);

            WriteCloudHints(enriched.Hints, capabilities.Glyphs.Charset);
            envelope = enriched.Envelope;
        }

        return Render(options, envelope, analysis, window, store, capabilities);
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
        OutputCapabilities capabilities)
    {
        // A blank line either side, for a person only. It separates the report from the command that
        // produced it and from whatever comes next, which a terminal gives no other help with — and
        // it is decoration, so a redirected stream carries the report and nothing around it.
        bool pad = options.Format == ReportFormat.Text && capabilities.Decorate;

        if (pad)
            io.Output.WriteLine();

        Renderer(options, capabilities).Render(envelope, io.Output);

        // Anything beyond the report itself is for a person at a terminal. A redirected stream is on
        // its way to a clipboard, a file or a script, and none of them asked for an invitation.
        // On standard error even then, so standard output is the report and nothing else (contract
        // F3): a person copying it, or tee-ing it to a file, gets no advertisement with it.
        if (pad)
        {
            WriteCloudInvitation(store, analysis, window.Sessions, envelope, capabilities);

            // After the notices rather than after the report: the trailing blank closes the whole
            // block, and one wedged in the middle of it would separate nothing. The notices sit on
            // the other stream, but at a terminal both land on one screen in this order.
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
    private void WriteCloudHints(IReadOnlyList<CloudHint> hints, TerminalCharset charset)
    {
        foreach (CloudHint hint in hints)
        {
            // The whole line: it can carry an assembly name or an exception's message.
            if (hint.Always || io.IsErrorTerminal || globalOptions.Verbose)
                io.Error.WriteLine(TerminalText.Printable(hint.Text, charset));
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
            ReportFormat.Summary => new SummaryReportRenderer(capabilities.Glyphs.Charset),
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
    /// <param name="result">Why the window could not be opened.</param>
    /// <param name="store">The store that was read.</param>
    /// <param name="assembly">The assembly the reader named, if any.</param>
    /// <param name="charset">What the messages may print of a recorded name.</param>
    /// <param name="warnings">
    /// What the reads behind it could not use, when that was gathered over several reads rather
    /// than carried on <paramref name="result"/>.
    /// </param>
    private int ReportUnavailable(
        WindowResult result, ILocalSessionStore store, string? assembly, TerminalCharset charset, LookupWarnings? warnings = null)
    {
        TextWriter error = io.Error;

        // Every line here echoes a recorded name, the store path or the reader's --since, and none
        // carries a command, so each is cleaned whole.
        string Printable(string text) => TerminalText.Printable(text, charset);

        switch (result.Failure)
        {
            case WindowFailure.NoStore:
                error.WriteLine("No Xping local store found.");
                error.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
                break;

            case WindowFailure.ShaNotFound:
            case WindowFailure.SinceNotUnderstood:
                error.WriteLine(Printable(result.FailureDetail ?? string.Empty));
                break;

            default:
                error.WriteLine(Printable(assembly == null
                    ? $"No runs recorded yet in {store.StorePath}"
                    : $"No runs recorded for assembly '{assembly}' in {store.StorePath}"));

                if (result.FailureDetail is { Length: > 0 } detail)
                    error.WriteLine(Printable(detail));

                error.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
                break;
        }

        if (warnings != null)
            WriteLookupWarnings(warnings, charset);
        else
            WriteReadWarnings(result);

        return ExitCodes.InsufficientData;
    }

    /// <summary>
    /// Says what a read could not use: files it could not read, and runs it could not date.
    /// </summary>
    private void WriteReadWarnings(WindowResult result)
    {
        WriteUnreadableWarning(result.UnreadableSessions);
        WriteClockWarning(result);
    }

    /// <summary>
    /// Says what the <c>--test</c> lookup could not use.
    /// </summary>
    /// <remarks>
    /// The lookup writes no report, so the clock is named as the thing to check rather than as the
    /// one the report is dated against. An assembly whose every run is ahead is named on its own: its
    /// runs were kept, so no count of excluded runs says anything about it.
    /// </remarks>
    private void WriteLookupWarnings(LookupWarnings warnings, TerminalCharset charset)
    {
        WriteUnreadableWarning(warnings.Unreadable);
        WriteClockWarning(warnings.WholeStoreSkewed, warnings.Skewed, reportWritten: false);

        foreach (string assembly in warnings.AheadAssemblies)
        {
            io.Error.WriteLine(
                $"warning: every run of {TerminalText.Printable(assembly, charset)} is stamped ahead of this machine's clock. " +
                "Check the clock on the machine that recorded them.");
        }
    }

    private void WriteUnreadableWarning(int unreadable)
    {
        if (unreadable > 0)
            io.Error.WriteLine($"warning: {unreadable} unreadable run(s) skipped.");
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
    private void WriteClockWarning(WindowResult result) =>
        WriteClockWarning(result.WholeStoreSkewed, result.SkewedSessions, reportWritten: result.Window != null);

    private void WriteClockWarning(bool wholeStoreSkewed, int skewed, bool reportWritten)
    {
        if (wholeStoreSkewed)
        {
            // Said two ways, because this runs on the failure path too. There is no report to be
            // dated against anything when no window resolved, and a warning that describes one is
            // the wrong thing to hand a developer who was just told they have nothing to look at.
            io.Error.WriteLine(
                "warning: every recorded run is stamped ahead of this machine's clock. " +
                (reportWritten
                    ? "The report is dated against the clock that recorded them."
                    : "Check the clock on the machine that recorded them."));
            return;
        }

        if (skewed > 0)
        {
            io.Error.WriteLine(
                $"warning: {skewed} run(s) stamped ahead of this machine's clock " +
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
    private int ReportNothingAttributable(ILocalSessionStore store, TerminalCharset charset)
    {
        TextWriter error = io.Error;

        error.WriteLine($"No runs in {TerminalText.Printable(store.StorePath ?? string.Empty, charset)} record which test assembly they belong to.");
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
    /// <param name="charset">What the list may print of a recorded name.</param>
    /// <remarks>
    /// Capped, because a name like <c>Add</c> can match a parameterised test's every case, and past
    /// ten the reader needs a longer name rather than a longer list. Each name is broken as the
    /// report breaks one, so the list survives being pasted into a chat.
    /// </remarks>
    private void WriteAmbiguous(string query, IReadOnlyList<TestCandidateDto> candidates, TerminalCharset charset)
    {
        const int Shown = 10;
        const int NameIndent = 4;

        TextWriter error = io.Error;

        string Recorded(string value) => TerminalText.Printable(value, charset);

        error.WriteLine($"'{Recorded(query)}' matches {candidates.Count} tests. Name one by its fingerprint:");

        foreach (TestCandidateDto candidate in candidates.Take(Shown))
        {
            error.WriteLine($"  {Recorded(candidate.FingerprintPrefix)}  {Recorded(candidate.Assembly)}");

            foreach (string line in TextReportRenderer.WrapValue(
                Recorded(candidate.FullyQualifiedName), TextReportRenderer.FenceWidth - NameIndent))
            {
                error.WriteLine(new string(' ', NameIndent) + line);
            }
        }

        if (candidates.Count > Shown)
            error.WriteLine($"  +{candidates.Count - Shown} more");

        error.WriteLine(TerminalText.Printable(candidates[0].Command, TerminalCharset.Command));
    }

    /// <summary>
    /// Says that the finding <c>--id</c> named is not in this report, and where its subject went.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Not reported in this window", never "not found" or "error": an id that was on screen
    /// yesterday and is absent today is the report working. The window is named because it is the
    /// honest boundary — a finding that needs twenty runs is absent from five.
    /// </para>
    /// <para>
    /// Names the current id rather than showing it. A promoted cluster is a different claim, and
    /// rendering it under the id of the one it replaced would say otherwise.
    /// </para>
    /// </remarks>
    private void WriteNotReported(
        SelectionDto selection, ReportEnvelope envelope, string? assembly, TerminalCharset charset)
    {
        string Recorded(string value) => TerminalText.Printable(value, charset);

        var message = new StringBuilder("Finding ").Append(Recorded(selection.Id));

        if (selection.Kind is { } kind)
            message.Append(" (").Append(Recorded(ReportVocabulary.LabelFor(kind))).Append(')');

        int sessions = envelope.Window.SessionCount;
        string runs = sessions == 1 ? "1 run" : $"{sessions} runs";

        message.Append(" is not reported in this window (")
               .Append(assembly == null ? runs : $"{Recorded(assembly)}, {runs}")
               .Append(").");

        if (selection.SameSubject.Count > 0)
        {
            message.Append(" Its subject is reported as ")
                   .AppendJoin(", ", selection.SameSubject.Select(same =>
                       $"{Recorded(same.Id)} ({Recorded(ReportVocabulary.LabelFor(same.Kind))}, row {same.Row})"))
                   .Append('.');
        }

        io.Error.WriteLine(message.ToString());

        // A line of its own, ending on the command: anything after it would be copied with it and
        // run as arguments.
        if (selection.SameSubject.Count == 0 && selection.History is { } history)
            io.Error.WriteLine(
                $"Its test has no findings in this window: {TerminalText.Printable(history, TerminalCharset.Command)}");
    }

    /// <summary>
    /// Says that no assembly's window reports the finding <c>--id</c> named, and where its subject
    /// went in each assembly that knows it.
    /// </summary>
    /// <remarks>
    /// Names every assembly as the window, because every one was searched (finding-detail
    /// Amendment 10B). A row number or a history command belongs to one assembly's report, so each
    /// is said with the assembly it is from.
    /// </remarks>
    private void WriteNotReportedAnywhere(string id, IReadOnlyList<AssemblyAnalysis> analyses, TerminalCharset charset)
    {
        string Recorded(string value) => TerminalText.Printable(value, charset);

        List<(string Assembly, SelectionDto Selection)> selections = [.. analyses
            .Where(a => a.Envelope?.Selection != null)
            .Select(a => (a.Assembly, a.Envelope!.Selection!))
            .OrderBy(a => a.Assembly, StringComparer.Ordinal)];

        var message = new StringBuilder("Finding ")
            .Append(Recorded(selections.Count > 0 ? selections[0].Selection.Id : id));

        if (selections.Select(s => s.Selection.Kind).FirstOrDefault(k => k != null) is { } kind)
            message.Append(" (").Append(Recorded(ReportVocabulary.LabelFor(kind))).Append(')');

        message.Append(" is not reported in any window (every assembly in this store).");

        foreach ((string assembly, SelectionDto selection) in selections.Where(s => s.Selection.SameSubject.Count > 0))
        {
            message.Append(" In ").Append(Recorded(assembly)).Append(", its subject is reported as ")
                   .AppendJoin(", ", selection.SameSubject.Select(same =>
                       $"{Recorded(same.Id)} ({Recorded(ReportVocabulary.LabelFor(same.Kind))}, row {same.Row})"))
                   .Append('.');
        }

        io.Error.WriteLine(message.ToString());

        // Each a line of its own, ending on the command, as WriteNotReported writes it.
        foreach ((string assembly, SelectionDto selection) in selections)
        {
            if (selection.SameSubject.Count == 0 && selection.History is { } history)
                io.Error.WriteLine(
                    $"Its test has no findings in {Recorded(assembly)}: {TerminalText.Printable(history, TerminalCharset.Command)}");
        }
    }

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

        if (!ctaThrottle.ShouldShow(store.StorePath, analysis.Findings.Count > 0, isConnected))
            return;

        io.Error.WriteLine();
        io.Error.WriteLine(
            "See flakiness across CI and your whole team " +
            $"{capabilities.Glyphs.Arrow} https://xping.io/start");
    }
}
