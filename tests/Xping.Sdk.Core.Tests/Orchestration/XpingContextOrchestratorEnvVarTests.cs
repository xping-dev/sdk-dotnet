/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Tests that mutate the XPING_STRICTMODE environment variable.  They are placed in the
/// "Sequential" xUnit collection (<see cref="SequentialCollection"/>) so that they never run
/// concurrently with other test classes that construct an orchestrator and could observe a
/// stale env-var value.
/// </summary>
[Collection("Sequential")]
public sealed class XpingContextOrchestratorEnvVarTests
{
    // Minimal concrete subclass that exposes enough surface for these tests.
    private sealed class TestOrchestrator : XpingContextOrchestrator
    {
        public TestOrchestrator(IHost host) : base(host) { }

        public Task<UploadResult> FinalizeAsync(CancellationToken ct = default)
            => FinalizeSessionAsync(ct);

        public void RecordExecution(TestExecution e) => RecordTestExecution(e);
    }

    [Fact]
    public void Constructor_WithInvalidConfig_AndStrictModeViaEnvVar_ThrowsXpingConfigurationException()
    {
        // Arrange — set XPING_STRICTMODE before creating the host
        System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", "true");
        try
        {
            var host = ServiceHelper.BuildInvalidConfigHost();

            // Act & Assert
            Assert.Throws<XpingConfigurationException>(() => new TestOrchestrator(host));
        }
        finally
        {
            // Always clear so the variable doesn't bleed into subsequent tests.
            System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", null);
        }
    }

    [Fact]
    public async Task Constructor_WithInvalidConfig_AndStrictModeDisabled_DoesNotThrow()
    {
        // Arrange — explicitly clear the env var to guarantee resilient (default) behavior.
        System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", null);
        var host = ServiceHelper.BuildInvalidConfigHost();

        // Act — no exception expected
        var orchestrator = new TestOrchestrator(host);
        Assert.NotNull(orchestrator);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public void Constructor_WithInvalidConfig_AndStrictModeViaEnvVar_ExceptionContainsValidationErrors()
    {
        // Arrange
        System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", "true");
        try
        {
            var host = ServiceHelper.BuildInvalidConfigHost();

            // Act & Assert
            var ex = Assert.Throws<XpingConfigurationException>(() => new TestOrchestrator(host));
            Assert.Contains("Xping configuration invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", null);
        }
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaEnvVar_AndUploadFailure_ThrowsXpingNetworkException()
    {
        // Through the real registration: XPING_STRICTMODE reaches the options, and the orchestrator
        // judges the failed upload by them.
        System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", "true");
        try
        {
            var uploaderMock = new Mock<IXpingUploader>();
            var envDetectorMock = new Mock<IEnvironmentDetector>();
            envDetectorMock
                .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EnvironmentInfo());
            uploaderMock
                .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Network timeout" });

            using var store = new ScratchStore();
            var host = ServiceHelper.BuildRegisteredOrchestratorHost(
                ServiceHelper.CloudConfiguration(store.Path, strictMode: false), uploaderMock, envDetectorMock);
            var orchestrator = new TestOrchestrator(host);
            orchestrator.RecordExecution(
                new TestExecutionBuilder().WithTestName("EnvVarNetworkTest").WithOutcome(TestOutcome.Passed).Build());

            await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());

            await orchestrator.DisposeAsync();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", null);
        }
    }

    [Fact]
    public void AddXping_WithStrictModeViaEnvVar_SetsStrictModeOnOptions()
    {
        System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", "true");
        try
        {
            var services = new ServiceCollection();
            services.AddXping(new XpingConfiguration { ApiKey = "test-key", ProjectId = "test-project" });
            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.True(provider.GetRequiredService<IOptions<XpingConfiguration>>().Value.StrictMode);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("XPING_STRICTMODE", null);
        }
    }
}
