/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Contract;

/// <summary>
/// What the report asked Xping Cloud, with which credential, and how it went.
/// </summary>
/// <remarks>
/// Present whenever a credential resolved, including when every request failed: an agent reading
/// the envelope needs to tell "Cloud was not asked" from "Cloud was asked and could not answer",
/// and <paramref name="Status"/> <c>login-required</c> is its signal that a human must sign in.
/// </remarks>
/// <param name="CloudUrl">The normalized Xping Cloud URL.</param>
/// <param name="Credential"><c>stored-login</c> or <c>api-key</c>: the credential last used.</param>
/// <param name="WorkspaceId">The signed-in workspace, or null for an API key.</param>
/// <param name="Status">
/// <c>ok</c>, <c>partial</c>, <c>unavailable</c>, <c>login-required</c> or <c>not-attempted</c>.
/// </param>
/// <param name="Reason">Why the status is not <c>ok</c>, in the words of the hint line; else null.</param>
/// <param name="Project">The Cloud project the report's assembly was bound to, or null.</param>
internal sealed record CloudContextDto(
    string CloudUrl,
    string Credential,
    string? WorkspaceId,
    string Status,
    string? Reason,
    CloudProjectDto? Project);

/// <summary>
/// The Cloud project an assembly was bound to.
/// </summary>
/// <param name="Assembly">The test assembly.</param>
/// <param name="ProjectKey">The Cloud's project key.</param>
/// <param name="Source">
/// How the key was found: <c>flag</c>, <c>env</c>, <c>session</c> or <c>name-match</c>.
/// </param>
internal sealed record CloudProjectDto(string Assembly, string ProjectKey, string Source);

/// <summary>
/// Xping Cloud's view of one test, across every run uploaded to it.
/// </summary>
/// <param name="Confidence">The Cloud confidence score, 0 to 1, or null when it has none.</param>
/// <param name="Category">The score's category in kebab case (<c>moderately-reliable</c>), or null.</param>
/// <param name="EvidenceLevel">The Cloud's evidence level in kebab case (<c>robust</c>).</param>
/// <param name="Runs">Executions the Cloud has recorded.</param>
/// <param name="Trend">The score's trend in kebab case, or null.</param>
/// <param name="Delta">The score's recent change, or null.</param>
/// <param name="Flaky">Whether the Cloud classifies the test as flaky, or null.</param>
/// <param name="LastExecutedAt">When the Cloud last saw the test run, or null.</param>
/// <param name="FetchedAt">When this report read it.</param>
internal sealed record CloudTestDto(
    double? Confidence,
    string? Category,
    string EvidenceLevel,
    int Runs,
    string? Trend,
    double? Delta,
    bool? Flaky,
    DateTimeOffset? LastExecutedAt,
    DateTimeOffset FetchedAt);
