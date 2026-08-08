/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Builds the single line the SDK prints about local history.
/// </summary>
/// <remarks>
/// <para>
/// The SDK does not analyse history — that is the CLI's job. It reports exactly one fact about the
/// run it just observed: how many tests failed and then passed on a retry. That needs no store read
/// and no cross-run comparison, because <see cref="LocalTestRecord.PassedOnRetry"/> is already on
/// records held in memory at finalization.
/// </para>
/// <para>
/// The line exists so the CLI is discoverable. Without it a developer with no API key sees nothing
/// after a test run and has no reason to learn that <c>dotnet xping report</c> exists. It stays
/// silent on clean runs and in CI so it never becomes recurring noise.
/// </para>
/// </remarks>
internal static class RetryFlakeHint
{
    internal const string SuppressVariable = "XPING_NO_BANNER";

    /// <summary>
    /// Builds the hint, or returns <see langword="null"/> when nothing should be printed.
    /// </summary>
    /// <param name="records">Records for the run that just finished.</param>
    /// <param name="isCi">Whether the run executed in a CI environment.</param>
    /// <param name="suppressed">Whether output has been suppressed by the user.</param>
    public static string? Build(IReadOnlyList<LocalTestRecord> records, bool isCi, bool suppressed)
    {
        // CI logs are read when something breaks; a pointer to a local tool is noise there.
        if (isCi || suppressed || records == null)
            return null;

        int flaked = 0;
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].PassedOnRetry)
                flaked++;
        }

        if (flaked == 0)
            return null;

        string subject = flaked == 1 ? "1 test" : $"{flaked} tests";

        return $"[Xping] {subject} flaked on retry this run - " +
               "run `dotnet xping report` for local history";
    }

    /// <summary>
    /// Returns whether the user has suppressed SDK banners.
    /// </summary>
    public static bool IsSuppressed() =>
        System.Environment.GetEnvironmentVariable(SuppressVariable) is { Length: > 0 };
}
