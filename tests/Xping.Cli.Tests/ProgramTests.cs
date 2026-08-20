/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;

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
}
