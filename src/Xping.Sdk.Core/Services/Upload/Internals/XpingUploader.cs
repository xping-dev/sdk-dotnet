/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.Upload.Internals;

internal sealed class XpingUploader(
    HttpClient httpClient,
    IOptions<XpingConfiguration> options,
    ILogger<XpingUploader> logger,
    IXpingSerializer serializer) : IXpingUploader
{
    private const int CompressionThresholdBytes = 1024; // 1KB

    private readonly XpingConfiguration _configuration = options.Value;
    private readonly ConcurrentDictionary<string, int> _errorOccurrences = new();

    /// <inheritdoc/>
    async Task<UploadResult> IXpingUploader.UploadAsync(
        TestSession testSession,
        CancellationToken cancellationToken)
    {
        if (testSession == null)
        {
            throw new ArgumentNullException(nameof(testSession));
        }

        if (testSession.Executions.Count == 0)
        {
            return new UploadResult
            {
                Success = true,
                TotalRecordsCount = 0,
            };
        }

        // Upload the batch
        UploadResult? result = await UploadBatchAsync(testSession, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<UploadResult> UploadBatchAsync(
        TestSession testSession,
        CancellationToken cancellationToken)
    {
        try
        {
            int executionsCount = testSession.Executions.Count;
            string requestUrl = _configuration.ApiEndpoint;

            var (request, payloadSizeBytes) = CreateUploadRequest(testSession);
            using (request)
            {
                var sw = Stopwatch.StartNew();
                using HttpResponseMessage? response =
                    await httpClient
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                sw.Stop();

                return await ProcessResponseAsync(
                    response, executionsCount, requestUrl,
                    sw.ElapsedMilliseconds, payloadSizeBytes).ConfigureAwait(false);
            }
        }
        catch (BrokenCircuitException ex)
        {
            const string ErrorMsg = "Circuit breaker is open: Too many consecutive failures";
            logger.LogError(ex, "{ErrorMessage}", ErrorMsg);
            logger.LogWarning("Status: Upload attempts will resume after circuit breaker resets (30 seconds)");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"{ErrorMsg}: {ex.Message}",
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, message: "Network error occurred");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}",
            };
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(
                ex, message: "Request timeout after {TimeoutSeconds}s", _configuration.UploadTimeout.TotalSeconds);

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Request timeout: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, message: "Unexpected error occurred");

            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
            };
        }
    }

    private (HttpRequestMessage Request, long PayloadSizeBytes) CreateUploadRequest(TestSession testSession)
    {
        if (testSession == null || testSession.Executions.Count == 0)
        {
            throw new InvalidOperationException("Executions cannot be null or empty");
        }

        string sessionId = testSession.SessionId;
#pragma warning disable CA2000 // Caller owns and disposes the request via using (request)
        HttpRequestMessage request = new(
            HttpMethod.Post,
            requestUri: _configuration.ApiEndpoint.TrimEnd('/') + "?sessionId=" + sessionId);
#pragma warning restore CA2000

        string json = serializer.Serialize(testSession);
        byte[] content = Encoding.UTF8.GetBytes(json);
        long payloadSizeBytes = content.Length;

        // Compress if the payload is large enough and enabled
        if (_configuration.EnableCompression && content.Length > CompressionThresholdBytes)
        {
            using MemoryStream compressedStream = new();
            using (GZipStream gzipStream = new(compressedStream, CompressionLevel.Fastest))
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

        return (request, payloadSizeBytes);
    }

    private async Task<UploadResult> ProcessResponseAsync(
        HttpResponseMessage response,
        int executionCount,
        string requestUrl,
        long durationMs,
        long payloadSizeBytes)
    {
        if (response.IsSuccessStatusCode)
        {
            // Reset error tracking on a successful upload
            _errorOccurrences.Clear();

            ApiResponse? apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>().ConfigureAwait(false);

            int confirmedCount = apiResponse?.TotalRecords ?? executionCount;
            string? receiptId = apiResponse?.ReceiptId;
            string shortReceipt = receiptId is { Length: > 8 }
                ? receiptId.Substring(0, 8)
                : receiptId ?? "n/a";

            logger.LogInformation(
                "Published {TotalRecords} tests in {DurationMs}ms ({PayloadKB:F1} KB) · receipt {ReceiptId}",
                confirmedCount,
                durationMs,
                payloadSizeBytes / 1024.0,
                shortReceipt);

            return new UploadResult
            {
                Success = true,
                TotalRecordsCount = confirmedCount,
                ReceiptId = receiptId,
                DurationMs = (int)durationMs,
                PayloadSizeBytes = payloadSizeBytes,
            };
        }

        int statusCode = (int)response.StatusCode;
        string? errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        // Track error occurrences across different upload attempts (not within a single upload's retry cycle).
        // This helps reduce log noise for persistent errors (e.g., invalid credentials, authorization issues)
        // that occur repeatedly across multiple separate calls to UploadAsync.
        // Note: Retry attempts within a single upload happen inside the resilience pipeline before this method is called.
        string errorKey = $"{statusCode}:{GetErrorContentKey(errorContent)}";
        int occurrenceCount = _errorOccurrences.AddOrUpdate(errorKey, 1, (_, count) => count + 1);

        // Extract base URL without query parameters for cleaner error messages
        string baseUrl = GetBaseUrl(requestUrl);

        // Enhanced error messages with actionable guidance
        string detailedErrorMsg = statusCode switch
        {
            401 => $"Authentication failed (401) for {baseUrl}: Invalid API Key. " +
                   "Action: Verify credentials at https://app.xping.io",
            403 => $"Authorization failed (403) for {baseUrl}: Insufficient permissions. " +
                   "Action: Check project access at https://app.xping.io",
            404 => $"API endpoint not found (404): {baseUrl}. " +
                   "Action: Verify the ApiEndpoint configuration matches your deployment",
            429 => "Rate limit exceeded (429): Too many requests. " +
                   "Action: Reduce test execution frequency or contact support",
            >= 500 => $"Server error ({statusCode}): API temporarily unavailable.",
            _ => string.IsNullOrWhiteSpace(errorContent)
                ? $"API returned {statusCode}: No additional error details provided"
                : $"API returned {statusCode}: {errorContent}"
        };

        // Log detailed message on the first occurrence, abbreviated on later
        if (occurrenceCount == 1)
        {
            logger.LogError("{DetailedErrorMessage}", detailedErrorMsg);
        }
        else
        {
            logger.LogError("Same {StatusCode} error ({Ordinal} occurrence, batch size: {TotalRecords})",
                statusCode, GetOrdinal(occurrenceCount), executionCount);
        }

        return new UploadResult
        {
            Success = false,
            ErrorMessage = detailedErrorMsg,
        };
    }

    private static string GetBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        }

        return url;
    }

    private static string GetErrorContentKey(string? errorContent)
    {
        if (string.IsNullOrWhiteSpace(errorContent))
        {
            return "empty";
        }

        const int MaxLength = 200;
        string trimmedStart = errorContent!.TrimStart();

        // Truncate first to avoid trimming unnecessary characters
        string truncated = trimmedStart.Length <= MaxLength
            ? trimmedStart
            : trimmedStart.Substring(0, MaxLength);

        return truncated.TrimEnd();
    }

    private static string GetOrdinal(int number)
    {
        if (number <= 0)
            return $"{number}";

        return (number % 100) switch
        {
            11 or 12 or 13 => $"{number}th",
            _ => (number % 10) switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            }
        };
    }

#pragma warning disable CA1812 // Response classes are instantiated by JSON deserializer
    private sealed class ApiResponse
    {
        public int TotalRecords { get; set; }

        public string? ReceiptId { get; set; }
    }
#pragma warning restore CA1812
}
