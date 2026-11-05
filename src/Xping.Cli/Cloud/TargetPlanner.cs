/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.RegularExpressions;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Cloud;

/// <summary>
/// One test the report asks Xping Cloud about.
/// </summary>
/// <param name="Assembly">The test's assembly, which decides its project.</param>
/// <param name="Fingerprint">The test.</param>
/// <param name="Hashes">The signatures this machine saw for it, at most <see cref="TargetPlanner.MaxHashesPerTest"/>.</param>
/// <param name="LatestFailureHash">
/// The message hash of its final attempt in the newest session when that attempt failed; null when
/// it did not fail there or recorded no message.
/// </param>
internal sealed record CloudTarget(string Assembly, string Fingerprint, IReadOnlyList<string> Hashes, string? LatestFailureHash);

/// <summary>
/// What the report asks Xping Cloud.
/// </summary>
/// <param name="Targets">The tests, in contract §7.2 order.</param>
/// <param name="SessionKeys">The newest session's key of each assembly.</param>
internal sealed record TargetPlan(IReadOnlyList<CloudTarget> Targets, IReadOnlyDictionary<string, string> SessionKeys);

/// <summary>
/// Chooses the tests a report asks Xping Cloud about, and what it tells Cloud of each
/// (cloud-data-in-cli-cli-spec §4.1, §4.2).
/// </summary>
/// <remarks>
/// Pure: everything comes from the envelope, the ranked findings and the window's sessions, so the
/// same store always asks the same questions in the same order.
/// </remarks>
internal static partial class TargetPlanner
{
    /// <summary>
    /// The most tests one report asks about (contract §7.2).
    /// </summary>
    public const int MaxEnrichedTests = 200;

    /// <summary>
    /// The most signatures sent per test (contract §6.1).
    /// </summary>
    public const int MaxHashesPerTest = 3;

    /// <summary>
    /// The longest fingerprint the digest accepts (contract §6.1, §9.3).
    /// </summary>
    public const int MaxFingerprintLength = 128;

    /// <summary>
    /// Plans the questions for one report.
    /// </summary>
    /// <param name="envelope">The report, with its <c>--test</c> subject and latest run.</param>
    /// <param name="findings">Every finding, ranked, before <c>--top</c> cut the envelope's list.</param>
    /// <param name="sessions">The window's sessions, newest first.</param>
    /// <returns>The targets and session keys.</returns>
    public static TargetPlan Plan(ReportEnvelope envelope, IReadOnlyList<Finding> findings, IReadOnlyList<TestSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(sessions);

        // One host run writes one session per assembly under one key, so the newest run can be
        // several sessions.
        List<TestSession> latest = sessions.Count == 0 ? [] : [.. sessions.Where(s => s.SessionId == sessions[0].SessionId)];
        Dictionary<string, List<TestExecution>> failures = FailuresByTest(sessions);
        HashSet<Guid> latestIds = [.. latest.SelectMany(s => s.Executions).Select(e => e.ExecutionId)];
        Dictionary<string, TestExecution> latestFinal = FinalAttempts(latest);

        IEnumerable<(string? Assembly, string? Fingerprint)> ordered =
        [
            .. Subjects(envelope.Test?.Test),
            .. findings
                .Select(f => f.Subject)
                .OfType<FindingSubject.SingleTest>()
                .Select(s => ((string?)s.Test.Assembly, (string?)s.Test.TestFingerprint)),
            .. envelope.LatestRun?.Failures.Select(f => (f.Subject.Assembly, f.Subject.Fingerprint)) ?? [],
            .. latestFinal.Values
                .Where(e => e.Outcome.IsFailure())
                .OrderBy(e => e.Identity.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(e => e.Identity.TestFingerprint, StringComparer.Ordinal)
                .Select(e => ((string?)e.Identity.Assembly, (string?)e.Identity.TestFingerprint))
        ];

        // A fingerprint longer than the digest accepts would cost its whole batch a 400; Cloud cannot
        // know such a test anyway (contract §9.3), so it is not asked about.
        List<CloudTarget> targets =
        [
            .. ordered
                .Where(t => t.Assembly is { Length: > 0 } && t.Fingerprint is { Length: > 0 and <= MaxFingerprintLength })
                .DistinctBy(t => t.Fingerprint, StringComparer.Ordinal)
                .Take(MaxEnrichedTests)
                .Select(t => new CloudTarget(
                    t.Assembly!,
                    t.Fingerprint!,
                    Hashes(failures.GetValueOrDefault(t.Fingerprint!) ?? [], latestIds),
                    latestFinal.TryGetValue(t.Fingerprint!, out TestExecution? final) && final.Outcome.IsFailure() && IsHash(final.ErrorMessageHash)
                        ? final.ErrorMessageHash
                        : null))
        ];

        return new TargetPlan(targets, SessionKeys(sessions));
    }

    private static IEnumerable<(string? Assembly, string? Fingerprint)> Subjects(SubjectDto? subject) =>
        subject is null ? [] : [(subject.Assembly, subject.Fingerprint)];

    /// <summary>
    /// The hashes of the newest session's failures first, newest first; then the most frequent over
    /// the window, ordinally smallest on a tie so the request does not depend on read order.
    /// </summary>
    private static List<string> Hashes(List<TestExecution> failures, HashSet<Guid> latestIds) =>
    [
        .. failures
            .Where(e => latestIds.Contains(e.ExecutionId))
            .OrderByDescending(e => e.StartTimeUtc)
            .Select(e => e.ErrorMessageHash)
            .Concat(failures
                .GroupBy(e => e.ErrorMessageHash, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key))
            .Where(IsHash)
            .Select(h => h!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxHashesPerTest)
    ];

    private static Dictionary<string, List<TestExecution>> FailuresByTest(IReadOnlyList<TestSession> sessions)
    {
        var failures = new Dictionary<string, List<TestExecution>>(StringComparer.Ordinal);

        foreach (TestExecution execution in sessions.SelectMany(s => s.Executions))
        {
            if (!execution.Outcome.IsFailure() || execution.Identity?.TestFingerprint is not { Length: > 0 } fingerprint)
                continue;

            if (!failures.TryGetValue(fingerprint, out List<TestExecution>? list))
                failures[fingerprint] = list = [];

            list.Add(execution);
        }

        return failures;
    }

    /// <summary>
    /// The deciding attempt of every test in the newest run: the highest attempt number, the latest
    /// start on a tie.
    /// </summary>
    private static Dictionary<string, TestExecution> FinalAttempts(List<TestSession> latest) =>
        latest
            .SelectMany(s => s.Executions)
            .Where(e => e.Identity?.TestFingerprint is { Length: > 0 })
            .GroupBy(e => e.Identity.TestFingerprint, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.Retry?.AttemptNumber ?? 1).ThenByDescending(e => e.StartTimeUtc).First(),
                StringComparer.Ordinal);

    /// <summary>
    /// The newest session's key of each assembly. Only the newest speaks for an assembly: it is the
    /// run a reader wants to know reached Cloud.
    /// </summary>
    private static Dictionary<string, string> SessionKeys(IReadOnlyList<TestSession> sessions)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (TestSession session in sessions)
        {
            foreach (string assembly in SessionAssemblies.Of(session))
                keys.TryAdd(assembly, session.SessionId.ToString("D", CultureInfo.InvariantCulture));
        }

        return keys;
    }

    /// <summary>
    /// Whether a stored hash has the shape the digest accepts. One malformed value costs the whole
    /// batch a 400, and the store is a file anyone can edit.
    /// </summary>
    private static bool IsHash(string? value) => value is not null && HashShape().IsMatch(value);

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashShape();
}
