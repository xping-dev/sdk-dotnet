/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Cloud.Digest;
using Xping.Cli.Report.Contract;

namespace Xping.Cli.Cloud;

/// <summary>
/// Cloud's answer for one test.
/// </summary>
/// <param name="Digest">The digest, before the per-subject additions.</param>
/// <param name="AlgorithmMismatch">Whether its project answered under another signature algorithm.</param>
internal sealed record CloudAnswer(CloudDigestDto Digest, bool AlgorithmMismatch);

/// <summary>
/// Writes Xping Cloud's answers into the envelope's <c>cloud</c> slots
/// (cloud-data-in-cli-cli-spec §4.4).
/// </summary>
/// <remarks>
/// Only <c>cloud</c> members are written. Every local field stays as the analysis left it, so the
/// local layer reads the same with and without Cloud (contract R1, §8.3).
/// </remarks>
internal static class EnvelopeStitcher
{
    private const string Ok = "ok";

    /// <summary>
    /// Maps one wire digest into the envelope's shape.
    /// </summary>
    /// <param name="digest">The digest Cloud sent.</param>
    /// <param name="fetchedAt">When this machine read it.</param>
    /// <returns>
    /// The digest; a test Cloud does not know, or sent in a state this CLI cannot read, as
    /// <c>unknown</c> with every block null (contract §8.2 F4).
    /// </returns>
    public static CloudDigestDto ToDto(TestDigest digest, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(digest);

        if (digest.State != Ok)
            return new CloudDigestDto("unknown", fetchedAt, false, null, null, null, null, null, null, null, null, null, []);

        return new CloudDigestDto(
            Ok,
            fetchedAt,
            DisagreesWithLocal: false,
            Identity: null,
            digest.Verdict,
            digest.TestName,
            digest.LastExecutedAt,
            digest.TotalRuns,
            digest.Confidence is { } c
                ? new CloudConfidenceDto(
                    c.State, c.Score, c.LowerBound, c.UpperBound, c.Category, c.Trend, c.Delta, c.EffectiveSampleSize,
                    c.EvidenceLevel, c.CanonicalRuns, c.SpeculativeRuns, c.UntrackedRuns, Window(c.Window),
                    Window(c.EvidenceWindow), c.ComputedAt, c.Freshness)
                : null,
            digest.Flaky is { } f
                ? new CloudFlakyDto(f.State, f.Category, f.Severity, f.Since, f.LastDetectionAt, Window(f.Window), f.ComputedAt, f.Freshness)
                : null,
            digest.Canonical is { } k
                ? new CloudCanonicalDto(k.Runs, k.Failed, k.ConsecutiveFailures, k.FailingSince, k.LatestOutcome, k.LatestAt, Window(k.Window))
                : null,
            digest.LastTransition is { } t ? new CloudTransitionDto(t.Type, t.From, t.To, t.OccurredAt) : null,
            [
                .. (digest.Signatures ?? []).OfType<DigestSignature>().Select(s => new CloudSignatureDto(
                    s.ErrorMessageHash, s.ExceptionType, s.Requested, s.Runs, s.CanonicalRuns, s.CiRuns, s.LocalRuns,
                    s.Machines, s.FirstSeenAt, s.LastSeenAt, [.. (s.StackTraceHashes ?? []).OfType<string>()], Window(s.Window)))
            ]);
    }

    /// <summary>
    /// Maps one run of the <c>--test</c> history into the envelope's shape.
    /// </summary>
    public static CloudRunDto ToDto(CloudRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new CloudRunDto(
            run.SessionKey, run.StartedAt, run.Outcome, run.Attempts, run.FirstAttemptOutcome, run.DurationMs, run.Tier,
            run.IsCi, run.MachineName, run.Branch, run.CommitSha, run.PullRequestNumber, run.ExceptionType, run.ErrorMessageHash);
    }

    /// <summary>
    /// Writes the answers and the Cloud context into the envelope.
    /// </summary>
    /// <param name="envelope">The local report.</param>
    /// <param name="answers">Cloud's answers, by fingerprint.</param>
    /// <param name="latestFailureHashes">The message hash of each test's failure in the newest session.</param>
    /// <param name="context">How the Cloud layer went.</param>
    /// <param name="test">The <c>--test</c> Cloud view, or null without <c>--test</c>.</param>
    /// <returns>The envelope with every slot Cloud answered for filled.</returns>
    /// <remarks>
    /// A subject Cloud did not answer for keeps <c>cloud: null</c>; why is in <c>context.cloud</c>
    /// (contract R5).
    /// </remarks>
    public static ReportEnvelope Stitch(
        ReportEnvelope envelope,
        IReadOnlyDictionary<string, CloudAnswer> answers,
        IReadOnlyDictionary<string, string?> latestFailureHashes,
        CloudContextDto context,
        CloudTestHistoryDto? test)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(latestFailureHashes);
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, int> names = NamesPerFingerprint(envelope);

