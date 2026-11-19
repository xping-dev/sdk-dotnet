/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Hosting;
using Xping.Cli.Report;
using Xping.Cli.Services;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests;

public sealed class ProgramTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void NoArgumentsPrintsUsageAndFails()
    {
        var (code, output, _) = Run();

        Assert.Equal(1, code);
        Assert.Contains("Report test reliability findings from recent local runs", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void HelpPrintsUsageAndSucceeds(string arg)
    {
        var (code, output, _) = Run(arg);

        Assert.Equal(0, code);
        Assert.Contains("Report test reliability findings from recent local runs", output, StringComparison.Ordinal);
        Assert.Contains("No account is required", output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownVerbFailsWithUsageOnStandardError()
    {
        var (code, _, error) = Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", error, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionPrintsCleanSemVer()
    {
        var (code, output, _) = Run("--version");

        string informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string expected = informational.Split('+')[0];

        Assert.Equal(0, code);

        // The build metadata suffix the built-in action would print is what this replaces.
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void VersionVerbIsNotACommand()
    {
        var (code, _, error) = Run("version");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", error, StringComparison.Ordinal);
    }

    // Scripts branch on it, and docs/cli/command-reference.md repeats it.
    [Fact]
    public void ACrashHasItsOwnExitCode()
    {
        Assert.Equal(70, ExitCodes.Crashed);
    }

    // Only the command's invocation used to be guarded, so a failure while the host or the parser
    // was built escaped Run and the runtime printed the raw exception.
    [Fact]
    public void AFailureWhileStartingUpIsACrash()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            ["where"],
            output,
            error,
            configureServices: services => services.AddSingleton<GlobalOptions>(
                _ => throw new InvalidOperationException("broken registration")));

        Assert.Equal(ExitCodes.Crashed, code);
        Assert.Equal("xping: broken registration" + Environment.NewLine, error.ToString());
    }

    // Work torn down by Ctrl+C need not fail with OperationCanceledException.
    [Fact]
    public void AnyFailureAfterCtrlCIsACancel()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            ["where"],
            output,
            error,
            configureServices: services => services.AddSingleton<ILocalSessionStoreFactory>(new ThrowingStoreFactory()),
            cancelKeys: new CancelKeyHandler(cancellation));

        Assert.Equal(AuthExitCodes.Cancelled, code);
        Assert.Equal("Cancelled." + Environment.NewLine, error.ToString());
    }

    private sealed class ThrowingStoreFactory : ILocalSessionStoreFactory
    {
        public ILocalSessionStore Create(LocalStoreOptions? options = null, string? startDirectory = null) =>
            throw new ObjectDisposedException("store");
    }
}
