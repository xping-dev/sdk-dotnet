/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Analysis;
using Xping.Cli.Hosting;
using Xping.Cli.Reporting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Commands;

/// <summary>
/// Renders the local flakiness report from the run store.
/// </summary>
internal sealed class ReportCommand(ILocalRunStoreFactory storeFactory, ConsoleIO io)
{
    /// <summary>
    /// Runs the report.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <returns>Process exit code.</returns>
    public int Run(ReportOptions options)
    {
        TextWriter output = io.Output;

        ILocalRunStore store = storeFactory.Create(
            new LocalStoreOptions { AnalysisWindow = options.Last },
            startDirectory: options.Directory ?? Directory.GetCurrentDirectory());

        if (!store.IsAvailable)
        {
            output.WriteLine("No Xping local store found.");
            output.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
            return 1;
        }

        // Every test project in a solution shares one store. A scoped report describes one suite;
        // --all describes the solution and uses a header that does not pretend otherwise.
        string? assembly = options.All ? null : options.Assembly ?? NewestAssembly(store);

        // In aggregate mode --last bounds each assembly's window, not the total. Reading N x
        // assemblies globally does not achieve that: ReadRecent returns the newest runs overall, so
        // one busy suite owning the newest runs would push another suite out of the window
        // entirely. Read each assembly's window separately instead.
        IReadOnlyList<LocalRun> runs = options.All
            ? ReadPerAssembly(store, options.Last)
            : store.ReadRecent(options.Last, assembly);

        if (runs.Count == 0)
        {
            output.WriteLine(options.Assembly == null
                ? $"No runs recorded yet in {store.StorePath}"
                : $"No runs recorded for assembly '{options.Assembly}' in {store.StorePath}");
            output.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
            return 1;
        }

        LocalAnalysis analysis = options.All
            ? AggregateAnalyzer.Analyze(runs)
            : LocalFlakinessAnalyzer.Analyze(runs);

        if (options.Json)
        {
            output.WriteLine(JsonReportWriter.Write(analysis, store.StorePath, assembly, runs));
            return 0;
        }

        if (!options.All)
            WriteScopeNotice(store, assembly, options.Assembly != null, output);

        // The invitation belongs to a human-readable report; JSON consumers are scripts.
        // A project that has uploaded at any point in the window is already a customer.
        bool isConnected = runs.Any(r => r.Header.IsConnected);

        bool showCta = CtaThrottle.ShouldShow(store.StorePath, analysis.HasFindings, isConnected);

        // --ascii forces ASCII; otherwise ask the terminal. Assuming Unicode would render as
        // mojibake on a console still using a legacy code page.
        ReportGlyphs glyphs = options.Ascii ? ReportGlyphs.Ascii : ReportGlyphs.Detect();

        string? report = options.All
            ? LocalRunReportWriter.RenderAggregate(analysis, glyphs, showCta, store.StorePath)
            : LocalRunReportWriter.Render(
                BuildStatistics(runs[runs.Count - 1]), analysis, glyphs, showCta, store.StorePath);

        if (report != null)
            output.Write(report);

        if (options.Details)
            WriteDetails(analysis, runs, output);

        return 0;
    }

    /// <summary>
    /// Reads the newest <paramref name="last"/> runs for every assembly in the store.
    /// </summary>
    /// <remarks>
    /// Each assembly gets its own window, so a suite that runs rarely is still represented even when
    /// another suite owns every one of the newest runs.
    /// </remarks>
    private static List<LocalRun> ReadPerAssembly(ILocalRunStore store, int last)
    {
        // A generous probe purely to discover which assemblies exist; the per-assembly reads below
        // are what actually bound the analysis window.
        const int DiscoveryWindow = 500;

        var assemblies = store.ReadRecent(DiscoveryWindow)
            .Select(r => r.Header.Assembly)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (assemblies.Count == 0)
            return [];

        var runs = new List<LocalRun>();
        foreach (string? assembly in assemblies)
            runs.AddRange(store.ReadRecent(last, assembly));

        // Merge back into one chronological sequence so downstream ordering assumptions hold.
        return runs.OrderBy(r => r.Header.StartedAtUtc).ToList();
    }

