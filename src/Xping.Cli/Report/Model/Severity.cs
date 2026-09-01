/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// How much a finding deserves attention, banded from an impact score.
/// </summary>
/// <remarks>
/// Declared most-severe-first so that ordinal comparison is severity comparison: sorting ascending
/// puts <see cref="High"/> at the top, and <c>severity &lt;= threshold</c> reads as "at least as
/// severe as". <c>--fail-on</c> depends on that, so the order must not be reversed.
/// </remarks>
internal enum Severity
{
    /// <summary>Impact at or above <see cref="LocalAnalysisConstants.SeverityHighThreshold"/>.</summary>
    High,

    /// <summary>Impact at or above <see cref="LocalAnalysisConstants.SeverityMediumThreshold"/>.</summary>
    Medium,

    /// <summary>Everything else.</summary>
    Low
}

/// <summary>
/// How much data a finding rests on, measured in sessions the subject ran in within the window.
/// </summary>
/// <remarks>
/// <para>
/// Sessions, not executions: attempts of one test inside one session are correlated, so a test that
/// retried its way to forty executions across six builds has six occasions' worth of evidence and
/// must not be labelled as though it had forty.
/// </para>
/// <para>
/// The unit matches Xping Cloud, which bands an effective sample size computed over runs collapsed
/// to one row per test per session. The thresholds do not, and deliberately —
/// <see cref="LocalAnalysisConstants.EvidenceModerateSessions"/> explains why. Local windows are
/// small, so most local findings will legitimately be <see cref="Low"/> or <see cref="Moderate"/> —
/// that is correct, and must be surfaced rather than hidden.
/// </para>
/// </remarks>
internal enum EvidenceLevel
{
    /// <summary>Fewer than <see cref="LocalAnalysisConstants.EvidenceModerateSessions"/> sessions.</summary>
    Low,

    /// <summary>Between the moderate and high session thresholds, inclusive.</summary>
    Moderate,

    /// <summary>More than <see cref="LocalAnalysisConstants.EvidenceHighSessions"/> sessions.</summary>
    High
}
