/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Xping.Cli.Cloud;

/// <summary>
/// The retries and per-attempt timeout of every DataGateway request (cli-auth-cli-spec §10.3).
/// </summary>
/// <remarks>
/// Like the SDK uploader's pipeline, without the circuit breaker: the process lives for one command.
/// 401, 403, 404 and 400 are never retried; they are answers, not failures.
/// </remarks>
internal static class CloudResilience
{
    /// <summary>
    /// The longest <c>Retry-After</c> honoured; a longer one gives up rather than stall the command.
    /// </summary>
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Builds the pipeline on <paramref name="timeProvider"/>.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> Build(TimeProvider timeProvider) =>
        new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = timeProvider }
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,

                // Retry-After is read below for 429 only, and capped; honoured on a 5xx it could
                // stall the command for as long as the server asks.
                ShouldRetryAfterHeader = false,
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome, args.AttemptNumber, timeProvider)),
                DelayGenerator = args => ValueTask.FromResult(
                    args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response
                        ? RetryAfter(response, timeProvider)
                        : null)
            })

            // Innermost, so each attempt gets its own limit.
            .AddTimeout(new HttpTimeoutStrategyOptions { Timeout = CloudHttp.AttemptTimeout })
            .Build();

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome, int attemptNumber, TimeProvider timeProvider) =>
        outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response

            // Contract §8.2: wait Retry-After and retry once, then fail.
            ? attemptNumber == 0 && RetryAfter(response, timeProvider) is not null

            // 5xx, 408, network failures and the attempt timeout; never 400, 401, 403 or 404.
            : HttpClientResiliencePredicates.IsTransient(outcome);

    private static TimeSpan? RetryAfter(HttpResponseMessage response, TimeProvider timeProvider)
    {
        TimeSpan? delay = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - timeProvider.GetUtcNow(),
            _ => null
        };

        return delay switch
        {
            null => null,
            { } d when d <= TimeSpan.Zero => TimeSpan.Zero,
            { } d when d <= MaxRetryAfter => d,
            _ => null
        };
    }
}
