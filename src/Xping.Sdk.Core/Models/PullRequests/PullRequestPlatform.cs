/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.PullRequests;

/// <summary>
/// Identifies the source control platform from which the pull request originates.
/// </summary>
public enum PullRequestPlatform
{
    /// <summary>
    /// The platform could not be determined from the CI/CD environment.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// GitHub pull request.
    /// </summary>
    GitHub = 1,

    /// <summary>
    /// GitLab merge request.
    /// </summary>
    GitLab = 2,

    /// <summary>
    /// Azure DevOps pull request.
    /// </summary>
    AzureDevOps = 3
}
