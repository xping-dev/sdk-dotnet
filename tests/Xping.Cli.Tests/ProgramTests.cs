/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
        Assert.Contains("xping report", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void HelpPrintsUsageAndSucceeds(string arg)
    {
        var (code, output, _) = Run(arg);

        Assert.Equal(0, code);
        Assert.Contains("xping report", output, StringComparison.Ordinal);
        Assert.Contains("No account is required", output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownVerbFailsWithUsageOnStandardError()
    {
        var (code, _, error) = Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("Unknown command", error, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionPrintsAVersion()
    {
        var (code, output, _) = Run("version");

        Assert.Equal(0, code);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }
}
