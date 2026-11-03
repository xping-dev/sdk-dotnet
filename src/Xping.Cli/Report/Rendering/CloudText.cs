/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// The words the text report prints for Xping Cloud's view of a test (cloud-data-in-cli-cli-spec §7,
/// §8, Amendment 3).
/// </summary>
/// <remarks>
/// <para>
/// Composed at render time from the <c>cloud</c> objects rather than resolved into the envelope's
/// strings, so that in JSON every Cloud value stays under a <c>cloud</c> object and the local
/// <c>headline</c>, <c>contrast</c> and <c>metrics</c> are never mixed with it (report-format
/// Amendment 4). Every phrase says <c>cloud</c> or <c>confidence</c>, or sits under a <c>CLOUD</c>
/// heading, so no Cloud figure can be read as a local statistic (contract R3).
/// </para>
/// <para>
/// Every word read from a DTO goes through <see cref="Recorded"/>: the Cloud is another writer of
/// text this process prints, and is trusted no more than the local store (D7). A value this build
/// does not know is printed as sent, never dropped (contract §4).
/// </para>
/// <para>
/// Nothing here derives a verdict from a number (contract R6): the verdict and the flaky state are
/// printed as Cloud sent them, and the score only ever as a score.
/// </para>
/// </remarks>
/// <param name="glyphs">The glyph set the report is drawn with.</param>
internal sealed class CloudText(ReportGlyphs glyphs)
{
    /// <summary>The ESS below which Cloud does not score a test (contract §6.2).</summary>
    private const int MinimumEffectiveSampleSize = 10;

    private const string NotAttempted = "not-attempted";

    // The local kinds that are observations about this machine (contract L4). Their label never
    // moves; Cloud adds its flaky state on a second line.
    private static readonly HashSet<string> Observations = new(StringComparer.Ordinal)
    {
        nameof(FindingKind.RetryMasked),
        nameof(FindingKind.RetryExhausted),
        nameof(FindingKind.TimingOut),
        nameof(FindingKind.BrokenFixture),
        nameof(FindingKind.SharedFailure),
        nameof(FindingKind.DurationUnstable),
        nameof(FindingKind.Vanished)
    };

    /// <summary>
    /// Gets what joins the parts of one Cloud phrase.
    /// </summary>
    /// <remarks>
    /// The glyph set's separator, except in ASCII, where it is <c>-</c>: <c>|</c> joins the trailer's
    /// own segments, and a Cloud part would read as a field of its own.
    /// </remarks>
    public string Separator => glyphs.Separator == "|" ? "-" : glyphs.Separator;

    private string Joint => $" {Separator} ";

    /// <summary>
    /// Whether the Cloud layer ran for this report. Without it the report is exactly the local one
    /// (contract R1, Amendment 3E).
    /// </summary>
    public static bool Attempted(CloudContextDto? context) => context is not null && context.Status != NotAttempted;

    /// <summary>
    /// The header's provenance token: <c>cloud</c> when Cloud answered, <c>cloud (partial)</c> when
    /// some of it did, else null (§7.5).
    /// </summary>
    public static string? HeaderToken(CloudContextDto? context) => context?.Status switch
    {
        "ok" => "cloud",
        "partial" => "cloud (partial)",
        _ => null
    };

    /// <summary>
    /// Why the report has less Cloud data than it asked for, said once for the whole report (§7.6);
    /// null when there is nothing to say.
    /// </summary>
    public string? Reason(CloudContextDto? context)
    {
        if (context is null || context.Status is "ok" or NotAttempted)
            return null;

        string reason = Recorded(context.Reason);

        return context.Status switch
        {
            "unavailable" => $"unavailable ({reason}){Joint}showing local results only",
            "login-required" => $"login required{Joint}run xping login",
            "forbidden" when context.Credential == "api-key" =>
                $"forbidden{Joint}API keys need the Team plan; run xping login instead",
            "forbidden" => $"forbidden{Joint}showing local results only",
            "no-project" => NoProject(context),
            "partial" =>
                $"partial, {Count(context.EnrichedTests)} of {Count(context.RequestedTests)} tests answered ({reason})",
            _ => $"{Recorded(context.Status)} ({reason})"
        };
    }

