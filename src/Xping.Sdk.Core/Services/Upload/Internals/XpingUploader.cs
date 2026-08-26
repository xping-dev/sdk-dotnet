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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.Serialization;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Services.Upload.Internals;

internal sealed class XpingUploader(
    HttpClient httpClient,
    IOptions<XpingConfiguration> options,
    ILogger<XpingUploader> logger,
    IXpingSerializer serializer) : IXpingUploader
{
    private const int CompressionThresholdBytes = 1024; // 1KB

    private static readonly JsonSerializerOptions ProblemDetailsSerializerOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly XpingConfiguration _configuration = options.Value;
    private readonly ConcurrentDictionary<string, int> _errorOccurrences = new();

    /// <inheritdoc/>
    async Task<UploadResult> IXpingUploader.UploadAsync(
        TestSession testSession,
        CancellationToken cancellationToken)
    {
        testSession.RequireNotNull();

        if (testSession.Executions.Count == 0 && testSession.SessionState != TestSessionState.Finalized)
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
            TestSessionState sessionState = testSession.SessionState;
            string requestUrl = _configuration.ApiEndpoint;

            (HttpRequestMessage request, long payloadSizeBytes) = CreateUploadRequest(testSession);
            using (request)
            {
                var sw = Stopwatch.StartNew();
                using HttpResponseMessage? response =
                    await httpClient
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                sw.Stop();

                return await ProcessResponseAsync(
                    response, executionsCount, sessionState, requestUrl,
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
        string sessionId = testSession
            .RequireNotNull()
            .SessionId
            .ToString();

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
        TestSessionState sessionState,
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

            if (sessionState == TestSessionState.Finalized)
            {
                logger.LogInformation(
                    "Finalization receipt {ReceiptId} ({DurationMs}ms, {PayloadKB:F1} KB)",
                    shortReceipt,
                    durationMs,
                    payloadSizeBytes / 1024.0);
            }
            else
            {
                logger.LogInformation(
                    "Published {TotalRecords} tests in {DurationMs}ms ({PayloadKB:F1} KB) · receipt {ReceiptId}",
                    confirmedCount,
                    durationMs,
                    payloadSizeBytes / 1024.0,
                    shortReceipt);
            }

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
        string detailedErrorMsg = BuildErrorMessage(statusCode, errorContent, baseUrl);

        // Log detailed message on the first occurrence, abbreviated on later
        if (occurrenceCount == 1)
        {
            logger.LogError("{DetailedErrorMessage}", detailedErrorMsg);
        }
        else
        {
            logger.LogError("Same HTTP {StatusCode} error ({Ordinal} occurrence, batch size: {TotalRecords})",
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

    /// <summary>
    /// Builds an actionable error message from a failed upload response.
    /// </summary>
    /// <remarks>
    /// The API reports failures as RFC 7807 ProblemDetails, where <c>title</c> carries the server
    /// error code (for example <c>Error.Subscription.ProjectLimitReached</c>) and <c>detail</c> a
    /// human-readable explanation. The error code is the authoritative signal: a single status code
    /// covers unrelated causes, so 403 is not necessarily a missing scope and 402 is not necessarily
    /// a failed payment. The status code is only used when no recognizable code is present.
    /// </remarks>
    private static string BuildErrorMessage(int statusCode, string? errorContent, string baseUrl)
    {
        ProblemDetails? problem = TryParseProblemDetails(errorContent);
        string? errorCode = problem?.Title;

        string action = GetActionForErrorCode(errorCode, baseUrl)
            ?? GetActionForStatusCode(statusCode, baseUrl);

        // Prefer the server's explanation - it carries the specifics the SDK cannot know,
        // such as which plan limit was hit and what the limit is.
        string reason = problem?.Detail
            ?? (string.IsNullOrWhiteSpace(errorContent) ? "No additional error details provided" : errorContent!);

        string category = GetCategoryForStatusCode(statusCode);
        string code = string.IsNullOrWhiteSpace(errorCode) ? string.Empty : $" [{errorCode}]";

        return $"{category} (HTTP {statusCode}) for {baseUrl}{code}: {reason} Action: {action}";
    }

    /// <summary>
    /// Maps a server error code to the action that resolves it. Returns <c>null</c> for codes the
    /// SDK does not recognize, so the caller can fall back to status-code guidance.
    /// </summary>
    private static string? GetActionForErrorCode(string? errorCode, string baseUrl) => errorCode switch
    {
        null or "" => null,

        // Credentials - the key itself is the problem
        "Error.ApiKey.MissingApiKey" =>
            "Set the ApiKey in your Xping configuration; it is sent as the X-API-Key header",
        "Error.ApiKey.InvalidApiKey" =>
            $"Verify the ApiKey value matches a key issued for this workspace at {baseUrl}",
        "Error.ApiKey.ExpiredApiKey" =>
            "The key has passed its expiry date - issue a new API key and update your configuration",
        "Error.ApiKey.RevokedApiKey" =>
            "This key was revoked - issue a new API key and update your configuration",

        // Scope and access - the key is valid but not permitted for uploads
        "Error.ApiKey.InsufficientScope" or "Authorization.InsufficientScope" =>
            "Grant the API key the UPLOAD scope, or use a key that already has it",
        "Authorization.InsufficientAccess" or "Error.Security.OperationRequiresApiKeyContext" =>
            "This endpoint requires an API key principal with upload access",
        "Error.ApiKey.IpNotAllowed" =>
            "The key restricts client IP addresses - add this runner's public IP to the key's allow list",

        // Quotas - the caller is authorized, the plan or the key cap is the limit
        "Error.ApiKey.UsageLimitReached" =>
            "This key reached its configured usage cap - raise the cap or use a different key",
        "Error.ApiKey.RateLimitExceeded" =>
            "Back off and retry later, or reduce upload frequency by batching more executions per upload",
        "Error.Subscription.ProjectLimitReached" =>
            "This run names a project the workspace does not have yet and the project limit is " +
            "reached - upgrade the plan, or set ProjectId to report into an existing project. " +
            "A solution-wide run reports one project per test assembly, so it can request several at once",
        "Error.Subscription.TestRunLimitReached" or "Error.Subscription.UsageLimitExceeded" =>
            "Upgrade the plan to continue ingesting test results this billing period",
        "Error.Billing.PaymentFailed" or "Error.Billing.CardDeclined" or "Error.Billing.InsufficientFunds" =>
            "Update the workspace payment method to restore uploads",

        // Project state
        "Error.Project.ProjectDisabled" =>
            "The target project is disabled and rejects uploads - re-enable it in Xping Cloud",

        // Request content - a configuration or SDK-version problem
        "Error.Uploads.MissingProjectKey" =>
            "The upload named no project: no ProjectId is configured and no test assembly could be " +
            "resolved for this session. Set ProjectId to pin one, or check that your tests report " +
            "an assembly name",
        "Error.Uploads.MissingSessionId" =>
            "The upload was sent without a session id - this indicates an SDK defect, please report it",
        "Error.Uploads.UnrecognizedPayload" or "Error.Uploads.UnrecognizedState" =>
            "The server could not read this payload - update Xping.Sdk to a version compatible with the API",
        "Error.Uploads.BatchSizeExceeded" =>
            "Reduce the number of executions per upload - the server accepts at most 1000 per batch",

        _ => null
    };

    /// <summary>
    /// Fallback guidance when the response carries no recognizable error code - for example when a
    /// proxy or load balancer, rather than the API, produced the response.
    /// </summary>
    private static string GetActionForStatusCode(int statusCode, string baseUrl) => statusCode switch
    {
        400 or 422 => "Check the upload configuration (ApiEndpoint) and the SDK version",
        401 => $"Verify the ApiKey is set and valid for {baseUrl}",
        402 => "Review the workspace subscription - a plan limit or billing issue is blocking uploads",
        403 => "Verify the API key has the UPLOAD scope and that this client's IP is permitted",
        404 => "Verify the ApiEndpoint configuration matches your deployment",
        409 => "The target project rejected the upload in its current state - check it in Xping Cloud",
        413 => "Reduce the number of executions per upload",
        429 => "Reduce test execution frequency, or retry after the interval the server indicates",
        >= 500 => "Retry later; if this persists, contact support with the receipt of a failing run",
        _ => "Inspect the response body for details"
    };

    private static string GetCategoryForStatusCode(int statusCode) => statusCode switch
    {
        400 or 422 => "Upload rejected",
        401 => "Authentication failed",
        402 => "Subscription limit reached",
        403 => "Authorization failed",
        404 => "API endpoint not found",
        409 => "Upload conflicts with the current project state",
        413 => "Payload too large",
        429 => "Rate limit exceeded",
        >= 500 => "Server error",
        _ => "Upload failed"
    };

    private static ProblemDetails? TryParseProblemDetails(string? errorContent)
    {
        if (string.IsNullOrWhiteSpace(errorContent))
        {
            return null;
        }

        try
        {
            ProblemDetails? problem = JsonSerializer.Deserialize<ProblemDetails>(
                errorContent!,
                ProblemDetailsSerializerOptions);

            // A non-ProblemDetails JSON body (or a bare literal) deserializes without throwing,
            // so treat a payload that carries neither field as unparsed.
            return problem is null || (problem.Title is null && problem.Detail is null) ? null : problem;
        }
        catch (JsonException)
        {
            // Not JSON at all - a proxy error page, for example. The raw body is still logged.
            return null;
        }
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

    /// <summary>
    /// RFC 7807 problem response returned by the API on failure. <see cref="Title"/> carries the
    /// server error code and <see cref="Detail"/> the human-readable explanation.
    /// </summary>
    private sealed class ProblemDetails
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
#pragma warning restore CA1812
}
