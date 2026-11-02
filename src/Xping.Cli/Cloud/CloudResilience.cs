/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Xping.Cli.Cloud;

/// <summary>
/// The retry and per-attempt timeout of every DataGateway request (cloud-data-in-cli-cli-spec §3.2,
/// Amendment 1B).
/// </summary>
/// <remarks>
/// One retry, on a 5xx only, and only when the budget can still pay for it (contract §7.3). A 429
/// is never waited for and its <c>Retry-After</c> is not read: the report would stall for as long
/// as the server asks. 401, 403, 404 and 400 are answers, not failures. A network error or a timed
/// out attempt is not retried either: after a 3 s attempt at most 1 s of the budget is left.
/// </remarks>
internal static class CloudResilience
{
    /// <summary>
    /// The most retries of one request.
    /// </summary>
    public const int MaxRetryAttempts = 1;

    /// <summary>
    /// The wait before the retry.
    /// </summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The budget a retry needs left to be made at all; less, and it is skipped, not shortened.
    /// </summary>
    public static readonly TimeSpan MinimumForRetry = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Builds the pipeline on <paramref name="timeProvider"/>, retrying only inside <paramref name="budget"/>.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> Build(TimeProvider timeProvider, CloudBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);

        return new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = timeProvider }
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                Delay = RetryDelay,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldRetryAfterHeader = false,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result is { StatusCode: >= System.Net.HttpStatusCode.InternalServerError }
                    && budget.HasAtLeast(MinimumForRetry))
            })

            // Innermost, so each attempt gets its own limit.
            .AddTimeout(new HttpTimeoutStrategyOptions { Timeout = CloudHttp.AttemptTimeout })
            .Build();
    }
}
