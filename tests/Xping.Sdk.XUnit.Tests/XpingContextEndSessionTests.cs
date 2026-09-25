/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Moq;
using Xping.Sdk.Core.Exceptions;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Tests for <see cref="XpingContext.EndSessionAsync"/>: every way ending the session can go, without the
/// static context.
/// </summary>
public sealed class XpingContextEndSessionTests : IDisposable
{
    private readonly List<Exception> _failFastCalls = [];
    private readonly Mock<ILogger> _logger = new();
    private readonly StringWriter _shutdownErrors = new();
    private int _shutdownCalls;

    public void Dispose() => _shutdownErrors.Dispose();

    [Fact]
    public async Task FinalizeSucceeds_ShutsDownWithoutReportingAnything()
    {
        await EndSessionAsync(finalize: () => Task.CompletedTask);

        Assert.Equal(1, _shutdownCalls);
        Assert.Empty(_failFastCalls);
        _logger.VerifyNoOtherCalls();
        Assert.Empty(_shutdownErrors.ToString());
    }

    [Fact]
    public async Task FinalizeThrowsNetworkError_FailsFastThenShutsDown()
    {
        var error = new XpingNetworkException("upload refused");

        await EndSessionAsync(finalize: () => Task.FromException(error));

        Assert.Same(error, Assert.Single(_failFastCalls));
        Assert.Equal(1, _shutdownCalls);
        _logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FinalizeThrowsOtherError_LogsItThenShutsDown()
    {
        var error = new InvalidOperationException("finalize broke");

        await EndSessionAsync(finalize: () => Task.FromException(error));

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                error,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        Assert.Empty(_failFastCalls);
        Assert.Equal(1, _shutdownCalls);
    }

    [Fact]
    public async Task FinalizeThrowsOtherError_WithoutLogger_StillShutsDown()
    {
        await XpingContext.EndSessionAsync(
            () => Task.FromException(new InvalidOperationException("finalize broke")),
            Shutdown,
            FailFast,
            logger: null,
            _shutdownErrors);

        Assert.Equal(1, _shutdownCalls);
        Assert.Empty(_failFastCalls);
    }

    [Fact]
    public async Task ShutdownThrows_WritesItToShutdownErrorsWithoutThrowing()
    {
        var exception = await Record.ExceptionAsync(() => XpingContext.EndSessionAsync(
            () => Task.CompletedTask,
            () => Task.FromException(new ObjectDisposedException("host")),
            FailFast,
            _logger.Object,
            _shutdownErrors));

        Assert.Null(exception);
        Assert.Contains("Error shutting down Xping", _shutdownErrors.ToString(), StringComparison.Ordinal);
        Assert.Contains(nameof(ObjectDisposedException), _shutdownErrors.ToString(), StringComparison.Ordinal);
    }

    private Task EndSessionAsync(Func<Task> finalize) =>
        XpingContext.EndSessionAsync(finalize, Shutdown, FailFast, _logger.Object, _shutdownErrors);

    private Task Shutdown()
    {
        _shutdownCalls++;
        return Task.CompletedTask;
    }

    private void FailFast(string message, Exception exception) => _failFastCalls.Add(exception);
}
