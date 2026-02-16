/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly.CircuitBreaker;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Upload;

namespace Xping.Sdk.Core.Tests.Upload;

public sealed class XpingUploaderTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal fake message handler — returns the supplied response or throws the supplied exception.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHttpMessageHandler(HttpResponseMessage response)
            => _respond = _ => response;

        // Factory variant: creates a fresh response per call (required for retryable status codes).
        public FakeHttpMessageHandler(Func<HttpResponseMessage> factory)
            => _respond = _ => factory();

        public FakeHttpMessageHandler(Exception exception)
            => _respond = _ => throw exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }

    /// <summary>
    /// Builds a real IXpingUploader via DI, injecting a custom primary handler.
    /// MaxRetries = 1 is the minimum valid value accepted by Polly.
    /// </summary>
    private static IXpingUploader BuildUploader(
        HttpMessageHandler fakeHandler,
        Action<XpingConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o =>
        {
            o.ApiKey = "test-key";
            o.ProjectId = "test-project";
            o.MaxRetries = 1;
            configure?.Invoke(o);
        });
        services.AddXpingSerialization();
        services.AddLogging();
        services.AddXpingUploader();

        // Replace the innermost HTTP handler. After PostConfigureAll runs, the pipeline is:
        //   [Polly retry/circuit-breaker DelegatingHandlers] → fakeHandler (PrimaryHandler)
        services.PostConfigureAll<HttpClientFactoryOptions>(opts =>
            opts.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = fakeHandler));

        return services.BuildServiceProvider().GetRequiredService<IXpingUploader>();
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object? body = null)
    {
        var json = body != null ? JsonSerializer.Serialize(body) : string.Empty;
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static TestSession BuildSession(int executionCount = 1)
    {
        var builder = new TestSessionBuilder();
        for (var i = 0; i < executionCount; i++)
        {
            builder.AddExecution(
                new TestExecutionBuilder()
                    .WithTestName($"Test{i}")
                    .WithOutcome(TestOutcome.Passed)
                    .Build());
        }
        return builder.Build();
    }

    // ---------------------------------------------------------------------------
    // UploadAsync — guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_NullSession_ShouldThrowArgumentNullException()
    {
        using var response = JsonResponse(HttpStatusCode.OK);
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);
        await Assert.ThrowsAsync<ArgumentNullException>(() => uploader.UploadAsync(null!));
    }

    [Fact]
    public async Task UploadAsync_EmptySession_ShouldReturnSuccess_WithZeroCount()
    {
        // The uploader short-circuits when the session has no executions.
        using var response = JsonResponse(HttpStatusCode.OK);
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);
        var emptySession = new TestSessionBuilder().Build();

        var result = await uploader.UploadAsync(emptySession);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExecutionCount);
    }

    // ---------------------------------------------------------------------------
    // UploadAsync — success path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_200Response_ShouldReturnSuccessResult()
    {
        using var response = JsonResponse(HttpStatusCode.OK, new { totalRecords = 1, receiptId = "r001" });
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession(1));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UploadAsync_200Response_ShouldParse_TotalRecords_FromApiResponse()
    {
        using var response = JsonResponse(HttpStatusCode.OK, new { totalRecords = 3, receiptId = (string?)null });
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession(3));

        Assert.Equal(3, result.ExecutionCount);
    }

    [Fact]
    public async Task UploadAsync_200Response_ShouldSet_ReceiptId()
    {
        using var response = JsonResponse(HttpStatusCode.OK, new { totalRecords = 1, receiptId = "receipt-xyz" });
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession(1));

        Assert.Equal("receipt-xyz", result.ReceiptId);
    }

    // ---------------------------------------------------------------------------
    // UploadAsync — HTTP error responses
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_401Response_ShouldReturnFailure_WithAuthErrorMessage()
    {
        using var response = JsonResponse(HttpStatusCode.Unauthorized);
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("401", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_403Response_ShouldReturnFailure_WithAuthorizationMessage()
    {
        using var response = JsonResponse(HttpStatusCode.Forbidden);
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.Contains("403", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_404Response_ShouldReturnFailure_WithEndpointMessage()
    {
        using var response = JsonResponse(HttpStatusCode.NotFound);
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.Contains("404", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_429Response_ShouldReturnFailure_WithRateLimitMessage()
    {
        // 429 is retryable — factory creates a fresh response for each Polly attempt.
        using var handler = new FakeHttpMessageHandler(() => JsonResponse(HttpStatusCode.TooManyRequests));
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.Contains("429", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_500Response_ShouldReturnFailure_WithServerErrorMessage()
    {
        // 500 is retryable — factory creates a fresh response for each Polly attempt.
        using var handler = new FakeHttpMessageHandler(() => JsonResponse(HttpStatusCode.InternalServerError));
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.Contains("500", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_UnknownStatusCode_ShouldReturnFailure_WithStatusCode()
    {
        using var response = JsonResponse((HttpStatusCode)418); // I'm a teapot
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.Contains("418", result.ErrorMessage!, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // UploadAsync — exceptions
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_HttpRequestException_ShouldReturnFailureResult()
    {
        using var handler = new FakeHttpMessageHandler(new HttpRequestException("Network error"));
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("HTTP request failed", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_TaskCanceledException_ShouldReturnFailureResult()
    {
        using var handler = new FakeHttpMessageHandler(new TaskCanceledException("Timeout"));
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_BrokenCircuitException_ShouldReturnFailureResult()
    {
        // BrokenCircuitException is NOT in the retry/CB ShouldHandle lists, so it
        // passes through Polly and is caught by the catch(BrokenCircuitException) block.
        using var handler = new FakeHttpMessageHandler(new BrokenCircuitException("circuit open"));
        var uploader = BuildUploader(handler);

        var result = await uploader.UploadAsync(BuildSession());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Circuit breaker", result.ErrorMessage, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // UploadAsync — compression
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_LargePayload_WithCompressionEnabled_ShouldSucceed()
    {
        // payload > 1 KB threshold + EnableCompression = true → uses GZip path
        using var response = JsonResponse(HttpStatusCode.OK, new { totalRecords = 1, receiptId = "gz" });
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler, o =>
        {
            o.EnableCompression = true;
            o.BatchSize = 200;
        });

        // Build a session with enough data to exceed the 1 KB compression threshold
        var builder = new TestSessionBuilder();
        for (var i = 0; i < 20; i++)
        {
            builder.AddExecution(
                new TestExecutionBuilder()
                    .WithTestName(new string('X', 60) + i)
                    .WithOutcome(TestOutcome.Passed)
                    .Build());
        }
        var largeSession = builder.Build();

        var result = await uploader.UploadAsync(largeSession);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UploadAsync_SmallPayload_WithCompressionEnabled_ShouldSucceed()
    {
        // Small payload stays below the 1 KB threshold even with compression enabled
        using var response = JsonResponse(HttpStatusCode.OK, new { totalRecords = 1, receiptId = "small" });
        using var handler = new FakeHttpMessageHandler(response);
        var uploader = BuildUploader(handler, o => o.EnableCompression = true);

        var result = await uploader.UploadAsync(BuildSession(1));

        Assert.True(result.Success);
    }
}
