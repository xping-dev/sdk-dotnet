/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// The session summary every adapter prints when a session ends, logged by the orchestrator itself.
/// </summary>
public sealed class XpingContextOrchestratorSessionLogTests
{
    private sealed class Harness(IHost host) : XpingContextOrchestrator(host)
    {
        protected override string TestFramework => "Harness";

        public void Record() =>
            RecordTestExecution(new TestExecutionBuilder().WithTestName("Test").WithOutcome(TestOutcome.Passed).Build());

        public Task<UploadResult> FinalizeAsync() => FinalizeSessionAsync();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, string Message)> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(string category, List<(string Category, string Message)> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                messages.Add((category, formatter(state, exception)));
        }
    }

    [Fact]
    public async Task FinalizingASession_LogsItsSummaryOnce()
    {
        using var provider = new CapturingLoggerProvider();
        var uploader = new Mock<IXpingUploader>();
        var detector = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploader, detector);
        using IHost host = ServiceHelper.BuildOrchestratorHost(
            uploader, detector, configureServices: services => services.AddSingleton<ILoggerProvider>(provider));
        var orchestrator = new Harness(host);
        orchestrator.Record();

        await orchestrator.FinalizeAsync();
        await orchestrator.FinalizeAsync();
        await orchestrator.DisposeAsync();

        var summary = Assert.Single(
            provider.Messages, m => m.Message.StartsWith("Total tests recorded: 1", StringComparison.Ordinal));
        Assert.DoesNotContain(provider.Messages, m => m.Message == "Session finalized.");
        Assert.Contains("exec:", summary.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(Harness).FullName!.Replace('+', '.'), summary.Category);
    }
}
