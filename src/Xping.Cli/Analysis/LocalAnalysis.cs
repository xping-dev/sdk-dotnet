/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Analysis;

/// <summary>
/// Why a test was flagged as unstable. Ordered by how strongly it implies flakiness.
/// </summary>
internal enum InstabilityKind
{
    /// <summary>
    /// The test failed and then passed on a later attempt within a single run.
    /// </summary>
    /// <remarks>
    /// The strongest available signal, and the only one that works on a developer's first run,
    /// before any cross-run history exists.
    /// </remarks>
    FlakedInRun = 0,

    /// <summary>
    /// The test both passed and failed across the analysed runs.
    /// </summary>
    FlakyAcrossRuns = 1,

    /// <summary>
    /// The test passed in every earlier run and failed in the most recent one.
    /// </summary>
    NewlyFailing = 2,

    /// <summary>
    /// The test failed in every analysed run. Not flaky — most likely a real defect.
    /// </summary>
    ConsistentlyFailing = 3
}

/// <summary>
/// A test flagged by local analysis, with the history behind the flag.
/// </summary>
internal sealed class UnstableTest
{
    /// <summary>Gets the stable test fingerprint.</summary>
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets why the test was flagged.</summary>
    public InstabilityKind Kind { get; init; }

    /// <summary>
    /// Gets the per-run outcomes in chronological order, oldest first.
    /// <see langword="true"/> is a pass. Runs in which the test did not appear are omitted.
    /// </summary>
    public IReadOnlyList<bool> History { get; init; } = [];

    /// <summary>Gets the number of analysed runs in which the test passed.</summary>
    public int PassCount { get; init; }

    /// <summary>Gets the number of analysed runs in which the test appeared.</summary>
    public int RunCount { get; init; }

    /// <summary>
    /// Gets the retry attempt on which the test passed, when <see cref="Kind"/> is
    /// <see cref="InstabilityKind.FlakedInRun"/>.
    /// </summary>
    public int? PassedOnAttempt { get; init; }

    /// <summary>Gets the maximum retries configured, when known.</summary>
    public int? MaxAttempts { get; init; }

    /// <summary>
    /// Gets the test assembly this finding belongs to. Set when findings from several assemblies
    /// are merged; <see langword="null"/> for a single-assembly report.
    /// </summary>
    public string? Assembly { get; init; }

    /// <summary>Returns a copy of this finding tagged with its assembly.</summary>
    public UnstableTest WithAssembly(string? assembly) => new()
    {
        Fingerprint = Fingerprint,
        Name = Name,
        Kind = Kind,
        History = History,
        PassCount = PassCount,
        RunCount = RunCount,
        PassedOnAttempt = PassedOnAttempt,
        MaxAttempts = MaxAttempts,
        Assembly = assembly
    };
}

/// <summary>
/// The result of analysing recent local runs.
/// </summary>
internal sealed class LocalAnalysis
{
    /// <summary>Gets an empty analysis.</summary>
    public static LocalAnalysis Empty { get; } = new();

    /// <summary>Gets the tests flagged as unstable, most significant first.</summary>
    public IReadOnlyList<UnstableTest> UnstableTests { get; init; } = [];

    /// <summary>Gets the tests that failed in every analysed run.</summary>
    public IReadOnlyList<UnstableTest> ConsistentFailures { get; init; } = [];

    /// <summary>Gets the number of runs the analysis covered.</summary>
    public int RunsAnalysed { get; init; }

    /// <summary>
    /// Gets the number of test assemblies covered. Greater than one only for an aggregate report.
    /// </summary>
    public int AssembliesAnalysed { get; init; } = 1;

    /// <summary>
    /// Gets the number of runs needed before cross-run analysis becomes meaningful.
    /// </summary>
    public int MinimumRunsForHistory { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is enough history for cross-run analysis.
    /// </summary>
    public bool HasSufficientHistory => RunsAnalysed >= MinimumRunsForHistory;

    /// <summary>
    /// Gets a value indicating whether the analysis found anything worth showing.
    /// </summary>
    public bool HasFindings => UnstableTests.Count > 0 || ConsistentFailures.Count > 0;
}
