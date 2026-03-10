/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Marker interface for platform-specific pull request detectors (GitHub, GitLab, Azure DevOps, etc.).
/// Implementations are consumed exclusively by <see cref="CompositePullRequestContextDetector"/>;
/// external code depends on <see cref="IPullRequestContextDetector"/> instead.
/// </summary>
internal interface IPlatformPullRequestDetector : IPullRequestContextDetector
{
}
