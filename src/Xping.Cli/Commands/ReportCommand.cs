/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Analysis;
using Xping.Cli.Reporting;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Commands;

/// <summary>
/// Renders the local flakiness report from the run store.
/// </summary>
internal static class ReportCommand
{
    /// <summary>
    /// Runs the report.
    /// </summary>
    /// <param name="options">Parsed command-line options.</param>
    /// <param name="output">Where to write the report.</param>
    /// <returns>Process exit code.</returns>
    public static int Run(ReportOptions options, TextWriter output)
    {
        ILocalRunStore store = LocalRunStore.Create(
            new LocalStoreOptions { AnalysisWindow = options.Last },
            logger: null,
            startDirectory: options.Directory ?? Directory.GetCurrentDirectory());

        if (!store.IsAvailable)
        {
            output.WriteLine("No Xping local store found.");
            output.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
            return 1;
        }

        // Every test project in a solution shares one store. Reporting across them would produce a
        // block whose headline counts describe one run while its history spans several unrelated
        // suites, so scope to a single assembly — the caller's choice, or the most recent one.
        string? assembly = options.Assembly ?? NewestAssembly(store);

        IReadOnlyList<LocalRun> runs = store.ReadRecent(options.Last, assembly);

        if (runs.Count == 0)
        {
            output.WriteLine(options.Assembly == null
                ? $"No runs recorded yet in {store.StorePath}"
                : $"No runs recorded for assembly '{options.Assembly}' in {store.StorePath}");
            output.WriteLine("Run your tests once with the Xping SDK installed, then try again.");
            return 1;
        }

        WriteScopeNotice(store, assembly, options.Assembly != null, output);

        LocalAnalysis analysis = LocalFlakinessAnalyzer.Analyze(runs);
        LocalRun latest = runs[runs.Count - 1];

        string? report = LocalRunReportWriter.Render(
            BuildStatistics(latest),
            analysis,
            options.Ascii ? ReportGlyphs.Ascii : ReportGlyphs.Unicode,
            CtaThrottle.ShouldShow(store.StorePath, analysis.HasFindings, isConnected: false),
            store.StorePath);

        if (report != null)
            output.Write(report);

        if (options.Details)
            WriteDetails(analysis, runs, output);

        return 0;
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
