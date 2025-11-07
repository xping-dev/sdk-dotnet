/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA2007 // Do not directly await a Task

namespace Xping.Sdk.Core.Tests.Upload;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;
using Xunit;

public class XpingApiClientTests
{
    private static TestExecution CreateTestExecution(string testName = "Test1")
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = testName,
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow,
            SessionContext = new TestSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                EnvironmentInfo = new EnvironmentInfo()
            },
            Metadata = new TestMetadata(),
        };
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        Assert.Throws<ArgumentNullException>(() => new XpingApiClient(null!, config));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<ArgumentNullException>(() => new XpingApiClient(httpClient, null!));
    }

    [Fact]
    public async Task UploadAsync_WithNullExecutions_ThrowsArgumentNullException()
    {
        using var httpClient = new HttpClient();
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        using var client = new XpingApiClient(httpClient, config);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadAsync(null!));
    }

    [Fact]
    public async Task UploadAsync_WithEmptyList_ReturnsSuccessWithZeroCount()
    {
        using var httpClient = new HttpClient();
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        using var client = new XpingApiClient(httpClient, config);

        var result = await client.UploadAsync(new List<TestExecution>());

        Assert.True(result.Success);
        Assert.Equal(0, result.ExecutionCount);
    }

    [Fact]
    public async Task UploadAsync_WithSuccessfulResponse_ReturnsSuccess()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 3, receiptId = "receipt-123" }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            ApiEndpoint = "https://api.test.com",
        };
        using var client = new XpingApiClient(httpClient, config);

        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1"),
            CreateTestExecution("Test2"),
            CreateTestExecution("Test3"),
        };

        var result = await client.UploadAsync(executions);

        Assert.True(result.Success);
        Assert.Equal(3, result.ExecutionCount);
        Assert.Equal("receipt-123", result.ReceiptId);
    }

    [Fact]
    public async Task UploadAsync_SetsCorrectHeaders()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 1 }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test-api-key",
            ProjectId = "test-project-id",
            ApiEndpoint = "https://api.test.com",
        };
        using var client = new XpingApiClient(httpClient, config);

        await client.UploadAsync(new[] { CreateTestExecution() });

        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal("test-api-key", request.Headers.GetValues("X-API-Key").First());
        Assert.Equal("test-project-id", request.Headers.GetValues("X-Project-Id").First());
        Assert.Contains("Xping-SDK-DotNet", request.Headers.GetValues("User-Agent").First(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithSmallPayload_DoesNotCompress()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 1 }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            EnableCompression = true,
        };
        using var client = new XpingApiClient(httpClient, config);

        await client.UploadAsync(new[] { CreateTestExecution() });

        var request = handler.LastRequest;
        Assert.NotNull(request?.Content);
        Assert.DoesNotContain("gzip", request.Content.Headers.ContentEncoding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithLargePayload_CompressesContent()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 100 }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            EnableCompression = true,
        };
        using var client = new XpingApiClient(httpClient, config);

        // Create many executions to exceed 1KB threshold
        var executions = Enumerable.Range(0, 100).Select(i => CreateTestExecution($"Test{i}")).ToList();

        await client.UploadAsync(executions);

        var request = handler.LastRequest;
        Assert.NotNull(request?.Content);
        Assert.Contains("gzip", request.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task UploadAsync_WithCompressionDisabled_DoesNotCompress()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 100 }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            EnableCompression = false,
        };
        using var client = new XpingApiClient(httpClient, config);

        var executions = Enumerable.Range(0, 100).Select(i => CreateTestExecution($"Test{i}")).ToList();

        await client.UploadAsync(executions);

        var request = handler.LastRequest;
        Assert.NotNull(request?.Content);
        Assert.DoesNotContain("gzip", request.Content.Headers.ContentEncoding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithServerError_ReturnsFailure()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "Internal server error");

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1, // Minimum retries
        };
        using var client = new XpingApiClient(httpClient, config);

        var result = await client.UploadAsync(new[] { CreateTestExecution() });

        Assert.False(result.Success);
        Assert.Contains("InternalServerError", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithBadRequest_ReturnsFailure()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.BadRequest,
            "Bad request");

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
        };
        using var client = new XpingApiClient(httpClient, config);

        var result = await client.UploadAsync(new[] { CreateTestExecution() });

        Assert.False(result.Success);
        Assert.Contains("BadRequest", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithNetworkError_ReturnsFailure()
    {
        using var handler = new MockHttpMessageHandler(throwException: true);

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1, // Minimum retries
        };
        using var client = new XpingApiClient(httpClient, config);

        var result = await client.UploadAsync(new[] { CreateTestExecution() });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task UploadAsync_WithRetriableError_RetriesRequest()
    {
        var callCount = 0;
        using var handler = new MockHttpMessageHandler((req) =>
        {
            callCount++;
            if (callCount < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { executionCount = 1 })),
            };
        });

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromMilliseconds(10),
        };
        using var client = new XpingApiClient(httpClient, config);

        var result = await client.UploadAsync(new[] { CreateTestExecution() });

        Assert.True(result.Success);
        Assert.Equal(3, callCount); // 1 initial + 2 retries
    }

    [Fact]
    public async Task UploadAsync_PostsToCorrectEndpoint()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Created,
            JsonSerializer.Serialize(new { executionCount = 1 }));

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
        };
        using var client = new XpingApiClient(httpClient, config);

        await client.UploadAsync(new[] { CreateTestExecution() });

        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/test-executions", request.RequestUri?.PathAndQuery);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _responseContent;
        private readonly bool _throwException;
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responseFunc;

        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string responseContent = "",
            bool throwException = false)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
            _throwException = throwException;
        }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
        {
            _responseFunc = responseFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (_throwException)
            {
                throw new HttpRequestException("Network error");
            }

            if (_responseFunc != null)
            {
                return Task.FromResult(_responseFunc(request));
            }

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent ?? string.Empty, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}

#pragma warning restore CA2007
#pragma warning restore CA1707
