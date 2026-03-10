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
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        string commitSha,
        string baseBranch,
        string headBranch,
        string? author)
    {
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
    /// Gets the full SHA of the head commit that triggered the pull request build.
    /// </summary>
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
