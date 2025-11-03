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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Persistence;

/// <summary>
/// HTTP client for uploading test executions to the Xping API with resilience policies.
/// </summary>
public sealed class XpingApiClient : ITestResultUploader, IDisposable
{
    private const int CompressionThresholdBytes = 1024; // 1KB
    private const string ApiVersion = "v1";
    private const int MaxDequeueBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient;
    private readonly XpingConfiguration _config;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    private readonly IOfflineQueue _offlineQueue;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API calls.</param>
    /// <param name="config">The Xping configuration.</param>
    /// <param name="offlineQueue">Optional offline queue for failed uploads.</param>
    public XpingApiClient(HttpClient httpClient, XpingConfiguration config, IOfflineQueue offlineQueue = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _offlineQueue = offlineQueue;

        ConfigureHttpClient();
        _resiliencePipeline = BuildResiliencePipeline();
    }

    /// <summary>
    /// Uploads a batch of test executions to the Xping API.
    /// If the upload fails and an offline queue is configured, the executions are queued.
    /// Before uploading, any queued executions are processed first.
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

        var executionList = executions as List<TestExecution> ?? executions.ToList();

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
            return new UploadResult
            {
                Success = false,
                ErrorMessage = "Upload skipped: API Key and Project ID are required but not configured",
            };
        }        // Try to process any queued items first
        if (_offlineQueue != null)
        {
            await ProcessQueuedExecutionsAsync(cancellationToken).ConfigureAwait(false);
        }

        // Now upload the current batch
        var result = await UploadBatchAsync(executionList, cancellationToken).ConfigureAwait(false);

        // If upload failed and we have an offline queue, enqueue for later
        if (!result.Success && _offlineQueue != null)
        {
            try
            {
                await _offlineQueue.EnqueueAsync(executionList, cancellationToken).ConfigureAwait(false);
                result.ErrorMessage += " (queued for retry)";
            }
            catch (Exception ex)
            {
                result.ErrorMessage += $" (failed to queue: {ex.Message})";
            }
        }

        return result;
    }

    private async Task<UploadResult> UploadBatchAsync(
        List<TestExecution> executions,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct =>
                {
                    using var request = CreateUploadRequest(executions);
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
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Circuit breaker is open: {ex.Message}",
            };
        }
        catch (HttpRequestException ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}",
            };
        }
        catch (TaskCanceledException ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Request timeout: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
            };
        }
    }

    private async Task ProcessQueuedExecutionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queueSize = await _offlineQueue.GetQueueSizeAsync(cancellationToken).ConfigureAwait(false);
            if (queueSize == 0)
            {
                return;
            }

            // Process queued items in batches
            while (queueSize > 0)
            {
                var batch = (await _offlineQueue.DequeueAsync(MaxDequeueBatchSize, cancellationToken)
                    .ConfigureAwait(false)).ToList();

                if (batch.Count == 0)
                {
                    break;
                }

                var result = await UploadBatchAsync(batch, cancellationToken).ConfigureAwait(false);

                // If upload failed, re-queue and stop processing
                if (!result.Success)
                {
                    await _offlineQueue.EnqueueAsync(batch, cancellationToken).ConfigureAwait(false);
                    break;
                }

                queueSize -= batch.Count;
            }
        }
        catch
        {
            // Errors during queue processing should not prevent current upload
        }
    }

    /// <summary>
    /// Disposes the HTTP client resources and offline queue.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient?.Dispose();
        (_offlineQueue as IDisposable)?.Dispose();
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

    private HttpRequestMessage CreateUploadRequest(List<TestExecution> executions)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/{ApiVersion}/test-executions");

        var json = JsonSerializer.Serialize(new { executions }, JsonOptions);

        var content = Encoding.UTF8.GetBytes(json);

        // Compress if payload is large enough
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

        var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return new UploadResult
        {
            Success = false,
            ErrorMessage = $"API returned {response.StatusCode}: {errorContent}",
        };
    }

#pragma warning disable CA1812 // ApiResponse is instantiated by JSON deserializer
    private sealed class ApiResponse
    {
        public int ExecutionCount { get; set; }

        public string ReceiptId { get; set; }
    }
#pragma warning restore CA1812
}