    /// <summary>
    /// A finding's label and the Cloud lines under its name (§7.2, Amendment 3D–3F, 3I, 3O).
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <param name="context">What the report asked Cloud.</param>
    /// <param name="local">The local kind's word, already safe to print.</param>
    /// <returns>The label, and the lines to write under the name; empty without Cloud data.</returns>
    public (string Label, IReadOnlyList<string> Lines) Row(FindingDto finding, CloudContextDto? context, string local)
    {
        if (!Attempted(context))
            return (local, []);

        string labelled = "local " + local;

        // A group is never asked about: Cloud knows tests, not clusters.
        if (finding.Subject.Members != null)
            return (labelled, []);

        if (finding.Cloud is not { } digest)
            return (labelled, context!.Status == "partial" ? ["cloud: not answered"] : []);

        if (digest.State != "ok")
            return (labelled, [$"cloud: {NotInProject(context!, finding.Subject.Assembly)}"]);

        // The score first: it is what Xping adds that no local run can, and it is on the same line
        // of every scored row so a reader scanning the list reads one column of it (Amendment 3O).
        var lines = new List<string>();

        if (ConfidenceLine(digest.Confidence) is { } confidence)
            lines.Add(confidence);

        string label;
        string? verdict = Verdict(digest);
        string? flaky = FlakyPhrase(digest.Flaky);

        if (digest.DisagreesWithLocal)
        {
            label = $"{labelled} {glyphs.NotEqual} cloud {Recorded(digest.Verdict ?? "no verdict")}";
            lines.Add("cloud: " + string.Join(Joint, new[] { verdict, flaky }.OfType<string>()));
        }
        else if (Observations.Contains(finding.Kind))
        {
            label = labelled;
            lines.Add("cloud: " + (flaky ?? verdict ?? "no verdict"));
        }
        else if (digest.Verdict is "known-flaky" or "broken" or "unstable" or "passing")
        {
            label = "cloud " + digest.Verdict;
            lines.Add($"local: {local} in {Runs(finding.EvidenceSessions)} here");
        }
        else
        {
            label = labelled;
            lines.Add("cloud: " + (verdict ?? "no verdict"));
        }

        if (NameDiffers(digest) is { } name)
            lines.Add(name);

        return (label, lines);
    }

    /// <summary>
    /// The latest-run Cloud line's segments, the confidence first (§7.5, Amendment 3A, 3G, 3P).
    /// </summary>
    public IReadOnlyList<string> LatestRun(LatestRunFailureDto failure, CloudContextDto? context)
    {
        if (!Attempted(context))
            return [];

        if (failure.Cloud is not { } cloud)
            return context!.Status == "partial" ? ["not answered"] : [];

        CloudDigestDto digest = cloud.Digest;

        if (digest.State != "ok")
            return [NotInProject(context!, failure.Subject.Assembly)];

        var segments = new List<string>();

        if (digest.Confidence is { Score: { } score } scored)
            segments.Add($"confidence {Score(score)}" + (Window(scored.Window) is { } length ? $" ({length})" : string.Empty));

        string? since = digest.Canonical?.FailingSince is { } failingSince ? " since " + Date(failingSince) : null;
        string? verdict = digest.Verdict switch
        {
            null => null,
            "broken" => "broken" + since,
            "known-flaky" => "known flaky",
            "no-history" => "no canonical history",
            _ => Recorded(digest.Verdict)
        };

        if (verdict is not null)
            segments.Add(verdict);

        if (MatchPhrase(cloud.Match, cloud.Signature) is { } match)
            segments.Add(match);

        return segments;
    }

    /// <summary>
    /// The finding's confidence line, <c>cloud: confidence 0.22 ▼0.61 · highly unreliable · 35 d</c>;
    /// for a score Cloud withheld, the evidence it lacks; null when there is no score to speak of
    /// (Amendment 3O).
    /// </summary>
    /// <remarks>
    /// The window stays with the score and the ESS with the evidence level (contract §4.4, §4.8).
    /// The category is Cloud's own word for the score, printed as sent: nothing here turns a number
    /// into a verdict (contract R6).
    /// </remarks>
    public string? ConfidenceLine(CloudConfidenceDto? confidence)
    {
        if (confidence is null)
            return null;

        if (confidence.State == "insufficient-evidence")
            return EvidenceToken(confidence) is { } evidence ? "cloud: " + evidence : null;

        if (confidence.Score is not { } score)
            return null;

        var parts = new List<string> { $"cloud: confidence {Score(score)}{Delta(confidence.Delta)}" };

        if (confidence.Category is { } category)
            parts.Add(Words(category));

        if (Window(confidence.Window) is { } window)
            parts.Add(window);

        if (confidence.Freshness == "pending")
            parts.Add("pending");

        return string.Join(Joint, parts);
    }

