/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Detects GitLab merge request context from GitLab CI/CD environment variables.
/// </summary>
/// <remarks>
/// Only merge request pipelines on gitlab.com carry MR context; branch and tag pipelines return
/// <c>null</c>. Self-managed instances return <c>null</c> too: the context names no host, so
/// Xping Cloud would attribute their projects to gitlab.com. Their pipelines are still flagged by
/// <c>CI.IsPullRequest</c>. Returns <c>null</c> when required variables are absent. Detection never throws.
/// <para>
/// <see cref="PullRequestContext.CommitSha"/> is the MR head commit. In merged-results and
/// merge-train pipelines <c>CI_COMMIT_SHA</c> is the merge result GitLab built, so
/// <c>CI_MERGE_REQUEST_SOURCE_BRANCH_SHA</c> is used when set; in plain MR pipelines it is not
/// set and <c>CI_COMMIT_SHA</c> is the head.
/// </para>
/// </remarks>
internal sealed class GitLabPullRequestDetector(
    IEnvironmentVariableProvider env,
    ILogger<GitLabPullRequestDetector> logger) : IPlatformPullRequestDetector
{
    private const string Platform = "GitLab";
    private const string GitLabComHost = "gitlab.com";

    /// <summary>
    /// Whether the environment is a GitLab merge request pipeline. <c>EnvironmentDetector</c> uses the
    /// same predicate for <c>CI.IsPullRequest</c>, so the two never disagree.
    /// </summary>
    internal static bool IsPullRequestBuild(Func<string, string?> getVariable) =>
        !string.IsNullOrEmpty(getVariable("CI_MERGE_REQUEST_IID"));

    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        try
        {
            return DetectCore();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GitLab PR detection failed unexpectedly; PR integration suppressed.");
            return null;
        }
    }

    private PullRequestContext? DetectCore()
    {
        if (!IsPullRequestBuild(env.GetVariable))
        {
            logger.LogDebug("GitLab PR detection skipped: CI_MERGE_REQUEST_IID is not set (not an MR pipeline).");
            return null;
        }

        if (!TryGetRequired("CI_SERVER_HOST", out string? serverHost))
            return null;

        if (!string.Equals(serverHost, GitLabComHost, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(
                "GitLab PR detection skipped: CI_SERVER_HOST='{ServerHost}' is a self-managed instance; " +
                "only gitlab.com is supported.",
                serverHost);
            return null;
        }

        if (!PullRequestEnvironment.TryGetRequiredNumber(env, logger, Platform, "CI_MERGE_REQUEST_IID", out int mrNumber))
            return null;

        if (!TryGetRequired("CI_MERGE_REQUEST_PROJECT_PATH", out string? projectPath))
            return null;

        if (!PullRequestEnvironment.TrySplitAtLastSlash(projectPath, out string? owner, out string? repoName))
        {
            logger.LogDebug(
                "GitLab PR detection skipped: CI_MERGE_REQUEST_PROJECT_PATH='{ProjectPath}' is not in 'namespace/project' format.",
                projectPath);
            return null;
        }

        if (!TryGetRequired("CI_MERGE_REQUEST_TARGET_BRANCH_NAME", out string? baseBranch)) return null;
        if (!TryGetRequired("CI_MERGE_REQUEST_SOURCE_BRANCH_NAME", out string? headBranch)) return null;

        string? commitSha = env.GetVariable("CI_MERGE_REQUEST_SOURCE_BRANCH_SHA");
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            if (!TryGetRequired("CI_COMMIT_SHA", out string? pipelineSha))
                return null;
            commitSha = pipelineSha;
        }

        string? author = env.GetVariable("GITLAB_USER_LOGIN");

        logger.LogDebug(
            "GitLab MR detected: {Owner}/{Repo}!{MR} on commit {Sha} ({Head} → {Base}).",
            owner, repoName, mrNumber, commitSha, headBranch, baseBranch);

        return new PullRequestContext(
            platform: PullRequestPlatform.GitLab,
            repositoryOwner: owner,
            repositoryName: repoName,
            pullRequestNumber: mrNumber,
            commitSha: commitSha!,
            baseBranch: baseBranch,
            headBranch: headBranch,
            author: author);
    }

    private bool TryGetRequired(string variable, [NotNullWhen(true)] out string? value) =>
        PullRequestEnvironment.TryGetRequired(env, logger, Platform, variable, out value);
}
