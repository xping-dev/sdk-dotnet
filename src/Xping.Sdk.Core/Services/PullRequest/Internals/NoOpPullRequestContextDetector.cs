/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.PullRequests;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// A no-op implementation of <see cref="IPullRequestContextDetector"/> used when the SDK
/// is disabled, configuration validation fails, or PR detection is turned off.
/// Always returns <c>null</c>, suppressing all PR integration behaviour.
/// </summary>
internal sealed class NoOpPullRequestContextDetector : IPullRequestContextDetector
{
    /// <inheritdoc/>
    public PullRequestContext? Detect() => null;
}
