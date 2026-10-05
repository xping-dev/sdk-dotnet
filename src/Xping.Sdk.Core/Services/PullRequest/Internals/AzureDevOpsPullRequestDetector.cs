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
/// Detects pull request context from Azure Pipelines environment variables, for repositories
/// hosted in Azure Repos or on github.com.
/// </summary>
/// <remarks>
/// Returns <c>null</c> for non-PR builds, other repository providers (Bitbucket, external Git, …),
/// Azure DevOps Server collections, GitHub repositories outside github.com (GitHub Enterprise
/// Server, GHE.com), or when required variables are absent. Detection never throws.
/// <para>
/// <see cref="PullRequestContext.CommitSha"/> is the PR head commit, from
/// <c>SYSTEM_PULLREQUEST_SOURCECOMMITID</c>. <c>BUILD_SOURCEVERSION</c> is not used: on a PR build
/// it is the merge commit Azure Pipelines built.
/// </para>
/// <para>
/// An Azure Repos repository is addressed by organization, project and repository, so its owner is
/// <c>{organization}/{project}</c> and its name is the repository. Xping Cloud parses the owner back
/// in that shape.
/// </para>
/// </remarks>
internal sealed class AzureDevOpsPullRequestDetector(
    IEnvironmentVariableProvider env,
    ILogger<AzureDevOpsPullRequestDetector> logger) : IPlatformPullRequestDetector
{
    private const string Platform = "Azure Pipelines";

    /// <summary>
    /// Whether the environment is an Azure Pipelines PR build. <c>EnvironmentDetector</c> uses the
    /// same predicate for <c>CI.IsPullRequest</c>, so the two never disagree.
    /// </summary>
    internal static bool IsPullRequestBuild(Func<string, string?> getVariable) =>
        string.Equals(getVariable("BUILD_REASON"), "PullRequest", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        try
        {
            return DetectCore();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Azure Pipelines PR detection failed unexpectedly; PR integration suppressed.");
            return null;
        }
    }

    private PullRequestContext? DetectCore()
    {
        if (!IsPullRequestBuild(env.GetVariable))
        {
            logger.LogDebug(
                "Azure Pipelines PR detection skipped: BUILD_REASON='{BuildReason}' (expected 'PullRequest').",
                env.GetVariable("BUILD_REASON"));
            return null;
        }

        if (!TryGetRequired("BUILD_REPOSITORY_PROVIDER", out string? provider)) return null;
        if (!TryGetRequired("BUILD_REPOSITORY_NAME", out string? repositoryName)) return null;

        PullRequestPlatform platform;
        string? owner;
        string? repoName;
        int prNumber;

        if (string.Equals(provider, "TfsGit", StringComparison.OrdinalIgnoreCase))
        {
            platform = PullRequestPlatform.AzureDevOps;
            if (!TryGetAzureReposOwner(out owner)) return null;
            repoName = repositoryName;
            if (!TryGetRequiredNumber("SYSTEM_PULLREQUEST_PULLREQUESTID", out prNumber)) return null;
        }
        else if (string.Equals(provider, "GitHub", StringComparison.OrdinalIgnoreCase))
        {
            platform = PullRequestPlatform.GitHub;
            if (!IsGitHubComRepository()) return null;

            if (!PullRequestEnvironment.TrySplitAtLastSlash(repositoryName, out owner, out repoName))
            {
                logger.LogDebug(
                    "Azure Pipelines PR detection skipped: BUILD_REPOSITORY_NAME='{Repository}' is not in 'owner/name' format.",
                    repositoryName);
                return null;
            }

            // For GitHub, PULLREQUESTID is GitHub's internal ID; the number users see is separate.
            if (!TryGetRequiredNumber("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER", out prNumber)) return null;
        }
        else
        {
            logger.LogDebug(
                "Azure Pipelines PR detection skipped: repository provider '{Provider}' is not supported.",
                provider);
            return null;
        }

        if (!TryGetRequiredBranch("SYSTEM_PULLREQUEST_TARGETBRANCH", out string? baseBranch)) return null;
        if (!TryGetRequiredBranch("SYSTEM_PULLREQUEST_SOURCEBRANCH", out string? headBranch)) return null;
        if (!TryGetRequired("SYSTEM_PULLREQUEST_SOURCECOMMITID", out string? commitSha)) return null;

        string? author = env.GetVariable("BUILD_REQUESTEDFOR");

        logger.LogDebug(
            "Azure Pipelines PR detected: {Owner}/{Repo}#{PR} on commit {Sha} ({Head} → {Base}).",
            owner, repoName, prNumber, commitSha, headBranch, baseBranch);

        return new PullRequestContext(
            platform: platform,
            repositoryOwner: owner,
            repositoryName: repoName,
            pullRequestNumber: prNumber,
            commitSha: commitSha,
            baseBranch: baseBranch,
            headBranch: headBranch,
            author: author);
    }

    // The context names no host, so a GitHub repository elsewhere (GHE.com, or GHES behind a
    // connection reporting provider GitHub) would be filed under github.com. GHES normally comes
    // through as provider GitHubEnterprise, which is rejected as unsupported.
    private bool IsGitHubComRepository()
    {
        if (!TryGetRequired("BUILD_REPOSITORY_URI", out string? repositoryUri)) return false;

        if (Uri.TryCreate(repositoryUri, UriKind.Absolute, out Uri? uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return true;

        logger.LogDebug(
            "Azure Pipelines PR detection skipped: BUILD_REPOSITORY_URI='{RepositoryUri}' is not a github.com " +
            "repository; GitHub Enterprise Server and GHE.com are not supported.",
            repositoryUri);
        return false;
    }

    private bool TryGetAzureReposOwner([NotNullWhen(true)] out string? owner)
    {
        owner = null;
        if (!TryGetRequired("SYSTEM_COLLECTIONURI", out string? collectionUri)) return false;
        if (!TryGetRequired("SYSTEM_TEAMPROJECT", out string? project)) return false;

        string? organization = ParseOrganization(collectionUri);
        if (organization is null)
        {
            logger.LogDebug(
                "Azure Pipelines PR detection skipped: SYSTEM_COLLECTIONURI='{CollectionUri}' is not an " +
                "Azure DevOps Services organization URL.",
                collectionUri);
            return false;
        }

        owner = organization + "/" + project;
        return true;
    }

    /// <summary>
    /// Extracts the organization from <c>https://dev.azure.com/{org}/</c> or the older
    /// <c>https://{org}.visualstudio.com/</c>. Any other host (Azure DevOps Server) yields <c>null</c>.
    /// </summary>
    internal static string? ParseOrganization(string collectionUri)
    {
        if (!Uri.TryCreate(collectionUri, UriKind.Absolute, out Uri? uri))
            return null;

        if (string.Equals(uri.Host, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            string organization = uri.AbsolutePath.Trim('/');
            return organization.Length == 0 || organization.IndexOf('/') >= 0 ? null : Uri.UnescapeDataString(organization);
        }

        const string legacySuffix = ".visualstudio.com";
        if (uri.Host.EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase))
        {
            string organization = uri.Host.Substring(0, uri.Host.Length - legacySuffix.Length);
            return organization.Length == 0 || organization.IndexOf('.') >= 0 ? null : organization;
        }

        return null;
    }

    private bool TryGetRequiredBranch(string variable, [NotNullWhen(true)] out string? branch)
    {
        branch = null;
        if (!TryGetRequired(variable, out string? raw))
            return false;

        // Azure Repos sends refs/heads/main, GitHub repos send main.
        string stripped = PullRequestEnvironment.StripHeadsPrefix(raw);
        if (stripped.Length == 0)
        {
            logger.LogDebug("Azure Pipelines PR detection skipped: {Variable}='{Value}' names no branch.", variable, raw);
            return false;
        }

        branch = stripped;
        return true;
    }

    private bool TryGetRequired(string variable, [NotNullWhen(true)] out string? value) =>
        PullRequestEnvironment.TryGetRequired(env, logger, Platform, variable, out value);

    private bool TryGetRequiredNumber(string variable, out int number) =>
        PullRequestEnvironment.TryGetRequiredNumber(env, logger, Platform, variable, out number);
}
