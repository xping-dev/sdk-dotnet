/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;

namespace Xping.Cli.Analysis;

/// <summary>
/// Analyses several test assemblies and merges the findings into one report.
/// </summary>
/// <remarks>
/// Each assembly is analysed against its own runs and the findings are merged afterwards, rather
/// than pooling every run into a single analysis. Pooling would corrupt the arithmetic: a suite seen
/// in 12 of 36 runs would be described against a 36-run window it never took part in, and "last N
/// runs" would mean something different for every test in the block.
/// </remarks>
internal static class AggregateAnalyzer
{
    /// <summary>
    /// Groups runs by assembly, analyses each independently, and merges the results.
    /// </summary>
    /// <param name="runs">All runs to consider, in chronological order.</param>
    /// <returns>A merged analysis tagged with per-finding assembly names.</returns>
    public static LocalAnalysis Analyze(IReadOnlyList<LocalRun> runs)
    {
        if (runs == null || runs.Count == 0)
            return LocalAnalysis.Empty;

        var byAssembly = new Dictionary<string, List<LocalRun>>(StringComparer.Ordinal);

        foreach (LocalRun run in runs)
        {
            string key = run.Header.Assembly ?? string.Empty;

            if (!byAssembly.TryGetValue(key, out List<LocalRun>? bucket))
            {
                bucket = [];
                byAssembly[key] = bucket;
            }

            bucket.Add(run);
        }

        var unstable = new List<UnstableTest>();
        var consistent = new List<UnstableTest>();
        int maxWindow = 0;

        foreach (KeyValuePair<string, List<LocalRun>> entry in byAssembly)
        {
            LocalAnalysis analysis = LocalFlakinessAnalyzer.Analyze(entry.Value);
            string? assembly = entry.Key.Length == 0 ? null : entry.Key;

            unstable.AddRange(analysis.UnstableTests.Select(t => t.WithAssembly(assembly)));
            consistent.AddRange(analysis.ConsistentFailures.Select(t => t.WithAssembly(assembly)));

            // The window differs per assembly; report the largest so the header is not misleading
            // about how much history any single finding is based on.
            maxWindow = Math.Max(maxWindow, analysis.RunsAnalysed);
        }

        return new LocalAnalysis
        {
            UnstableTests = unstable
                .OrderBy(t => (int)t.Kind)
                .ThenBy(t => t.PassCount)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .Take(LocalFlakinessAnalyzer.MaxReportedTests)
                .ToList(),
            ConsistentFailures = consistent
                .OrderBy(t => t.Assembly, StringComparer.Ordinal)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .ToList(),
            RunsAnalysed = runs.Count,
            AssembliesAnalysed = byAssembly.Count,
            MinimumRunsForHistory = LocalFlakinessAnalyzer.MinimumRunsForHistory
        };
    }
}
