/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;

namespace Xping.Cli.Analysis;

/// <summary>
/// Derives local instability signals from recent runs.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately narrow. This does not reproduce the platform's confidence score, which weighs six
/// factors and needs at least ten executions plus cross-environment data. Local history structurally
/// cannot contain CI runs, other machines or teammates' runs, so attempting a score here would
/// produce a number that looks like the dashboard's and disagrees with it.
/// </para>
/// <para>
/// What it answers instead: "what is unstable on my machine, in my last N runs".
/// </para>
/// </remarks>
internal static class LocalFlakinessAnalyzer
{
    /// <summary>Minimum runs before cross-run comparisons carry any weight.</summary>
    internal const int MinimumRunsForHistory = 3;

    /// <summary>Cap on how many unstable tests the report lists.</summary>
    internal const int MaxReportedTests = 5;

    /// <summary>
    /// Analyses runs in chronological order, oldest first.
    /// </summary>
    /// <param name="runs">Runs to analyse, oldest first. The last entry is the current run.</param>
    /// <returns>The analysis result.</returns>
    public static LocalAnalysis Analyze(IReadOnlyList<LocalRun> runs)
    {
        if (runs == null || runs.Count == 0)
            return LocalAnalysis.Empty;

        Dictionary<string, TestHistory> histories = BuildHistories(runs);

        var unstable = new List<UnstableTest>();
        var consistentFailures = new List<UnstableTest>();

        foreach (TestHistory history in histories.Values)
        {
            if (history.Outcomes.Count == 0)
                continue;

            UnstableTest? finding = Classify(history, runs.Count);
            if (finding == null)
                continue;

            if (finding.Kind == InstabilityKind.ConsistentlyFailing)
                consistentFailures.Add(finding);
            else
                unstable.Add(finding);
        }

        return new LocalAnalysis
        {
            UnstableTests = unstable
                .OrderBy(t => (int)t.Kind)
                .ThenBy(t => t.PassCount)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .Take(MaxReportedTests)
                .ToList(),
            ConsistentFailures = consistentFailures
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList(),
            RunsAnalysed = runs.Count,
            MinimumRunsForHistory = MinimumRunsForHistory
        };
    }

    private static Dictionary<string, TestHistory> BuildHistories(IReadOnlyList<LocalRun> runs)
    {
        var histories = new Dictionary<string, TestHistory>(StringComparer.Ordinal);

        foreach (LocalRun run in runs)
        {
            // Within one run a fingerprint can appear several times (retry attempts). Collapse them
            // to a single per-run outcome so the history has exactly one entry per run.
            var perRun = new Dictionary<string, LocalTestRecord>(StringComparer.Ordinal);

            foreach (LocalTestRecord record in run.Records)
            {
                if (string.IsNullOrEmpty(record.Fingerprint))
                    continue;

                // Skipped and not-executed tests carry no reliability signal.
                if (record.Outcome is OutcomeCodes.Skipped or OutcomeCodes.NotExecuted)
                    continue;

                if (!perRun.TryGetValue(record.Fingerprint, out LocalTestRecord? existing) ||
                    record.Attempt >= existing.Attempt)
                {
                    perRun[record.Fingerprint] = record;
                }
            }

            foreach (KeyValuePair<string, LocalTestRecord> entry in perRun)
            {
                if (!histories.TryGetValue(entry.Key, out TestHistory? history))
                {
                    history = new TestHistory { Name = entry.Value.Name };
                    histories[entry.Key] = history;
                }

                // Keep the most recent display name; parameterised names can change between runs.
                history.Name = entry.Value.Name;
                history.Fingerprint = entry.Key;
                history.Outcomes.Add(entry.Value.Outcome == OutcomeCodes.Passed);

                if (entry.Value.PassedOnRetry)
                {
                    history.FlakedInLatestRun = true;
                    history.PassedOnAttempt = entry.Value.Attempt;
                }
                else
                {
                    history.FlakedInLatestRun = false;
                    history.PassedOnAttempt = null;
                }
            }
        }

        return histories;
    }

    private static UnstableTest? Classify(TestHistory history, int totalRuns)
    {
        int passes = history.Outcomes.Count(o => o);
        int runCount = history.Outcomes.Count;
        bool latestFailed = !history.Outcomes[runCount - 1];

        UnstableTest Build(InstabilityKind kind) => new()
        {
            Fingerprint = history.Fingerprint,
            Name = history.Name,
            Kind = kind,
            History = history.Outcomes.ToList(),
            PassCount = passes,
            RunCount = runCount,
            PassedOnAttempt = kind == InstabilityKind.FlakedInRun ? history.PassedOnAttempt : null
        };

        // Retry flakiness in the current run outranks everything: it is direct evidence of
        // non-determinism, not an inference from a pattern.
        if (history.FlakedInLatestRun)
            return Build(InstabilityKind.FlakedInRun);

        // Everything below compares runs against each other and needs a minimum of history.
        if (totalRuns < MinimumRunsForHistory || runCount < 2)
            return null;

        if (passes == 0)
            return Build(InstabilityKind.ConsistentlyFailing);

        if (passes == runCount)
            return null;

        // Failing now after an unbroken run of passes reads as a regression, not as flakiness.
        if (latestFailed && passes == runCount - 1)
            return Build(InstabilityKind.NewlyFailing);

        return Build(InstabilityKind.FlakyAcrossRuns);
    }

    private sealed class TestHistory
    {
        public string Fingerprint { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<bool> Outcomes { get; } = [];

        public bool FlakedInLatestRun { get; set; }

        public int? PassedOnAttempt { get; set; }
    }
}
