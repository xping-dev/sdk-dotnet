/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;

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
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome, args.AttemptNumber, timeProvider)),
                DelayGenerator = args => ValueTask.FromResult(
                    args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response
                        ? RetryAfter(response, timeProvider)
                        : null)
            })

            // Innermost, so each attempt gets its own limit.
            .AddTimeout(CloudHttp.AttemptTimeout)
            .Build();

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome, int attemptNumber, TimeProvider timeProvider) =>
        outcome switch
        {
            { Exception: HttpRequestException or TimeoutRejectedException } => true,

            // Contract §8.2: wait Retry-After and retry once, then fail.
            { Result: { StatusCode: HttpStatusCode.TooManyRequests } response } =>
                attemptNumber == 0 && RetryAfter(response, timeProvider) is not null,
            { Result: { } response } => (int)response.StatusCode >= 500,
            _ => false
        };

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
