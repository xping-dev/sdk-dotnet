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
/// How much data a finding rests on, measured in executions of the subject within the window.
/// </summary>
/// <remarks>
/// These bands are shared verbatim with the Xping Dashboard. Local windows are small, so most local
/// findings will legitimately be <see cref="Low"/> or <see cref="Moderate"/> — that is correct, and
/// must be surfaced rather than hidden.
/// </remarks>
internal enum EvidenceLevel
{
    /// <summary>Fewer than <see cref="LocalAnalysisConstants.EvidenceModerateExecutions"/> executions.</summary>
    Low,

    /// <summary>Between the moderate and high execution thresholds, inclusive.</summary>
    Moderate,

    /// <summary>More than <see cref="LocalAnalysisConstants.EvidenceHighExecutions"/> executions.</summary>
    High
}
