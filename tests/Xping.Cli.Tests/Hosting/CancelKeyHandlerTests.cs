/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Commands.Auth;
using Xping.Cli.Hosting;

namespace Xping.Cli.Tests.Hosting;

public sealed class CancelKeyHandlerTests : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancelKeyHandler _handler;

    public CancelKeyHandlerTests()
    {
        _handler = new CancelKeyHandler(_cancellation);
    }

    public void Dispose() => _cancellation.Dispose();

    // report, where and clear never look at the token. Keeping the process alive for them would
    // leave Ctrl+C doing nothing, the clear prompt included.
    [Fact]
    public void CtrlCEndsTheProcessWhenNoCooperativeCommandRuns()
    {
        Assert.False(_handler.OnCancelKeyPress());
        Assert.False(_handler.Token.IsCancellationRequested);
    }

    [Fact]
    public void CtrlCCancelsACooperativeCommandAndKeepsTheProcessAlive()
    {
        using (_handler.EnterCooperative())
        {
            Assert.True(_handler.OnCancelKeyPress());
            Assert.True(_handler.Token.IsCancellationRequested);
        }
    }

    [Fact]
    public void ASecondCtrlCEndsTheProcessWhenTheCommandDidNotStop()
    {
        using (_handler.EnterCooperative())
        {
            Assert.True(_handler.OnCancelKeyPress());
            Assert.False(_handler.OnCancelKeyPress());
        }
    }

    [Fact]
    public void CtrlCEndsTheProcessAgainOnceTheCooperativeCommandFinished()
    {
        IDisposable cooperative = _handler.EnterCooperative();
        cooperative.Dispose();
        cooperative.Dispose();

        Assert.False(_handler.OnCancelKeyPress());
        Assert.False(_handler.Token.IsCancellationRequested);
    }

    [Fact]
    public void CtrlCDuringAnAuthCommandCancelsItWithExitCode130()
    {
        bool? keptAlive = null;
        using var output = new StringWriter();
        using var error = new StringWriter();

        // The command is resolved inside its cooperative section, so pressing Ctrl+C from the
        // factory is pressing it while the command runs.
        int code = Program.Run(
            ["login"],
            output,
            error,
            configureServices: services => services.AddTransient(sp =>
            {
                keptAlive = _handler.OnCancelKeyPress();
                return ActivatorUtilities.CreateInstance<LoginCommand>(sp);
            }),
            cancelKeys: _handler);

        Assert.True(keptAlive);
        Assert.Equal(AuthExitCodes.Cancelled, code);
        Assert.Equal("Cancelled." + Environment.NewLine, error.ToString());
    }
}
