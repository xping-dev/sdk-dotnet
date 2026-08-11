/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Hosting;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Report.Store;
using Xping.Cli.Report.Windowing;
using Xping.Cli.Reporting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Commands;

/// <summary>
/// Runs local analysis and renders the result.
/// </summary>
/// <remarks>
/// Wiring only: resolve a window, build the context, run the providers, render, choose an exit code.
/// Every decision worth arguing about lives in one of those steps rather than here, which is what
/// lets each be tested without a command line.
/// </remarks>
internal sealed class ReportCommand(
    ILocalSessionStoreFactory sessionStoreFactory,
    ILocalRunStoreFactory runStoreFactory,
    IWindowResolver windowResolver,
    FindingCoordinator coordinator,
    ConsoleIO io)
{
    /// <summary>
    /// Runs the report.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <returns>Process exit code.</returns>
    public int Run(ReportOptions options)
    {
        string startDirectory = options.Directory ?? Directory.GetCurrentDirectory();

        ILocalSessionStore store = sessionStoreFactory.Create(startDirectory: startDirectory);
        var source = new LocalSessionSource(store);

        // Auto-scope to the newest assembly unless the caller named one. Every test project in a
        // solution shares one store, so an unscoped report would pool unrelated suites: one
        // assembly's history would contain another's tests and its session count would be the
        // solution-wide total.
        string? assembly = options.Assembly ?? source.NewestAssembly();

        WindowResult resolved = windowResolver.Resolve(
            source, new WindowRequest(options.Runs, options.Since, assembly));

        if (resolved.Window == null)
            return ReportUnavailable(resolved, store, options);

        var context = new AnalysisContext(
            resolved.Window, RevisionContext.FromNewest(resolved.Window.Sessions));

        IReadOnlySet<FindingKind>? kinds =
            options.Kinds.Count == 0 ? null : options.Kinds.ToHashSet();

        AnalysisResult analysis = coordinator.Run(context, kinds, io.Error);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context,
            analysis,

            // A session that never finalised is never written, so the only way to lose one is for
            // its file to be unreadable — which is counted separately. This stays zero until the
            // store gains a way to persist a run that did not finish.
            incompleteSessions: 0,
            resolved.UnreadableSessions,
            options.Top);

        Renderer(options).Render(envelope, io.Output);

        if (options.Format == ReportFormat.Text)
            WriteScopeNotice(source, assembly, options.Assembly != null);

        if (options.Format == ReportFormat.Text)
            WriteCloudInvitation(store, analysis, startDirectory, options);

        return ExitCodes.ForReport(analysis.Findings, options.FailOn);
    }

    private static IReportRenderer Renderer(ReportOptions options) => options.Format switch
    {
        ReportFormat.Json => new JsonReportRenderer(),

        // --ascii forces the fallback; otherwise ask the terminal. Assuming Unicode would render as
        // mojibake on a console still using a legacy code page.
        _ => new TextReportRenderer(options.Ascii ? ReportGlyphs.Ascii : ReportGlyphs.Detect())
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

        return ExitCodes.InsufficientData;
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

        int others = source.KnownAssemblies()
            .Count(a => !string.Equals(a, assembly, StringComparison.Ordinal));

        if (others == 0)
            return;

        io.Output.WriteLine(
            $"Reporting on {assembly} {(char)0x00B7} {others} other " +
            (others == 1 ? "assembly" : "assemblies") +
            " in this store (use --assembly to switch).");
    }

    /// <summary>
    /// Prints the cloud invitation, at most once a day and only when it is relevant.
    /// </summary>
    /// <remarks>
    /// Whether a project is already cloud-connected is recorded on the run tier rather than on the
    /// session itself, so this reads one run header. Skipping that read would mean pitching the
    /// cloud to people who already pay for it.
    /// </remarks>
    private void WriteCloudInvitation(
        ILocalSessionStore store, AnalysisResult analysis, string startDirectory, ReportOptions options)
    {
        bool isConnected = runStoreFactory
            .Create(startDirectory: startDirectory)
            .ReadRecent(1)
            .Any(run => run.Header.IsConnected);

        if (!CtaThrottle.ShouldShow(store.StorePath, analysis.Findings.Count > 0, isConnected))
            return;

        ReportGlyphs glyphs = options.Ascii ? ReportGlyphs.Ascii : ReportGlyphs.Detect();

        io.Output.WriteLine();
        io.Output.WriteLine(
            $"See flakiness across CI and your whole team {glyphs.Arrow} https://xping.io/start");
    }
}
