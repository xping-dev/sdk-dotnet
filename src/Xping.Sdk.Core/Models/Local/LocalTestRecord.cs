/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json.Serialization;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Local;

/// <summary>
/// A single test execution as persisted in the local run store.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately lossy projection of <see cref="TestExecution"/>. It drops stack traces,
/// error message text, exception types, source locations, categories and orchestration data, keeping
/// only what local cross-run analysis needs. That makes the store roughly 10x smaller and decouples
/// the on-disk format from the cloud wire model.
/// </para>
/// <para>
/// Because it is lossy, a record <b>cannot be re-uploaded</b> as a <c>TestSession</c>. Recovering a
/// failed upload requires the separate outbox tier, which persists the wire payload verbatim.
/// </para>
/// <para>
/// Property names are single characters because they repeat on every line of every run file.
/// </para>
/// </remarks>
public sealed class LocalTestRecord
{
    /// <summary>
    /// Gets the stable test fingerprint.
    /// </summary>
    /// <remarks>
    /// Stored in full (64 hex characters) rather than truncated. Truncation would save a few bytes
    /// per record but would break identity joins against the cloud, which is what makes local
    /// history importable on signup.
    /// </remarks>
    [JsonPropertyName("f")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets the display name, used for rendering the report.
    /// </summary>
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the outcome, encoded as a single character. See <see cref="OutcomeCodes"/>.
    /// </summary>
    [JsonPropertyName("o")]
    public string Outcome { get; set; } = OutcomeCodes.NotExecuted;

    /// <summary>
    /// Gets the execution duration in whole milliseconds.
    /// </summary>
    [JsonPropertyName("d")]
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets the retry attempt number: 1 for the first attempt, 2 for the first retry, and so on.
    /// </summary>
    [JsonPropertyName("r")]
    public int Attempt { get; set; } = 1;

    /// <summary>
    /// Gets a value indicating whether the test failed and then passed on a later attempt within
    /// this same run.
    /// </summary>
    /// <remarks>
    /// This is the only flakiness signal available on a developer's very first run, before any
    /// cross-run history exists, so it is what keeps the first report from being empty.
    /// </remarks>
    [JsonPropertyName("p")]
    public bool PassedOnRetry { get; set; }

    /// <summary>
    /// Gets a short stable hash of the error message, used to group recurring failures.
    /// Null when the test did not fail.
    /// </summary>
    [JsonPropertyName("e")]
    public string? ErrorHash { get; set; }

    /// <summary>
    /// Projects a <see cref="TestExecution"/> onto its slim local representation.
    /// </summary>
    /// <param name="execution">The execution to project.</param>
    /// <returns>The slim record.</returns>
    public static LocalTestRecord FromExecution(TestExecution execution)
    {
        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        return new LocalTestRecord
        {
            Fingerprint = execution.Identity.TestFingerprint,
            Name = string.IsNullOrEmpty(execution.Identity.DisplayName)
                ? execution.TestName
                : execution.Identity.DisplayName,
            Outcome = OutcomeCodes.From(execution.Outcome),
            DurationMs = (long)execution.Duration.TotalMilliseconds,
            Attempt = execution.Retry?.AttemptNumber ?? 1,
            PassedOnRetry = execution.Retry?.PassedOnRetry ?? false,
            ErrorHash = Truncate(execution.ErrorMessageHash)
        };
    }

    private static string? Truncate(string? hash)
    {
        const int Length = 8;

        if (string.IsNullOrEmpty(hash))
            return null;

        return hash!.Length <= Length ? hash : hash.Substring(0, Length);
    }
}

/// <summary>
/// Single-character codes used to persist <see cref="TestOutcome"/> in the local store.
/// </summary>
public static class OutcomeCodes
{
    /// <summary>The test passed.</summary>
    public const string Passed = "P";

    /// <summary>The test failed.</summary>
    public const string Failed = "F";

    /// <summary>The test was skipped.</summary>
    public const string Skipped = "S";

    /// <summary>The test result was inconclusive.</summary>
    public const string Inconclusive = "I";

    /// <summary>The test was not executed.</summary>
    public const string NotExecuted = "N";

    /// <summary>
    /// Maps a <see cref="TestOutcome"/> onto its persisted code.
    /// </summary>
    /// <param name="outcome">The outcome to map.</param>
    /// <returns>The single-character code.</returns>
    public static string From(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => Passed,
        TestOutcome.Failed => Failed,
        TestOutcome.Skipped => Skipped,
        TestOutcome.Inconclusive => Inconclusive,
        _ => NotExecuted
    };
}
