/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.PullRequests;

namespace Xping.Sdk.Core.Services.PullRequest;

/// <summary>
/// Detects pull request or merge request context from the current CI/CD environment.
/// </summary>
public interface IPullRequestContextDetector
{
    /// <summary>
    /// Attempts to detect pull request context from the CI/CD environment variables.
    /// </summary>
    /// <returns>
    /// A <see cref="PullRequestContext"/> when running inside a pull request build,
    /// or <c>null</c> when not in a PR context or when required variables are absent.
    /// </returns>
    /// <remarks>
    /// Implementations must never throw. Detection failures are silently absorbed and
    /// represented as a <c>null</c> return value so that the host test process is unaffected.
    /// </remarks>
    PullRequestContext? Detect();
}
