/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Moq;
using Xping.Sdk.Core.Exceptions;

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// Tests for the error handling of <c>XpingContextOrchestrator.EndSessionAsync</c>: every way ending the
/// session can go, without a live context.
/// </summary>
public sealed class XpingContextOrchestratorEndSessionTests : IDisposable
{
    private readonly Mock<ILogger> _logger = new();
    private readonly StringWriter _shutdownErrors = new();
    private int _shutdownCalls;

    public void Dispose() => _shutdownErrors.Dispose();

    [Fact]
    public async Task FinalizeSucceeds_ShutsDownWithoutReportingAnything()
    {
        var runFailure = await EndSessionAsync(finalize: () => Task.CompletedTask);

        Assert.Null(runFailure);
        Assert.Equal(1, _shutdownCalls);
        _logger.VerifyNoOtherCalls();
        Assert.Empty(_shutdownErrors.ToString());
    }

    [Fact]
    public async Task FinalizeThrowsNetworkError_ShutsDownThenReturnsIt()
    {
        var error = new XpingNetworkException("upload refused");

        var runFailure = await EndSessionAsync(finalize: () => Task.FromException(error));

        Assert.Same(error, runFailure);
        Assert.Equal(1, _shutdownCalls);
        _logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FinalizeThrowsOtherError_LogsItThenShutsDown()
    {
        var error = new InvalidOperationException("finalize broke");

        var runFailure = await EndSessionAsync(finalize: () => Task.FromException(error));

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                error,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        Assert.Null(runFailure);
        Assert.Equal(1, _shutdownCalls);
    }

    [Fact]
    public async Task FinalizeThrowsOtherError_WithoutLogger_StillShutsDown()
    {
        var runFailure = await XpingContextOrchestrator.EndSessionAsync(
            () => Task.FromException(new InvalidOperationException("finalize broke")),
            Shutdown,
            logger: null,
            _shutdownErrors);

        Assert.Equal(1, _shutdownCalls);
        Assert.Null(runFailure);
    }

    [Fact]
    public async Task ShutdownThrows_WritesItToShutdownErrorsWithoutThrowing()
    {
        var exception = await Record.ExceptionAsync(() => XpingContextOrchestrator.EndSessionAsync(
            () => Task.CompletedTask,
            () => Task.FromException(new ObjectDisposedException("host")),
            _logger.Object,
            _shutdownErrors));

        Assert.Null(exception);
        Assert.Contains("Error shutting down Xping", _shutdownErrors.ToString(), StringComparison.Ordinal);
        Assert.Contains(nameof(ObjectDisposedException), _shutdownErrors.ToString(), StringComparison.Ordinal);
    }

    private Task<XpingNetworkException?> EndSessionAsync(Func<Task> finalize) =>
        XpingContextOrchestrator.EndSessionAsync(finalize, Shutdown, _logger.Object, _shutdownErrors);

    private Task Shutdown()
    {
        _shutdownCalls++;
        return Task.CompletedTask;
    }
}
