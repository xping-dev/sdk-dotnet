/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.PullRequests;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Tries each registered <see cref="IPlatformPullRequestDetector"/> in order and returns
/// the first non-null result. Returns <c>null</c> when no platform detector recognizes
/// the current CI/CD environment as a pull request build.
/// </summary>
internal sealed class CompositePullRequestContextDetector(
    IEnumerable<IPlatformPullRequestDetector> detectors) : IPullRequestContextDetector
{
    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        foreach (IPlatformPullRequestDetector detector in detectors)
        {
            PullRequestContext? context = detector.Detect();
            if (context != null)
                return context;
        }

        return null;
    }
}
