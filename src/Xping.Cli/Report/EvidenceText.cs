/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report;

/// <summary>
/// How raw failure text is published in evidence.
/// </summary>
/// <remarks>
/// Shared rather than repeated per provider. Two providers eliding at different lengths, or one
/// marking a cut message and another cutting it silently, would leave a reader unable to tell a
/// truncated message from a test that genuinely failed with one line.
/// </remarks>
internal static class EvidenceText
{
    // Appended when text is cut, so a reader can tell a truncated message from a short one.
    private const string ElisionMarker = "… (truncated)";

    /// <summary>
    /// Cuts raw text to the published budget, marking it when anything was removed.
    /// </summary>
    /// <param name="text">The raw text, which may be absent.</param>
    /// <returns>The text to publish, or <see langword="null"/> when there was none.</returns>
    public static string? Elide(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= LocalAnalysisConstants.ExemplarCharBudget
            ? text
            : string.Concat(text.AsSpan(0, LocalAnalysisConstants.ExemplarCharBudget), ElisionMarker);
    }
}
