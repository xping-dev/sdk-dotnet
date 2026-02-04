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
using Xping.Sdk.Core.Diagnostics;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;
using Xunit;

public sealed class XpingApiClientTests
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
        Assert.Contains("Server error (500)", result.ErrorMessage, StringComparison.Ordinal);
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
        Assert.Contains("400", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithNotFoundError_ReturnsSpecificErrorMessage()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.NotFound,
            "Endpoint not found");

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
        Assert.Contains("API endpoint not found (404)", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Verify the ApiEndpoint configuration", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithEmptyErrorContent_ReturnsFallbackMessage()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.BadRequest,
            string.Empty);

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
        Assert.Contains("API returned 400: No additional error details provided", result.ErrorMessage, StringComparison.Ordinal);
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

        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1"),
        };

        await client.UploadAsync(executions);

        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            expected: new Uri($"{config.ApiEndpoint}?sessionId={executions[0].SessionContext?.SessionId}").PathAndQuery,
            actual: request.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task UploadAsync_WithFirstErrorOccurrence_LogsDetailedMessage()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            "Invalid API key");

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1, // Minimum retries
        };

        var mockLogger = new MockLogger();
        using var client = new XpingApiClient(httpClient, config, logger: mockLogger);

        var result = await client.UploadAsync(new[] { CreateTestExecution() });

        Assert.False(result.Success);
        Assert.Contains("Authentication failed (401): Invalid API Key", result.ErrorMessage, StringComparison.Ordinal);

        // Verify detailed message is logged on first occurrence
        var errorLogs = mockLogger.ErrorMessages;
        Assert.Single(errorLogs);
        Assert.Contains("Authentication failed (401): Invalid API Key", errorLogs[0], StringComparison.Ordinal);
        Assert.Contains("Verify credentials at https://app.xping.io", errorLogs[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithSubsequentIdenticalErrors_LogsAbbreviatedMessage()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.NotFound,
            "Endpoint not found");

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1, // Minimum retries
        };

        var mockLogger = new MockLogger();
        using var client = new XpingApiClient(httpClient, config, logger: mockLogger);

        // First upload - should log detailed message
        var result1 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result1.Success);

        // Second upload with same error - should log abbreviated message
        var result2 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result2.Success);

        // Third upload with same error - should log abbreviated message
        var result3 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result3.Success);

        var errorLogs = mockLogger.ErrorMessages;
        Assert.Equal(3, errorLogs.Count);

        // First occurrence - detailed message
        Assert.Contains("API endpoint not found (404)", errorLogs[0], StringComparison.Ordinal);
        Assert.Contains("Verify the ApiEndpoint configuration", errorLogs[0], StringComparison.Ordinal);

        // Second occurrence - abbreviated message with ordinal
        Assert.Contains("Same 404 error (2nd occurrence, batch size: 1)", errorLogs[1], StringComparison.Ordinal);

        // Third occurrence - abbreviated message with ordinal
        Assert.Contains("Same 404 error (3rd occurrence, batch size: 1)", errorLogs[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithCorrectOrdinalFormatting_LogsProperOrdinals()
    {
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            "Rate limited");

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1,
        };

        var mockLogger = new MockLogger();
        using var client = new XpingApiClient(httpClient, config, logger: mockLogger);

        // Test various ordinals: 1st, 2nd, 3rd, 4th, 11th, 21st, 22nd, 23rd
        for (int i = 1; i <= 23; i++)
        {
            await client.UploadAsync(new[] { CreateTestExecution() });
        }

        var errorLogs = mockLogger.ErrorMessages;
        Assert.Equal(23, errorLogs.Count);

        // First occurrence has detailed message
        Assert.Contains("Rate limit exceeded (429)", errorLogs[0], StringComparison.Ordinal);

        // Check specific ordinals
        Assert.Contains("2nd occurrence", errorLogs[1], StringComparison.Ordinal);
        Assert.Contains("3rd occurrence", errorLogs[2], StringComparison.Ordinal);
        Assert.Contains("4th occurrence", errorLogs[3], StringComparison.Ordinal);
        Assert.Contains("10th occurrence", errorLogs[9], StringComparison.Ordinal);
        Assert.Contains("11th occurrence", errorLogs[10], StringComparison.Ordinal);
        Assert.Contains("12th occurrence", errorLogs[11], StringComparison.Ordinal);
        Assert.Contains("13th occurrence", errorLogs[12], StringComparison.Ordinal);
        Assert.Contains("21st occurrence", errorLogs[20], StringComparison.Ordinal);
        Assert.Contains("22nd occurrence", errorLogs[21], StringComparison.Ordinal);
        Assert.Contains("23rd occurrence", errorLogs[22], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_AfterSuccessfulUpload_ResetsErrorTracking()
    {
        var callCount = 0;
        using var handler = new MockHttpMessageHandler((req) =>
        {
            callCount++;
            if (callCount == 1 || callCount == 2)
            {
                // First two calls fail with 500
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error", Encoding.UTF8, "application/json"),
                };
            }
            else if (callCount == 3)
            {
                // Third call succeeds
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { executionCount = 1 }), Encoding.UTF8, "application/json"),
                };
            }
            else
            {
                // Fourth call fails again with same 500 error
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error", Encoding.UTF8, "application/json"),
                };
            }
        });

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1,
        };

        var mockLogger = new MockLogger();
        using var client = new XpingApiClient(httpClient, config, logger: mockLogger);

        // First failure - detailed message
        var result1 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result1.Success);

        // Second failure - abbreviated message (2nd occurrence)
        var result2 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result2.Success);

        // Success - should reset error tracking
        var result3 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.True(result3.Success);

        // Fourth failure - should be treated as first occurrence again (detailed message)
        var result4 = await client.UploadAsync(new[] { CreateTestExecution() });
        Assert.False(result4.Success);

        var errorLogs = mockLogger.ErrorMessages;
        Assert.Equal(3, errorLogs.Count); // 3 failures total

        // First occurrence
        Assert.Contains("Server error (500)", errorLogs[0], StringComparison.Ordinal);
        Assert.DoesNotContain("2nd occurrence", errorLogs[0], StringComparison.Ordinal);

        // Second occurrence
        Assert.Contains("Same 500 error (2nd occurrence", errorLogs[1], StringComparison.Ordinal);

        // After success, error tracking is reset - should be treated as first occurrence
        Assert.Contains("Server error (500)", errorLogs[2], StringComparison.Ordinal);
        Assert.DoesNotContain("3rd occurrence", errorLogs[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WithDifferentErrorKeys_TracksIndependently()
    {
        var callCount = 0;
        using var handler = new MockHttpMessageHandler((req) =>
        {
            callCount++;
            return callCount switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Invalid API key", Encoding.UTF8, "application/json"),
                },
                2 => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Not found", Encoding.UTF8, "application/json"),
                },
                3 => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Invalid API key", Encoding.UTF8, "application/json"),
                },
                4 => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Not found", Encoding.UTF8, "application/json"),
                },
                5 => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Different error", Encoding.UTF8, "application/json"),
                },
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            };
        });

        using var httpClient = new HttpClient(handler);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            ApiEndpoint = "https://api.test.com",
            MaxRetries = 1,
        };

        var mockLogger = new MockLogger();
        using var client = new XpingApiClient(httpClient, config, logger: mockLogger);

        // First 401 with "Invalid API key"
        await client.UploadAsync(new[] { CreateTestExecution() });

        // First 404 with "Not found"
        await client.UploadAsync(new[] { CreateTestExecution() });

        // Second 401 with "Invalid API key" - should be 2nd occurrence
        await client.UploadAsync(new[] { CreateTestExecution() });

        // Second 404 with "Not found" - should be 2nd occurrence
        await client.UploadAsync(new[] { CreateTestExecution() });

        // First 401 with "Different error" - different content, so first occurrence
        await client.UploadAsync(new[] { CreateTestExecution() });

        var errorLogs = mockLogger.ErrorMessages;
        Assert.Equal(5, errorLogs.Count);

        // First 401 - detailed message
        Assert.Contains("Authentication failed (401)", errorLogs[0], StringComparison.Ordinal);
        Assert.DoesNotContain("occurrence", errorLogs[0], StringComparison.Ordinal);

        // First 404 - detailed message
        Assert.Contains("API endpoint not found (404)", errorLogs[1], StringComparison.Ordinal);
        Assert.DoesNotContain("occurrence", errorLogs[1], StringComparison.Ordinal);

        // Second 401 with same content - abbreviated
        Assert.Contains("Same 401 error (2nd occurrence", errorLogs[2], StringComparison.Ordinal);

        // Second 404 with same content - abbreviated
        Assert.Contains("Same 404 error (2nd occurrence", errorLogs[3], StringComparison.Ordinal);

        // First 401 with different content - detailed message (new error key)
        Assert.Contains("Authentication failed (401)", errorLogs[4], StringComparison.Ordinal);
        Assert.DoesNotContain("occurrence", errorLogs[4], StringComparison.Ordinal);
    }

    private sealed class MockLogger : IXpingLogger
    {
        public List<string> ErrorMessages { get; } = new();
        public List<string> WarningMessages { get; } = new();
        public List<string> InfoMessages { get; } = new();
        public List<string> DebugMessages { get; } = new();

        public void LogError(string message)
        {
            ErrorMessages.Add(message);
        }

        public void LogWarning(string message)
        {
            WarningMessages.Add(message);
        }

        public void LogInfo(string message)
        {
            InfoMessages.Add(message);
        }

        public void LogDebug(string message)
        {
            DebugMessages.Add(message);
        }

        public bool IsEnabled(XpingLogLevel level)
        {
            return true;
        }
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
