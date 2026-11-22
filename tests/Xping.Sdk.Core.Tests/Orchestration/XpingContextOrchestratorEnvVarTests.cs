/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        protected override string TestFramework => "Harness";

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

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }

    // Sets each variable for the test and puts the process environment back afterwards, including
    // an XPING_APIKEY the developer's shell may hold.
    private static Restorer WithEnv(params (string Name, string? Value)[] variables)
    {
        var originals = variables.Select(v => (v.Name, System.Environment.GetEnvironmentVariable(v.Name))).ToList();
        foreach ((string name, string? value) in variables)
            System.Environment.SetEnvironmentVariable(name, value);
        return new Restorer(originals);
    }

    private sealed class Restorer(List<(string Name, string? Value)> originals) : IDisposable
    {
        public void Dispose()
        {
            foreach ((string name, string? value) in originals)
                System.Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Fact]
    public async Task AConfigurationErrorIsLoggedWithEachErrorAsOneSentence()
    {
        // The docs quote this line word for word. The template used to add ". " after messages that
        // already end with a period, printing "mode.. SDK disabled".
        using var _ = WithEnv(("XPING_APIKEY", null), ("XPING_MODE", "Cloud"), ("XPING_BATCHSIZE", "2000"));
        using var logs = new CapturingLoggerProvider();
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);
        using var store = new ScratchStore();

        using IHost host = ServiceHelper.BuildRegisteredOrchestratorHost(
            new XpingConfiguration { LocalStorePath = store.Path }, uploaderMock, envDetectorMock,
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        var orchestrator = new TestOrchestrator(host);
        await orchestrator.DisposeAsync();

        Assert.Contains(
            "Configuration validation failed: ApiKey is required in Cloud mode. BatchSize cannot exceed 1000. " +
            "SDK disabled - tests will run without observability tracking.",
            logs.Messages);
    }

    [Theory]
    [InlineData(true, "Mode=Disabled")]
    [InlineData(false, "Enabled=false")]
    public async Task TheDisabledMessageNamesTheSettingThatTurnedTheSdkOff(bool enabled, string setting)
    {
        using var _ = WithEnv(("XPING_APIKEY", null), ("XPING_MODE", null), ("XPING_ENABLED", null));
        using var logs = new CapturingLoggerProvider();
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);
        using var store = new ScratchStore();

        using IHost host = ServiceHelper.BuildRegisteredOrchestratorHost(
            new XpingConfiguration
            {
                Enabled = enabled,
                Mode = enabled ? XpingMode.Disabled : XpingMode.Auto,
                LocalStorePath = store.Path,
            },
            uploaderMock, envDetectorMock,
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        var orchestrator = new TestOrchestrator(host);
        await orchestrator.DisposeAsync();

        Assert.Contains(
            $"SDK is disabled via configuration ({setting}). Tests will run without observability tracking.",
            logs.Messages);
    }

    [Fact]
    public void AStrictModeConfigurationErrorJoinsTheErrorsAsSentences()
    {
        using var _ = WithEnv(("XPING_APIKEY", null), ("XPING_STRICTMODE", "true"), ("XPING_BATCHSIZE", "2000"));
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);
        using var store = new ScratchStore();

        using IHost host = ServiceHelper.BuildRegisteredOrchestratorHost(
            new XpingConfiguration { LocalStorePath = store.Path }, uploaderMock, envDetectorMock);

        var ex = Assert.Throws<XpingConfigurationException>(() => new TestOrchestrator(host));
        Assert.Equal(
            "Xping configuration invalid: ApiKey is required when StrictMode is enabled. BatchSize cannot exceed 1000.",
            ex.Message);
    }
}
