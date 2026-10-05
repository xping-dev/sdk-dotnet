/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Detects pull request context on Jenkins multibranch (Branch Source) PR builds, for repositories on
/// github.com, gitlab.com or Azure DevOps Services.
/// </summary>
/// <remarks>
/// Platform, owner, name and number come from <c>CHANGE_URL</c>; other hosts return <c>null</c>.
/// GHPRB builds (<c>ghprbPullId</c>) are flagged by <c>CI.IsPullRequest</c> but get no context.
/// Detection never throws.
/// <para>
/// <see cref="PullRequestContext.CommitSha"/> is the PR head commit, which no Jenkins variable holds
/// under the default merge strategy. Jenkins checks out the PR head, then merges the target branch
/// into it with fast-forward allowed
/// (<see href="https://github.com/jenkinsci/git-plugin/blob/master/src/main/java/jenkins/plugins/git/MergeWithGitSCMExtension.java">MergeWithGitSCMExtension</see>),
/// so <c>GIT_COMMIT</c> is either the PR head or a local merge commit whose first parent is the PR
/// head. Branch Source also fetches the PR head into <c>refs/remotes/origin/{BRANCH_NAME}</c>
/// (<see href="https://github.com/jenkinsci/github-branch-source-plugin/blob/master/src/main/java/org/jenkinsci/plugins/github_branch_source/GitHubSCMBuilder.java">GitHubSCMBuilder</see>).
/// That ref is used only when it equals <c>GIT_COMMIT</c> or <c>GIT_COMMIT</c>'s first parent: a
/// push to the PR after Jenkins pinned the revision moves the ref past what was built, and a PR head
/// that is itself a merge has a second parent that must not be mistaken for it.
/// </para>
/// <para>
/// An opt-in variable naming the head was rejected because it needs pipeline changes few users
/// would make. The merge commit is created on the agent by <c>git merge</c>, so it is always a loose
/// object and no pack file has to be read.
/// </para>
/// </remarks>
internal sealed class JenkinsPullRequestDetector(
    IEnvironmentVariableProvider env,
    IGitRepositoryReader git,
    ILogger<JenkinsPullRequestDetector> logger) : IPlatformPullRequestDetector
{
    private const string Platform = "Jenkins";

    /// <summary>
    /// Whether the environment is a Branch Source PR build. <c>EnvironmentDetector</c> uses the same
    /// predicate for <c>CI.IsPullRequest</c>, so the two never disagree.
    /// </summary>
    internal static bool IsPullRequestBuild(Func<string, string?> getVariable) =>
        !string.IsNullOrEmpty(getVariable("CHANGE_ID"));

    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        try
        {
            return DetectCore();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Jenkins PR detection failed unexpectedly; PR integration suppressed.");
            return null;
        }
    }

    private PullRequestContext? DetectCore()
    {
        if (!IsPullRequestBuild(env.GetVariable))
        {
            logger.LogDebug("Jenkins PR detection skipped: CHANGE_ID is not set (not a Branch Source PR build).");
            return null;
        }

        if (!PullRequestEnvironment.TryGetRequiredNumber(env, logger, Platform, "CHANGE_ID", out int prNumber))
            return null;

        if (!TryGetRequired("CHANGE_URL", out string? changeUrl)) return null;

        ChangeUrl? parsed = ParseChangeUrl(changeUrl);
        if (parsed is null)
        {
            logger.LogDebug(
                "Jenkins PR detection skipped: CHANGE_URL='{ChangeUrl}' is not a github.com, gitlab.com or " +
                "Azure DevOps Services pull request URL.",
                changeUrl);
            return null;
        }

        if (parsed.Number != prNumber)
        {
            logger.LogDebug(
                "Jenkins PR detection skipped: CHANGE_URL='{ChangeUrl}' names a different PR than CHANGE_ID='{ChangeId}'.",
                changeUrl, prNumber);
            return null;
        }

        if (!TryGetRequired("CHANGE_TARGET", out string? baseBranch)) return null;
        if (!TryGetRequired("CHANGE_BRANCH", out string? headBranch)) return null;
        if (!TryGetHeadCommit(out string? commitSha)) return null;

        string? author = env.GetVariable("CHANGE_AUTHOR");

        logger.LogDebug(
            "Jenkins PR detected: {Owner}/{Repo}#{PR} on commit {Sha} ({Head} → {Base}).",
            parsed.Owner, parsed.Name, prNumber, commitSha, headBranch, baseBranch);

        return new PullRequestContext(
            platform: parsed.Platform,
            repositoryOwner: parsed.Owner,
            repositoryName: parsed.Name,
            pullRequestNumber: prNumber,
            commitSha: commitSha,
            baseBranch: baseBranch,
            headBranch: headBranch,
            author: author);
    }

    private bool TryGetHeadCommit([NotNullWhen(true)] out string? commitSha)
    {
        commitSha = null;
        if (!TryGetRequired("WORKSPACE", out string? workspace)) return false;
        if (!TryGetRequired("BRANCH_NAME", out string? branchName)) return false;
        if (!TryGetRequired("GIT_COMMIT", out string? builtCommit)) return false;

        string refName = "refs/remotes/origin/" + branchName;
        string? head = git.ResolveRef(workspace, refName);
        if (head is null)
        {
            logger.LogDebug(
                "Jenkins PR detection skipped: {Ref} was not found in the repository at WORKSPACE='{Workspace}'.",
                refName, workspace);
            return false;
        }

        if (string.Equals(head, builtCommit, StringComparison.OrdinalIgnoreCase))
        {
            commitSha = head; // Head strategy, or a merge strategy build that fast-forwarded.
            return true;
        }

        string? firstParent = git.GetFirstParent(workspace, builtCommit);
        if (string.Equals(head, firstParent, StringComparison.OrdinalIgnoreCase))
        {
            commitSha = head; // Merge strategy: GIT_COMMIT is the merge of the target into the PR head.
            return true;
        }

        logger.LogDebug(
            "Jenkins PR detection skipped: {Ref}={Head} is neither GIT_COMMIT={Built} nor its first parent " +
            "({FirstParent}); the PR head that was built can't be proven.",
            refName, head, builtCommit, firstParent ?? "unreadable");
        return false;
    }

    /// <summary>
    /// Parses a Branch Source <c>CHANGE_URL</c>. Owners and names match what the GitHub, GitLab and Azure
    /// Pipelines detectors produce for the same repository.
    /// </summary>
    internal static ChangeUrl? ParseChangeUrl(string changeUrl)
    {
        if (!Uri.TryCreate(changeUrl, UriKind.Absolute, out Uri? uri))
            return null;

        string[] segments = uri.AbsolutePath.Trim('/').Split('/');
        string host = uri.Host;

        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
            return ParseGitHub(segments);

        if (string.Equals(host, "gitlab.com", StringComparison.OrdinalIgnoreCase))
            return ParseGitLab(segments);

        if (string.Equals(host, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            // dev.azure.com/{org}/{project}/_git/{repo}/pullrequest/{n}
            if (segments.Length != 6) return null;
            string? organization = AzureDevOpsPullRequestDetector.ParseOrganization(
                uri.GetLeftPart(UriPartial.Authority) + "/" + segments[0]);
            return ParseAzureRepos(organization, segments, 1);
        }

        if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            // {org}.visualstudio.com/[DefaultCollection/]{project}/_git/{repo}/pullrequest/{n}
            int projectIndex = segments.Length == 6
                && string.Equals(segments[0], "DefaultCollection", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (segments.Length != projectIndex + 5) return null;
            string? organization = AzureDevOpsPullRequestDetector.ParseOrganization(uri.GetLeftPart(UriPartial.Authority));
            return ParseAzureRepos(organization, segments, projectIndex);
        }

        return null;
    }

    private static ChangeUrl? ParseGitHub(string[] segments)
    {
        // github.com/{owner}/{repo}/pull/{n}
        if (segments.Length != 4 || !string.Equals(segments[2], "pull", StringComparison.Ordinal))
            return null;

        return Create(PullRequestPlatform.GitHub, Decode(segments[0]), Decode(segments[1]), segments[3]);
    }

    private static ChangeUrl? ParseGitLab(string[] segments)
    {
        // gitlab.com/{group…}/{project}/-/merge_requests/{n}
        int dash = Array.IndexOf(segments, "-");
        if (dash < 2 || segments.Length != dash + 3
            || !string.Equals(segments[dash + 1], "merge_requests", StringComparison.Ordinal))
            return null;

        string projectPath = string.Join("/", segments.Take(dash).Select(Decode));
        if (!PullRequestEnvironment.TrySplitAtLastSlash(projectPath, out string? owner, out string? name))
            return null;

        return Create(PullRequestPlatform.GitLab, owner, name, segments[dash + 2]);
    }

    private static ChangeUrl? ParseAzureRepos(string? organization, string[] segments, int projectIndex)
    {
        // {project}/_git/{repo}/pullrequest/{n}, starting at projectIndex.
        if (organization is null
            || !string.Equals(segments[projectIndex + 1], "_git", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[projectIndex + 3], "pullrequest", StringComparison.OrdinalIgnoreCase))
            return null;

        // Same owner shape as AzureDevOpsPullRequestDetector: {organization}/{project}.
        string owner = organization + "/" + Decode(segments[projectIndex]);
        return Create(PullRequestPlatform.AzureDevOps, owner, Decode(segments[projectIndex + 2]), segments[projectIndex + 4]);
    }

    private static ChangeUrl? Create(PullRequestPlatform platform, string owner, string name, string number)
    {
        if (owner.Length == 0 || name.Length == 0
            || !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0)
            return null;

        return new ChangeUrl(platform, owner, name, value);
    }

    private static string Decode(string segment) => Uri.UnescapeDataString(segment);

    private bool TryGetRequired(string variable, [NotNullWhen(true)] out string? value) =>
        PullRequestEnvironment.TryGetRequired(env, logger, Platform, variable, out value);

    /// <summary>The repository and PR number a <c>CHANGE_URL</c> points at.</summary>
    internal sealed record ChangeUrl(PullRequestPlatform Platform, string Owner, string Name, int Number);
}
