/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json.Serialization;

namespace Xping.Sdk.Core.Models.Local;

/// <summary>
/// The first line of a local run file: metadata describing the run as a whole.
/// </summary>
public sealed class LocalRunHeader
{
    /// <summary>
    /// The schema version this file was written with.
    /// </summary>
    /// <remarks>
    /// Readers must skip files carrying an unknown version rather than fail. Developers accumulate
    /// runs over weeks, so a schema change has to degrade to "less history" and never to a crash or
    /// a wiped store.
    /// </remarks>
    public const int CurrentVersion = 1;

    /// <summary>Gets the schema version of the run file.</summary>
    [JsonPropertyName("v")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets the session identifier.</summary>
    [JsonPropertyName("sid")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Gets the UTC timestamp at which the session started.</summary>
    [JsonPropertyName("ts")]
    public DateTime StartedAtUtc { get; set; }

    /// <summary>Gets the wall-clock duration of the session in milliseconds.</summary>
    [JsonPropertyName("dur")]
    public long DurationMs { get; set; }

    /// <summary>Gets the environment name (for example "Local" or "CI").</summary>
    [JsonPropertyName("env")]
    public string? Environment { get; set; }

    /// <summary>Gets the test assembly name.</summary>
    [JsonPropertyName("asm")]
    public string? Assembly { get; set; }

    /// <summary>Gets the source control branch, when known.</summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    /// <summary>Gets the commit SHA, when known.</summary>
    [JsonPropertyName("sha")]
    public string? CommitSha { get; set; }

    /// <summary>Gets a value indicating whether the run executed in a CI environment.</summary>
    [JsonPropertyName("ci")]
    public bool IsCi { get; set; }
}

/// <summary>
/// A complete run as read from, or written to, the local store.
/// </summary>
public sealed class LocalRun
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalRun"/> class.
    /// </summary>
    /// <param name="header">The run header.</param>
    /// <param name="records">The test records belonging to the run.</param>
    public LocalRun(LocalRunHeader header, IReadOnlyList<LocalTestRecord> records)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Records = records ?? throw new ArgumentNullException(nameof(records));
    }

    /// <summary>Gets the run header.</summary>
    public LocalRunHeader Header { get; }

    /// <summary>Gets the test records belonging to this run.</summary>
    public IReadOnlyList<LocalTestRecord> Records { get; }
}