        CloudAnswer? AnswerFor(SubjectDto subject) =>
            subject.Members is null && subject.Fingerprint is { } fingerprint && answers.TryGetValue(fingerprint, out CloudAnswer? answer)
                ? answer
                : null;

        CloudDigestDto For(SubjectDto subject, CloudDigestDto digest, string? kind) =>
            digest.State != Ok
                ? digest
                : digest with
                {
                    DisagreesWithLocal = kind is not null && Disagrees(kind, digest),
                    Identity = Identity(subject, digest, names)
                };

        IReadOnlyList<FailureModeDto> Modes(IReadOnlyList<FailureModeDto> modes, CloudAnswer answer) =>
            [.. modes.Select(m => m with { Cloud = SignatureMatcher.Match(m.ErrorMessageHash, answer.Digest, answer.AlgorithmMismatch) })];

        FindingDto Finding(FindingDto finding) =>
            AnswerFor(finding.Subject) is { } answer
                ? finding with
                {
                    Cloud = For(finding.Subject, answer.Digest, finding.Kind),
                    FailureModes = Modes(finding.FailureModes, answer)
                }
                : finding;

        LatestRunFailureDto Failure(LatestRunFailureDto failure)
        {
            if (AnswerFor(failure.Subject) is not { } answer)
                return failure;

            CloudMatchDto match = SignatureMatcher.Match(
                latestFailureHashes.GetValueOrDefault(failure.Subject.Fingerprint!), answer.Digest, answer.AlgorithmMismatch);

            return failure with { Cloud = new CloudFailureDto(For(failure.Subject, answer.Digest, null), match.Match, match.Signature) };
        }

        TestHistoryDto History(TestHistoryDto history)
        {
            if (history.Test is not { } subject || test is null)
                return history;

            CloudAnswer? answer = AnswerFor(subject);
            return history with
            {
                FailureModes = answer is null ? history.FailureModes : Modes(history.FailureModes, answer),
                Cloud = test with { Digest = test.Digest is { } digest ? For(subject, digest, null) : null }
            };
        }

        return envelope with
        {
            Context = envelope.Context is { } existing ? existing with { Cloud = context } : new ContextDto(null, null, null, context),
            LatestRun = envelope.LatestRun is { } latest ? latest with { Failures = [.. latest.Failures.Select(Failure)] } : null,
            Test = envelope.Test is { } history ? History(history) : null,
            Findings = [.. envelope.Findings.Select(Finding)]
        };
    }

    /// <summary>
    /// Whether Cloud reads a local <c>Flaky</c> or <c>AlwaysFailing</c> finding differently (contract L3).
    /// </summary>
    private static bool Disagrees(string kind, CloudDigestDto digest) => kind switch
    {
        "Flaky" => digest.Verdict != "known-flaky" && digest.Flaky?.State == "not-flaky",
        "AlwaysFailing" => digest.Verdict == "passing",
        _ => false
    };

    /// <summary>
    /// <c>shared</c> when several local tests carry the fingerprint; else <c>name-mismatch</c> when
    /// Cloud names it otherwise (contract §9.3). Both come from a pinned fingerprint reused.
    /// </summary>
    private static string? Identity(SubjectDto subject, CloudDigestDto digest, Dictionary<string, int> names)
    {
        if (names.GetValueOrDefault(subject.Fingerprint!) > 1)
            return "shared";

        return digest.TestName is { } cloudName && subject.FullyQualifiedName is { } localName
            && !string.Equals(cloudName, localName, StringComparison.Ordinal)
                ? "name-mismatch"
                : null;
    }

    private static Dictionary<string, int> NamesPerFingerprint(ReportEnvelope envelope)
    {
        IEnumerable<SubjectDto> subjects =
        [
            .. envelope.Findings.Select(f => f.Subject),
            .. envelope.LatestRun?.Failures.Select(f => f.Subject) ?? [],
            .. envelope.Test?.Test is { } test ? [test] : Array.Empty<SubjectDto>()
        ];

        return subjects
            .Where(s => s.Members is null && s.Fingerprint is not null && s.FullyQualifiedName is not null)
            .GroupBy(s => s.Fingerprint!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(s => s.FullyQualifiedName!).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
    }

    private static CloudWindowDto? Window(DigestWindow? window) =>
        window is null ? null : new CloudWindowDto(window.Kind, window.Length, window.End, window.OldestAt);
}
