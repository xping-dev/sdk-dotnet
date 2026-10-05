/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.PullRequests;

/// <summary>
/// Immutable pull request or merge request context detected from the CI/CD environment.
/// </summary>
public sealed class PullRequestContext
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public PullRequestContext()
    {
        Platform = PullRequestPlatform.Unknown;
        ServerUrl = string.Empty;
        RepositoryOwner = string.Empty;
        RepositoryName = string.Empty;
        PullRequestNumber = 0;
        CommitSha = string.Empty;
        BaseBranch = string.Empty;
        HeadBranch = string.Empty;
        Author = null;
    }

    /// <summary>
    /// Internal constructor for creation by the pull request context detector.
    /// </summary>
    internal PullRequestContext(
        PullRequestPlatform platform,
        string serverUrl,
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        string commitSha,
        string baseBranch,
        string headBranch,
        string? author)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("Server URL must not be empty.", nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(repositoryOwner))
            throw new ArgumentException("Repository owner must not be empty.", nameof(repositoryOwner));
        if (string.IsNullOrWhiteSpace(repositoryName))
            throw new ArgumentException("Repository name must not be empty.", nameof(repositoryName));
        if (string.IsNullOrWhiteSpace(commitSha))
            throw new ArgumentException("Commit SHA must not be empty.", nameof(commitSha));
        if (string.IsNullOrWhiteSpace(baseBranch))
            throw new ArgumentException("Base branch must not be empty.", nameof(baseBranch));
        if (string.IsNullOrWhiteSpace(headBranch))
            throw new ArgumentException("Head branch must not be empty.", nameof(headBranch));

        Platform = platform;
        ServerUrl = serverUrl;
        RepositoryOwner = repositoryOwner;
        RepositoryName = repositoryName;
        PullRequestNumber = pullRequestNumber;
        CommitSha = commitSha;
        BaseBranch = baseBranch;
        HeadBranch = headBranch;
        Author = author;
    }

    /// <summary>
    /// Gets the source control platform (GitHub, GitLab, Azure DevOps).
    /// </summary>
    public PullRequestPlatform Platform { get; init; }

    /// <summary>
    /// Gets the web root of the server hosting the repository, e.g. <c>https://github.com</c>,
    /// <c>https://ghes.example.com</c>, <c>https://gitlab.example.com:8443/gitlab</c>,
    /// <c>https://dev.azure.com</c> or <c>https://tfs.example.com/tfs</c>.
    /// </summary>
    /// <remarks>
    /// Normalized to scheme, lowercase host, non-default port and path, with no trailing <c>/</c>, so
    /// every detector reports the same value for the same server. A repository is identified by this
    /// URL together with <see cref="RepositoryOwner"/> and <see cref="RepositoryName"/>: the same
    /// <c>owner/name</c> on GitHub Enterprise and on github.com are different repositories.
    /// </remarks>
    public string ServerUrl { get; init; }

    /// <summary>
    /// Gets the repository owner (organization or user login).
    /// </summary>
    public string RepositoryOwner { get; init; }

    /// <summary>
    /// Gets the repository name (without the owner prefix).
    /// </summary>
    public string RepositoryName { get; init; }

    /// <summary>
    /// Gets the pull request or merge request number.
    /// </summary>
    public int PullRequestNumber { get; init; }

    /// <summary>
    /// Gets the full SHA of the pull request's head commit: the commit the author pushed.
    /// </summary>
    /// <remarks>
    /// This is not always the commit the build tested. GitHub <c>pull_request</c>, Azure Pipelines PR
    /// builds and GitLab merged-results pipelines build a merge commit, which the <c>CI.CommitSha</c>
    /// entry of <see cref="Environments.EnvironmentInfo.CustomProperties"/> carries. When a pull
    /// request context is present, consumers key the run by this commit, not by <c>CI.CommitSha</c>.
    /// </remarks>
    public string CommitSha { get; init; }

    /// <summary>
    /// Gets the name of the target branch (e.g. <c>main</c>).
    /// </summary>
    public string BaseBranch { get; init; }

    /// <summary>
    /// Gets the name of the source branch (e.g. <c>feature/new-feature</c>).
    /// </summary>
    public string HeadBranch { get; init; }

    /// <summary>
    /// Gets the username of the pull request author, or <c>null</c> if not available.
    /// </summary>
    public string? Author { get; init; }
}
