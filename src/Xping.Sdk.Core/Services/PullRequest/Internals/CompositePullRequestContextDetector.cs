/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.PullRequests;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Tries each registered <see cref="IPlatformPullRequestDetector"/> in order and returns
/// the first non-null result. Returns <c>null</c> when no platform detector recognizes
/// the current CI/CD environment as a pull request build.
/// </summary>
internal sealed class CompositePullRequestContextDetector(
    IEnumerable<IPlatformPullRequestDetector> detectors,
    ILogger<CompositePullRequestContextDetector> logger) : IPullRequestContextDetector
{
    private readonly IEnumerable<IPlatformPullRequestDetector> _detectors =
        detectors ?? Enumerable.Empty<IPlatformPullRequestDetector>();

    /// <inheritdoc/>
    public PullRequestContext? Detect()
    {
        foreach (IPlatformPullRequestDetector detector in _detectors)
        {
            try
            {
                PullRequestContext? context = detector.Detect();
                if (context != null)
                    return context;
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "PR detector '{DetectorType}' threw unexpectedly; skipping.",
                    detector.GetType().Name);
            }
        }

        return null;
    }
}
