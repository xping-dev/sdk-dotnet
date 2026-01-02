/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Upload;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Diagnostics;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Serialization;

/// <summary>
/// HTTP client for uploading test executions to the Xping API with resilience policies.
/// </summary>
public sealed class XpingApiClient : ITestResultUploader, IDisposable
{
    private const int CompressionThresholdBytes = 1024; // 1KB

    private readonly HttpClient _httpClient;
    private readonly XpingConfiguration _config;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    private readonly IXpingSerializer _serializer;
    private readonly IXpingLogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API calls.</param>
    /// <param name="config">The Xping configuration.</param>
    /// <param name="serializer">Optional serializer. If not provided, uses default XpingJsonSerializer with ApiOptions.</param>
    /// <param name="logger">Optional logger for diagnostics. If null, uses NullLogger.</param>
    public XpingApiClient(
        HttpClient httpClient,
        XpingConfiguration config,
        IXpingSerializer? serializer = null,
        IXpingLogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _serializer = serializer ?? new XpingJsonSerializer(XpingSerializerOptions.ApiOptions);
        _logger = logger ?? XpingNullLogger.Instance;

        ConfigureHttpClient();
        _resiliencePipeline = BuildResiliencePipeline();
    }

    /// <summary>
    /// Uploads a batch of test executions to the Xping API.
    /// If the upload fails, an error is logged.
    /// </summary>
    /// <param name="executions">The test executions to upload.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the upload result.</returns>
    public async Task<UploadResult> UploadAsync(
        IEnumerable<TestExecution> executions,
        CancellationToken cancellationToken = default)
    {
        if (executions == null)
        {
            throw new ArgumentNullException(nameof(executions));
        }

        var executionList = executions as List<TestExecution> ?? [.. executions];
        if (executionList.Count == 0)
        {
            return new UploadResult
            {
                Success = true,
                ExecutionCount = 0,
            };
        }

        // Fast-fail if credentials are missing - no need to waste time on HTTP timeouts
        // This is especially important for test scenarios where credentials aren't configured
        if (string.IsNullOrWhiteSpace(_config.ApiKey) || string.IsNullOrWhiteSpace(_config.ProjectId))
        {
            var errorMsg = "Upload skipped: API Key and Project ID are required but not configured";
            _logger.LogError(errorMsg);
            _logger.LogInfo("Action: Configure credentials in appsettings.json or environment variables");
            _logger.LogInfo("  - Set XPING__APIKEY and XPING__PROJECTID environment variables");
            _logger.LogInfo("  - Or add 'Xping' section to appsettings.json");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = errorMsg,
            };
        }

        // Upload the batch
        var result = await UploadBatchAsync(executionList, cancellationToken).ConfigureAwait(false);

        // Log upload failures at debug level (root cause already logged at error level)
        if (!result.Success)
        {
            _logger.LogDebug($"Upload failed: {result.ErrorMessage}");
        }

        return result;
    }

    private async Task<UploadResult> UploadBatchAsync(
        List<TestExecution> executions,
        CancellationToken cancellationToken)
    {
        try
        {
            // Optimize batch: only the first execution contains full session context
            var optimizedExecutions = TestExecutionBatchOptimizer.OptimizeForTransport(executions);

            var response = await _resiliencePipeline.ExecuteAsync(
                async ct =>
                {
                    using var request = CreateUploadRequest(optimizedExecutions);
                    return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            using (response)
            {
                return await ProcessResponseAsync(response, executions.Count).ConfigureAwait(false);
            }
        }
        catch (BrokenCircuitException ex)
        {
            var errorMsg = "Circuit breaker is open: Too many consecutive failures";
            _logger.LogError(errorMsg);
            _logger.LogWarning("Status: Upload attempts will resume after circuit breaker resets (30 seconds)");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"{errorMsg}: {ex.Message}",
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Network error: {ex.Message}");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}",
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError($"Request timeout after {_config.UploadTimeout.TotalSeconds}s");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Request timeout: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Disposes the HTTP client resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient?.Dispose();
        _disposed = true;
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_config.ApiEndpoint);
        _httpClient.Timeout = _config.UploadTimeout;
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Project-Id", _config.ProjectId);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Xping-SDK-DotNet/1.0");
    }

    private ResiliencePipeline<HttpResponseMessage> BuildResiliencePipeline()
    {
        // Retry strategy with exponential backoff
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = _config.MaxRetries,
            Delay = _config.RetryDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response =>
                {
                    // Retry on 5xx errors and 429 (Too Many Requests)
                    var statusCode = (int)response.StatusCode;
                    return statusCode >= 500 || statusCode == 429;
                })
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>(),
        };

        // Circuit breaker to prevent cascading failures
        var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.5, // Open circuit if 50% of requests fail
            MinimumThroughput = 10, // Minimum 10 requests before evaluating
            BreakDuration = TimeSpan.FromSeconds(30), // Stay open for 30 seconds
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => (int)response.StatusCode >= 500)
                .Handle<HttpRequestException>(),
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .AddCircuitBreaker(circuitBreakerOptions)
            .Build();
    }

    private HttpRequestMessage CreateUploadRequest(IReadOnlyList<TestExecution> executions)
    {
        if (executions == null || executions.Count == 0)
        {
            throw new InvalidOperationException("Executions cannot be null or empty");
        }

        if (executions[0].SessionContext == null)
        {
            throw new InvalidOperationException("First execution must contain SessionContext");
        }

        var sessionId = executions[0].SessionContext!.SessionId; // Non-null due to check above
        var request = new HttpRequestMessage(
            HttpMethod.Post, _config.ApiEndpoint.TrimEnd('/') + "?sessionId=" + sessionId);
        var batch = new TestExecutionBatch { Executions = executions.ToList() };
        var json = _serializer.Serialize(batch);
        var content = Encoding.UTF8.GetBytes(json);

        // Compress if the payload is large enough
        if (_config.EnableCompression && content.Length > CompressionThresholdBytes)
        {
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Fastest))
            {
                gzipStream.Write(content, 0, content.Length);
            }

            request.Content = new ByteArrayContent(compressedStream.ToArray());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content.Headers.ContentEncoding.Add("gzip");
        }
        else
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task<UploadResult> ProcessResponseAsync(HttpResponseMessage response, int executionCount)
    {
        if (response.IsSuccessStatusCode)
        {
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>().ConfigureAwait(false);

            return new UploadResult
            {
                Success = true,
                ExecutionCount = apiResponse?.ExecutionCount ?? executionCount,
                ReceiptId = apiResponse?.ReceiptId,
            };
        }

        var statusCode = (int)response.StatusCode;
        var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        // Enhanced error messages with actionable guidance
        var errorMsg = statusCode switch
        {
            401 => "Authentication failed (401): Invalid API Key. " +
                   "Action: Verify credentials at https://app.xping.io",
            403 => "Authorization failed (403): Insufficient permissions. " +
                   "Action: Check project access at https://app.xping.io",
            429 => "Rate limit exceeded (429): Too many requests. " +
                   "Action: Reduce test execution frequency or contact support",
            >= 500 => $"Server error ({statusCode}): API temporarily unavailable.",
            _ => $"API returned {statusCode}: {errorContent}"
        };

        _logger.LogError(errorMsg);

        return new UploadResult
        {
            Success = false,
            ErrorMessage = errorMsg,
        };
    }

#pragma warning disable CA1812 // Response classes are instantiated by JSON deserializer
    private sealed class ApiResponse
    {
        public int ExecutionCount { get; set; }

        public string? ReceiptId { get; set; }
    }
#pragma warning restore CA1812
}
