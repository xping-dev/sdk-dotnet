/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Detects GitHub pull request context from GitHub Actions environment variables and the
/// workflow event payload.
/// </summary>
/// <remarks>
/// Supports <c>pull_request</c> and <c>pull_request_target</c> event triggers.
/// Returns <c>null</c> for any non-PR trigger or when required variables are absent.
/// All failures are silently absorbed — detection never throws.
/// <para>
/// <see cref="PullRequestContext.CommitSha"/> is the PR head commit, read from
/// <c>pull_request.head.sha</c> in the event payload at <c>GITHUB_EVENT_PATH</c>.
/// <c>GITHUB_SHA</c> is not used: it is the synthetic merge commit on <c>pull_request</c>
/// and the base branch tip on <c>pull_request_target</c>.
/// </para>
/// <para>
/// A missing or unreadable payload on a PR event is logged as a warning: it usually means the
/// tests run in a container that was given the <c>GITHUB_*</c> variables but not the runner's
/// temp directory, and PR integration would otherwise vanish without a trace.
/// </para>
/// </remarks>
internal sealed class GitHubPullRequestDetector(
    IEnvironmentVariableProvider env,
    IXpingSerializer serializer,
    ILogger<GitHubPullRequestDetector> logger) : IPlatformPullRequestDetector
{
    internal static readonly HashSet<string> PullRequestEventNames =
        new(StringComparer.OrdinalIgnoreCase) { "pull_request", "pull_request_target" };

    // ReadOnlySpan<byte> deserialization doesn't skip a BOM the way the stream overloads do.
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static readonly Regex _prRefPattern =
        new(@"^refs/pull/(\d+)/", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        try
        {
            return DetectCore();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GitHub PR detection failed unexpectedly; PR integration suppressed.");
            return null;
        }
    }

    private PullRequestContext? DetectCore()
    {
        // Must be a PR-triggered workflow
        string? eventName = env.GetVariable("GITHUB_EVENT_NAME");
        if (string.IsNullOrEmpty(eventName) || !PullRequestEventNames.Contains(eventName!))
        {
            logger.LogDebug(
                "GitHub PR detection skipped: GITHUB_EVENT_NAME='{EventName}' " +
                "(expected 'pull_request' or 'pull_request_target').",
                eventName);
            return null;
        }

        // Extract PR number from GITHUB_REF: refs/pull/123/merge
        if (!TryGetRequired("GITHUB_REF", out string? githubRef))
            return null;

        Match refMatch = _prRefPattern.Match(githubRef);
        if (!refMatch.Success ||
            !int.TryParse(refMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int prNumber))
        {
            logger.LogDebug(
                "GitHub PR detection skipped: GITHUB_REF='{Ref}' does not match the expected PR pattern.",
                githubRef);
            return null;
        }

        // Split GITHUB_REPOSITORY into owner/name
        if (!TryGetRequired("GITHUB_REPOSITORY", out string? repository))
            return null;

        int slashIndex = repository.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == repository.Length - 1)
        {
            logger.LogDebug(
                "GitHub PR detection skipped: GITHUB_REPOSITORY='{Repository}' is not in 'owner/name' format.",
                repository);
            return null;
        }

        string owner = repository.Substring(0, slashIndex);
        string repoName = repository.Substring(slashIndex + 1);

        // Required fields
        if (!TryGetRequired("GITHUB_BASE_REF", out string? baseBranch)) return null;
        if (!TryGetRequired("GITHUB_HEAD_REF", out string? headBranch)) return null;
        if (!TryReadHeadSha(out string? commitSha)) return null;

        // Optional field
        string? author = env.GetVariable("GITHUB_ACTOR");

        logger.LogDebug(
            "GitHub PR detected: {Owner}/{Repo}#{PR} on commit {Sha} ({Head} → {Base}).",
            owner, repoName, prNumber, commitSha, headBranch, baseBranch);

        return new PullRequestContext(
            platform: PullRequestPlatform.GitHub,
            repositoryOwner: owner,
            repositoryName: repoName,
            pullRequestNumber: prNumber,
            commitSha: commitSha,
            baseBranch: baseBranch,
            headBranch: headBranch,
            author: author);
    }

    private bool TryReadHeadSha([NotNullWhen(true)] out string? sha)
    {
        sha = null;
        string? eventPath = env.GetVariable("GITHUB_EVENT_PATH");
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            logger.LogWarning(
                "GitHub PR detection skipped: GITHUB_EVENT_PATH is not set, so the PR head commit is unknown.");
            return false;
        }

        GitHubEventPayload? payload;
        try
        {
            ReadOnlySpan<byte> json = File.ReadAllBytes(eventPath);
            if (json.StartsWith(Utf8Bom))
                json = json.Slice(Utf8Bom.Length);

            payload = serializer.Deserialize<GitHubEventPayload>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "GitHub PR detection skipped: event payload at '{EventPath}' could not be read. " +
                "If tests run in a container, mount the runner's temp directory.",
                eventPath);
            return false;
        }

        string? headSha = payload?.PullRequest?.Head?.Sha;
        if (string.IsNullOrWhiteSpace(headSha))
        {
            logger.LogWarning(
                "GitHub PR detection skipped: event payload at '{EventPath}' has no pull_request.head.sha.",
                eventPath);
            return false;
        }

        sha = headSha!;
        return true;
    }

    private bool TryGetRequired(string variable, [NotNullWhen(true)] out string? value)
    {
        string? raw = env.GetVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.LogDebug("GitHub PR detection skipped: {Variable} is not set.", variable);
            value = null;
            return false;
        }

        value = raw!; // IsNullOrWhiteSpace guarantees non-null; ! informs the compiler
        return true;
    }

    // The serializer's camelCase policy would look for "pullRequest", so the snake_case
    // payload names are spelled out.
    private sealed class GitHubEventPayload
    {
        [JsonPropertyName("pull_request")]
        public GitHubEventPullRequest? PullRequest { get; set; }
    }

    private sealed class GitHubEventPullRequest
    {
        [JsonPropertyName("head")]
        public GitHubEventCommitRef? Head { get; set; }
    }

    private sealed class GitHubEventCommitRef
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
