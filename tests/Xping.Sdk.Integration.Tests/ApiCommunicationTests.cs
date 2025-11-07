/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Integration.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Integration tests for API communication with mock server.
/// </summary>
public sealed class ApiCommunicationTests : IDisposable
{
    private readonly MockApiServer _mockServer;

    public ApiCommunicationTests()
    {
        _mockServer = new MockApiServer();
    }

    [Fact]
    public async Task UploadAsync_WithValidTestExecutions_SendsCorrectRequest()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            Enabled = true
        };

        using var httpClient = new HttpClient();
        using var uploader = new XpingApiClient(httpClient, config);
        var testExecution = CreateTestExecution();

        // Act
        var result = await uploader.UploadAsync(new[] { testExecution });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var requests = _mockServer.ReceivedRequests;
        requests.Should().HaveCount(1);

        var request = requests[0];
        request.Method.Should().Be("POST");
        request.Headers.Should().ContainKey("X-API-Key");
        request.Headers["X-API-Key"].Should().Be("test-key");
        request.Headers.Should().ContainKey("X-Project-Id");
        request.Headers["X-Project-Id"].Should().Be("test-project");
        request.Body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadAsync_WithMultipleExecutions_BatchesCorrectly()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = 5,
            Enabled = true
        };

        using var httpClient = new HttpClient();
        using var uploader = new XpingApiClient(httpClient, config);
        var executions = Enumerable.Range(0, 5).Select(_ => CreateTestExecution()).ToArray();

        // Act
        var result = await uploader.UploadAsync(executions);

        // Assert
        result.Success.Should().BeTrue();
        _mockServer.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Collector_WithFlush_UploadsToApi()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = 100,
            Enabled = true
        };

        using var httpClient = new HttpClient();
        using var uploader = new XpingApiClient(httpClient, config);
        await using var collector = new TestExecutionCollector(uploader, config);

        // Act
        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        collector.RecordTest(CreateTestExecution("Test3"));
        await collector.FlushAsync();

        // Assert
        var requests = _mockServer.ReceivedRequests;
        requests.Should().HaveCount(1);
        requests[0].Body.Should().Contain("Test1");
        requests[0].Body.Should().Contain("Test2");
        requests[0].Body.Should().Contain("Test3");
    }

    [Fact]
    public async Task Collector_WithAutomaticFlush_UploadsWhenBatchSizeReached()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = 3,
            Enabled = true
        };

        using var httpClient = new HttpClient();
        using var uploader = new XpingApiClient(httpClient, config);
        await using var collector = new TestExecutionCollector(uploader, config);

        // Act
        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        collector.RecordTest(CreateTestExecution("Test3"));

        // Wait for automatic flush
        await Task.Delay(500);

        // Assert
        _mockServer.ReceivedRequests.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Uploader_WithServerError_ReturnsFailureResult()
    {
        // Arrange
        _mockServer.Behavior.FailureRate = 1.0;
        _mockServer.Behavior.FailureStatusCode = 500;

        var config = new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            MaxRetries = 1,
            Enabled = true
        };

        using var httpClient = new HttpClient();
        using var uploader = new XpingApiClient(httpClient, config);
        var testExecution = CreateTestExecution();

        // Act
        var result = await uploader.UploadAsync(new[] { testExecution });

        // Assert
        result.Success.Should().BeFalse();
        _mockServer.ReceivedRequests.Should().HaveCountGreaterThan(0);
    }

    private static TestExecution CreateTestExecution(string? testName = null)
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity
            {
                TestId = Guid.NewGuid().ToString(),
                FullyQualifiedName = "Test.Class.Method",
                Assembly = "Test.Assembly",
                Namespace = "Test",
                ClassName = "Class",
                MethodName = "Method"
            },
            TestName = testName ?? "TestMethod",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            SessionContext = new TestSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                EnvironmentInfo = new EnvironmentInfo()
            },
            Metadata = new TestMetadata()
        };
    }

    public void Dispose()
    {
        _mockServer.Dispose();
        GC.SuppressFinalize(this);
    }
}