    /// <summary>
    /// The line under a failure mode's count line (§7.4, Amendment 3J); null for an unsignable mode
    /// or one with no Cloud view.
    /// </summary>
    public string? Mode(CloudMatchDto? cloud)
    {
        if (cloud is null)
            return null;

        return cloud.Match switch
        {
            "unsignable" => null,
            "matched" when cloud.Signature is { } signature => "cloud: " + Spread(signature),
            "matched" => "cloud: seen",
            _ => "cloud: " + Recorded(cloud.Match)
        };
    }

    /// <summary>
    /// The labelled pairs for a digest: the detail view's, prefixed <c>cloud</c>, or the
    /// <c>--test</c> block's, unprefixed (§7.4, §8, Amendment 3N).
    /// </summary>
    /// <param name="digest">The digest; nothing is written unless it is <c>ok</c>.</param>
    /// <param name="prefix">What every label starts with.</param>
    /// <param name="disagreement">What follows the verdict when it disagrees with the local finding.</param>
    /// <param name="fetched">Whether to write when it was fetched; the <c>--test</c> heading says it.</param>
    public IReadOnlyList<(string Label, string? Value)> Pairs(
        CloudDigestDto? digest, string prefix, string? disagreement, bool fetched)
    {
        if (digest is not { State: "ok" })
            return [];

        string? verdict = Verdict(digest, withTime: true);

        if (verdict is not null && disagreement is not null)
            verdict += " " + disagreement;

        List<(string Label, string? Value)> pairs =
        [
            (prefix + "confidence", ConfidencePair(digest.Confidence)),
            (prefix + "verdict", verdict),
            (prefix + "evidence", EvidencePair(digest.Confidence)),
            (prefix + "flaky", FlakyPair(digest.Flaky)),
            (prefix + "canonical", CanonicalPair(digest)),
            (prefix + "transition", TransitionPair(digest.LastTransition)),
            (prefix + "lifetime", LifetimePair(digest))
        ];

        if (digest.Identity == "name-mismatch" && digest.TestName is { } name)
            pairs.Add((prefix + "name", "differs: " + Recorded(name)));

        if (fetched)
        {
            string pending = digest.Confidence?.Freshness == "pending" ? " (pending: newer runs not yet scored)" : string.Empty;
            pairs.Add((prefix + "fetched", Time(digest.FetchedAt) + pending));
        }

        return pairs;
    }

    /// <summary>
    /// One signature in the <c>--test</c> block (§8).
    /// </summary>
    /// <param name="signature">The signature.</param>
    /// <param name="seenHere">Whether this machine recorded it.</param>
    public string Signature(CloudSignatureDto signature, bool seenHere)
    {
        string head = string.Join(' ', new[] { ShortHash(signature.ErrorMessageHash), Recorded(signature.ExceptionType) }.OfType<string>());
        var parts = new List<string>();

        if (head.Length > 0)
            parts.Add(head);

        if (signature.Runs is { } runs)
            parts.Add(Runs(runs));

        if (signature.Machines is { } machines)
            parts.Add($"{Count(machines)} {(machines == 1 ? "machine" : "machines")}");

        if (signature.CiRuns is { } ci)
            parts.Add(ci > 0 ? $"{Count(ci)} CI" : "local only");

        if (signature.FirstSeenAt is { } first)
            parts.Add("since " + Date(first));

        string line = string.Join(Joint, parts);
        return seenHere ? line + " (seen here)" : line;
    }

    /// <summary>
    /// What Cloud's newest session from this machine is doing, for the <c>--test</c> block.
    /// </summary>
    public string? LastRun(CloudContextDto? context, string? projectKey)
    {
        if (context?.Sessions.FirstOrDefault(s => s.ProjectKey == projectKey) is not { } session)
            return null;

        string scored = session.Scored ? "scored" : "not scored yet";
        return $"{Recorded(session.State)}, {scored} (session {TerminalText.Head(Recorded(session.SessionKey), 8)})";
    }

    /// <summary>The project the assembly was bound to, or null.</summary>
    public static string? ProjectKey(CloudContextDto? context, string? assembly) =>
        context?.Projects.FirstOrDefault(p => p.Assembly == assembly)?.ProjectKey;

    /// <summary>Says a test is not in its project, naming the project when one was found.</summary>
    public string NotInProject(CloudContextDto context, string? assembly) =>
        ProjectKey(context, assembly) is { } key ? $"not in project {Recorded(key)}" : "not in the project";