    /// <summary>
    /// Returns the assembly of the most recently recorded run.
    /// </summary>
    private static string? NewestAssembly(ILocalRunStore store)
    {
        IReadOnlyList<LocalRun> newest = store.ReadRecent(1);
        return newest.Count == 0 ? null : newest[0].Header.Assembly;
    }

    /// <summary>
    /// Says which assembly the report covers when the store holds more than one.
    /// </summary>
    /// <remarks>
    /// Auto-scoping keeps the numbers honest, but silently omitting other suites would be worse than
    /// the problem it solves. Naming the scope makes the omission visible and points at the flag.
    /// </remarks>
    private static void WriteScopeNotice(
        ILocalRunStore store, string? assembly, bool explicitlyChosen, TextWriter output)
    {
        if (assembly == null || explicitlyChosen)
            return;

        // A generous window: enough to notice siblings without reading the whole store.
        const int ProbeWindow = 50;

        int others = store.ReadRecent(ProbeWindow)
            .Select(r => r.Header.Assembly)
            .Where(a => a != null && !string.Equals(a, assembly, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (others == 0)
            return;

        output.WriteLine(
            $"Reporting on {assembly} · {others} other " +
            (others == 1 ? "assembly" : "assemblies") +
            " in this store (use --assembly to switch).");
    }

    /// <summary>
    /// Derives headline counts for the most recent run.
    /// </summary>
    /// <remarks>
    /// Reconstructed from stored records rather than read from the session: the store deliberately
    /// keeps only what local analysis needs, and <c>QuickStatistics</c> is not part of that.
    /// </remarks>
    private static QuickStatistics BuildStatistics(LocalRun run)
    {
        int passed = 0, failed = 0, skipped = 0;

        foreach (LocalTestRecord record in run.Records)
        {
            switch (record.Outcome)
            {
                case OutcomeCodes.Passed: passed++; break;
                case OutcomeCodes.Failed: failed++; break;
                case OutcomeCodes.Skipped: skipped++; break;
                default: break;
            }
        }

        return new QuickStatistics
        {
            Total = run.Records.Count,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            WallClockDurationMs = run.Header.DurationMs
        };
    }

    private static void WriteDetails(
        LocalAnalysis analysis, IReadOnlyList<LocalRun> runs, TextWriter output)
    {
        if (!analysis.HasFindings)
            return;

        output.WriteLine();
        output.WriteLine("Details");
        output.WriteLine();

        foreach (UnstableTest test in analysis.UnstableTests.Concat(analysis.ConsistentFailures))
        {
            output.WriteLine($"  {test.Name}");
            output.WriteLine($"    fingerprint  {test.Fingerprint}");
            output.WriteLine($"    passed       {test.PassCount} of {test.RunCount} runs");

            var sb = new StringBuilder();
            foreach (LocalRun run in runs)
            {
                LocalTestRecord? record = run.Records
                    .Where(r => string.Equals(r.Fingerprint, test.Fingerprint, StringComparison.Ordinal))
                    .OrderByDescending(r => r.Attempt)
                    .FirstOrDefault();

                if (record == null)
                    continue;

                sb.Append("      ")
                  .Append(run.Header.StartedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                  .Append("  ")
                  .Append(record.Outcome == OutcomeCodes.Passed ? "pass" : "FAIL")
                  .Append("  ")
                  .Append(record.DurationMs.ToString(CultureInfo.InvariantCulture))
                  .Append("ms");

                if (record.PassedOnRetry)
                    sb.Append("  (passed on attempt ").Append(record.Attempt).Append(')');

                if (run.Header.Branch is { Length: > 0 })
                    sb.Append("  ").Append(run.Header.Branch);

                sb.AppendLine();
            }

            output.Write(sb.ToString());
            output.WriteLine();
        }
    }
}
