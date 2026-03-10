/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Detects GitHub pull request context from GitHub Actions environment variables.
/// </summary>
/// <remarks>
/// Supports <c>pull_request</c> and <c>pull_request_target</c> event triggers.
/// Returns <c>null</c> for any non-PR trigger or when required variables are absent.
/// All failures are silently absorbed — detection never throws.
/// </remarks>
internal sealed class GitHubPullRequestDetector(
    IEnvironmentVariableProvider env,
    ILogger<GitHubPullRequestDetector> logger) : IPlatformPullRequestDetector
{
    private static readonly HashSet<string> _prEventNames =
        new(StringComparer.OrdinalIgnoreCase) { "pull_request", "pull_request_target" };

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
        if (string.IsNullOrEmpty(eventName) || !_prEventNames.Contains(eventName!))
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
        if (!TryGetRequired("GITHUB_SHA", out string? commitSha)) return null;
        if (!TryGetRequired("GITHUB_BASE_REF", out string? baseBranch)) return null;
        if (!TryGetRequired("GITHUB_HEAD_REF", out string? headBranch)) return null;

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
}