    /// <summary>A Cloud time, in UTC as sent (Amendment 3K).</summary>
    public static string Time(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The first characters of a signature, marked as shortened.</summary>
    public string? ShortHash(string? hash) =>
        Recorded(hash) is { Length: > 0 } recorded ? TerminalText.Head(recorded, 4) + glyphs.Ellipsis : null;

    /// <summary>A value the Cloud sent, made safe for the terminal.</summary>
    public string Recorded(string? value) => value is null ? string.Empty : TerminalText.Printable(value, glyphs.Charset);

    private string NoProject(CloudContextDto context)
    {
        const string fix = "pass --project <key>";

        return context.Projects.FirstOrDefault(p => p.Status == "no-project") switch
        {
            { ProjectKey: { } key } => $"not in this workspace: project {Recorded(key)}{Joint}{fix}",
            { } row => $"no project in this workspace for {Recorded(row.Assembly)}{Joint}{fix}",
            null => $"no project in this workspace{Joint}{fix}"
        };
    }

    private string? NameDiffers(CloudDigestDto digest) =>
        digest.Identity == "name-mismatch" && digest.TestName is { } name ? "cloud: known as " + Recorded(name) : null;

    /// <summary>The verdict in words, with its date when it is <c>broken</c> (Amendment 3A).</summary>
    private string? Verdict(CloudDigestDto digest, bool withTime = false)
    {
        string? since = digest.Canonical?.FailingSince is { } failingSince
            ? " since " + (withTime ? Time(failingSince) : Date(failingSince))
            : null;

        return digest.Verdict switch
        {
            null => null,
            "known-flaky" => "known flaky",
            "broken" => "broken on the canonical branch" + since,
            "unstable" => "unstable on the canonical branch",
            "passing" => "passing on the canonical branch",
            "no-history" => "no canonical history",
            _ => Recorded(digest.Verdict)
        };
    }

    /// <summary>
    /// The flaky state in words for a row's second line. Never "not flaky" for a state that is not
    /// <c>not-flaky</c> (contract §4.3).
    /// </summary>
    private string? FlakyPhrase(CloudFlakyDto? flaky)
    {
        if (flaky?.State is not { } state)
            return null;

        string within = Window(flaky.Window) is { } length ? " in " + length : string.Empty;

        return state switch
        {
            "flaky" => FlakyNow(flaky),
            "not-flaky" => "not flaky" + within,
            "inconclusive" => "flaky inconclusive" + within,
            "not-examined" => "not examined for flakiness" + within,
            _ => Recorded(state)
        };
    }

    private string FlakyNow(CloudFlakyDto flaky)
    {
        string[] kind = new[] { flaky.Category, flaky.Severity }.OfType<string>().Select(Words).ToArray();
        string what = kind.Length > 0 ? $"flaky ({string.Join(", ", kind)})" : "flaky";

        return flaky.Since is { } since ? $"{what} since {Date(since)}" : what;
    }

    private string? MatchPhrase(string match, CloudSignatureDto? signature) => match switch
    {
        "matched" when signature is { Machines: { } machines } =>
            $"same error on {Count(machines)} {(machines == 1 ? "machine" : "machines")}" +
            (signature.CiRuns > 0 ? " and CI" : ", not in CI"),
        "matched" => "same error seen in Cloud",
        "unseen" => "this error unseen in Cloud",
        "unsignable" or "unknown" => null,
        _ => Recorded(match)
    };

    private string Spread(CloudSignatureDto signature)
    {
        var parts = new List<string>();

        if (signature.Machines is { } machines)
            parts.Add($"same error on {Count(machines)} {(machines == 1 ? "machine" : "machines")}");
        else
            parts.Add("same error");

        if (signature.CiRuns is { } ci)
            parts.Add($"{Count(ci)} CI");

        if (signature.LocalRuns is { } local)
            parts.Add($"{Count(local)} local");

        if (signature.FirstSeenAt is { } first)
            parts.Add("since " + Date(first));

        return string.Join(Joint, parts);
    }

    private string? EvidenceToken(CloudConfidenceDto confidence)
    {
        if (confidence.EvidenceLevel is not { } level || confidence.EffectiveSampleSize is not { } ess)
            return null;

        string of = confidence.State == "insufficient-evidence" ? $" of {Count(MinimumEffectiveSampleSize)}" : string.Empty;
        string window = Window(confidence.EvidenceWindow) is { } length ? ", " + length : string.Empty;

        return $"evidence {Recorded(level)} (ESS {Ess(ess)}{of}{window})";
    }

    private string? ConfidencePair(CloudConfidenceDto? confidence)
    {
        if (confidence is null)
            return null;

        var parts = new List<string>();

        if (confidence.Score is { } score)
            parts.Add(Score(score) + Delta(confidence.Delta));
        else
            parts.Add(confidence.State is { } state ? Words(state) : "not scored");

        if (confidence.Category is { } category)
            parts.Add(Words(category));

        if (Window(confidence.Window) is { } window)
            parts.Add(window + " window");

        if (confidence.ComputedAt is { } computed)
            parts.Add("computed " + Time(computed));

        return string.Join(Joint, parts);
    }

    private string? EvidencePair(CloudConfidenceDto? confidence)
    {
        if (confidence is null || (confidence.EvidenceLevel is null && confidence.EffectiveSampleSize is null))
            return null;

        var parts = new List<string>();

        if (confidence.EvidenceLevel is { } level)
            parts.Add(Recorded(level));

        if (confidence.EffectiveSampleSize is { } ess)
        {
            string of = confidence.State == "insufficient-evidence" ? $" of {Count(MinimumEffectiveSampleSize)}" : string.Empty;
            parts.Add($"ESS {Ess(ess)}{of}");
        }

        if (confidence.CanonicalRuns is not null || confidence.SpeculativeRuns is not null || confidence.UntrackedRuns is not null)
        {
            parts.Add(
                $"{Count(confidence.CanonicalRuns ?? 0)} canonical, {Count(confidence.SpeculativeRuns ?? 0)} speculative, " +
                $"{Count(confidence.UntrackedRuns ?? 0)} untracked");
        }

        if (Window(confidence.EvidenceWindow) is { } window)
            parts.Add(window + " window");

        return string.Join(Joint, parts);
    }

    private string? FlakyPair(CloudFlakyDto? flaky)
    {
        if (flaky?.State is not { } state)
            return null;

        string? window = Window(flaky.Window);
        string? examined = flaky.LastDetectionAt is { } at ? "examined " + Time(at) : null;

        IEnumerable<string?> parts = state switch
        {
            "flaky" => [FlakyNow(flaky)],
            "not-flaky" => ["not flaky", examined, window is null ? null : window + " window"],
            "inconclusive" => ["inconclusive", examined, window is null ? null : window + " window"],
            "not-examined" => [window is null ? "not examined" : "not examined in " + window],
            _ => [Recorded(state), examined]
        };

        return string.Join(Joint, parts.OfType<string>());
    }

    private string? CanonicalPair(CloudDigestDto digest)
    {
        if (digest.Canonical is not { } canonical)
            return digest.Verdict == "no-history" ? "no canonical history" : null;

        var parts = new List<string>();

        if (canonical.Failed is { } failed && canonical.Runs is { } runs)
            parts.Add($"failed {Count(failed)}/{Count(runs)}");

        if (canonical.ConsecutiveFailures is > 0 and { } streak)
        {
            string since = canonical.FailingSince is { } at ? " since " + Time(at) : string.Empty;
            parts.Add($"{Count(streak)} in a row{since}");
        }

        if (canonical.LatestAt is { } latest)
            parts.Add($"newest {(canonical.LatestOutcome is { } outcome ? Recorded(outcome) + " " : string.Empty)}{Time(latest)}");

        return parts.Count == 0 ? null : string.Join(Joint, parts);
    }

    private string? TransitionPair(CloudTransitionDto? transition)
    {
        if (transition?.Type is not { } type)
            return null;

        string change = transition.From is { } from && transition.To is { } to
            ? $" {Words(from)} {glyphs.Arrow} {Words(to)}"
            : string.Empty;
        string at = transition.OccurredAt is { } when ? " at " + Time(when) : string.Empty;

        return Words(type) + change + at;
    }

    private string? LifetimePair(CloudDigestDto digest)
    {
        if (digest.TotalRuns is not { } total)
            return null;

        return digest.LastExecutedAt is { } last ? $"{Runs(total)}{Joint}last {Time(last)}" : Runs(total);
    }

    /// <summary><c>▼0.61</c> when the score moved by at least 0.01; else nothing.</summary>
    private string Delta(double? delta)
    {
        if (delta is not { } value || Math.Abs(value) < 0.01)
            return string.Empty;

        return " " + (value < 0 ? glyphs.Down : glyphs.Up) + Score(Math.Abs(value));
    }

    /// <summary><c>35 d</c> or <c>10 runs</c>; null for a window without a length.</summary>
    private static string? Window(CloudWindowDto? window) => window?.Length switch
    {
        null => null,
        int length when window.Kind == "runs" => Runs(length),
        int length => $"{Count(length)} d"
    };

    /// <summary><c>moderately-reliable</c> → <c>moderately reliable</c>.</summary>
    private string Words(string kebab) => Recorded(kebab).Replace('-', ' ');

    private static string Date(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Score(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Ess(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Runs(int runs) => runs == 1 ? "1 run" : $"{Count(runs)} runs";
}
