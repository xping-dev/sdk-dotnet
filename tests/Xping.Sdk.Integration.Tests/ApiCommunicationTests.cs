/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

// Use the NUnit adapter's XpingContext as an independent static singleton,
// separate from the XUnit context initialized by XpingTestFramework.
using XpingCtx = Xping.Sdk.NUnit.XpingContext;

/// <summary>
/// Integration tests for API communication through the Xping pipeline against a local mock server.
/// </summary>
public sealed class ApiCommunicationTests : IAsyncLifetime, IDisposable
{
    private readonly MockApiServer _mockServer = new();

    // xUnit calls DisposeAsync before Dispose, so each cleans up its own resource.
    public Task InitializeAsync()
    {
        // Reset the NUnit context before each test so each test gets a fresh singleton
        return XpingCtx.ShutdownAsync().AsTask();
    }

    public Task DisposeAsync()
    {
        // Async XpingContext cleanup; MockApiServer is handled by Dispose()
        return XpingCtx.ShutdownAsync().AsTask();
    }

    public void Dispose()
    {
        _mockServer.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WithValidTestExecution_SendsCorrectRequestHeaders()
    {
        // Arrange
        XpingCtx.Initialize(CreateConfig());
        XpingCtx.RecordTest(CreateTestExecution());

        // Act
        await XpingCtx.FlushAsync().ConfigureAwait(true);

        // Assert
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
    public async Task FlushAsync_WithMultipleExecutions_SendsOneRequest()
    {
        // Arrange
        XpingCtx.Initialize(CreateConfig(batchSize: 5));
        foreach (var i in Enumerable.Range(0, 5))
            XpingCtx.RecordTest(CreateTestExecution($"Test{i}"));

        // Act
        await XpingCtx.FlushAsync().ConfigureAwait(true);

        // Assert
        _mockServer.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task FlushAsync_AfterRecordingMultipleTests_SendsAllToApi()
    {
        // Arrange
        XpingCtx.Initialize(CreateConfig(batchSize: 100));
        XpingCtx.RecordTest(CreateTestExecution("Test1"));
        XpingCtx.RecordTest(CreateTestExecution("Test2"));
        XpingCtx.RecordTest(CreateTestExecution("Test3"));

        // Act
        await XpingCtx.FlushAsync().ConfigureAwait(true);

        // Assert
        var requests = _mockServer.ReceivedRequests;
        requests.Should().HaveCount(1);
        requests[0].Body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FlushAsync_WithServerError_DoesNotThrow()
    {
        // Arrange
        _mockServer.Behavior.FailureRate = 1.0;
        _mockServer.Behavior.FailureStatusCode = 500;

        XpingCtx.Initialize(CreateConfig(maxRetries: 1));
        XpingCtx.RecordTest(CreateTestExecution());

        // Act
        var exception = await Record.ExceptionAsync(async () =>
            await XpingCtx.FlushAsync().ConfigureAwait(true)).ConfigureAwait(true);

        // Assert
        exception.Should().BeNull();
        _mockServer.ReceivedRequests.Should().HaveCountGreaterThan(0);
    }

    private XpingConfiguration CreateConfig(int batchSize = 100, int maxRetries = 1)
    {
        return new XpingConfiguration
        {
            ApiEndpoint = _mockServer.BaseUrl + "api/v1/test-executions",
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = batchSize,
            MaxRetries = maxRetries,
            Enabled = true
        };
    }

    private static TestExecution CreateTestExecution(string? testName = null)
    {
        return new TestExecutionBuilder()
            .WithTestName(testName ?? "TestMethod")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .Build();
    }
}
