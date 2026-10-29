/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

/// <summary>
/// The messages around the report echo recorded names and paths safely, in the charset the report
/// would use (D7).
/// </summary>
/// <remarks>
/// Driven through <c>Program.Run</c> to a pipe, so the charset is ASCII and a command on standard
/// error is the one place a recorded <c>é</c> may still appear.
/// </remarks>
// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class RecordedTextMessagesTests : IDisposable
{
    // An escape sequence, a newline, an é and a right-to-left override, mid-name.
    private const string Hostile = "Pay\u001b[31m\nCafé‮";

    private readonly string _root;

    public RecordedTextMessagesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-recorded-text-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ANameThatMatchesNothingIsEchoedPrintable()
    {
        Seed("Stable");

        var (code, _, error) = Run(isTerminal: false, "report", "--test", Hostile);

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Contains("'Pay?[31m?Caf??'", error, StringComparison.Ordinal);
        AssertPrintableAscii(error);
    }

    [Fact]
    public void AnAmbiguousNameListsItsCandidatesPrintableAndKeepsTheCommandRunnable()
    {
        Seed([.. Enumerable.Range(0, 2).Select(c => TestSessionFactory.Execution(
            $"{Hostile}{c}",
            fingerprint: $"fp-case-{c}",
            fullyQualifiedName: $"MyApp.Tests.SampleTests.{Hostile}({c})"))]);

        var (code, _, error) = Run(isTerminal: false, "report", "--test", Hostile);
        string[] lines = error.TrimEnd().Split(Environment.NewLine);

        Assert.Equal(ExitCodes.TestAmbiguous, code);
        Assert.Contains("'Pay?[31m?Caf??' matches 2 tests", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("SampleTests.Pay?[31m?Caf??(0)", StringComparison.Ordinal));

        // The command is printed to be run: its é stays, its controls do not.
        Assert.StartsWith("xping report --test ", lines[^1], StringComparison.Ordinal);
        AssertPrintableAscii(string.Join('\n', lines[..^1]));
        Assert.DoesNotContain(lines[^1], c => c is '\u001b' or '‮' or '\n');
    }

    [Fact]
    public void TheScopeNoticeNamesTheAssemblyPrintable()
    {
        Seed("Stable", assembly: "Other.Tests");
        Seed("Stable", assembly: $"{Hostile}.Tests", startOrdinal: 10);

        var (code, output, _) = Run(isTerminal: true, "report", "--ascii", "--no-color");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Reporting on Pay?[31m?Caf??.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain(output, c => c is '\u001b' or '‮' or 'é');
    }

    [Fact]
    public void AnUnknownAssemblyIsEchoedPrintable()
    {
        Seed("Stable");

        var (code, _, error) = Run(isTerminal: false, "report", "--assembly", Hostile);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains("'Pay?[31m?Caf??'", error, StringComparison.Ordinal);
        AssertPrintableAscii(error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARejectedProjectKeyIsEchoedPrintableWhereverAsciiIsGiven(bool asciiFirst)
    {
        string[] project = ["--project", "Café\u001b[31m‮/"];
        string[] args = asciiFirst ? ["report", "--ascii", .. project] : ["report", .. project, "--ascii"];

        var (code, _, error) = Run(isTerminal: true, args);

        Assert.Equal(2, code);
        Assert.Contains("got 'Caf??[31m?/'", error, StringComparison.Ordinal);
        AssertPrintableAscii(error);
    }

    [Fact]
    public void WhereListsAHostileAssemblyPrintable()
    {
        Seed("Stable", assembly: $"{Hostile}.Tests");

        var (code, output, _) = Run(isTerminal: false, "where");

        Assert.Equal(0, code);
        Assert.Contains("Pay?[31m?Caf??.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain(output, c => c is '\u001b' or '‮' or 'é');
    }

    /// <summary>Nothing but printable ASCII and the line breaks between messages.</summary>
    private static void AssertPrintableAscii(string text) =>
        Assert.All(text, c => Assert.True(
            c is '\n' or '\r' or (>= ' ' and < '\u007f'), $"U+{(int)c:X4} in '{text}'"));

    private static void Seed(string test, string assembly = TestSessionFactory.DefaultAssembly, int startOrdinal = 0) =>
        Seed([TestSessionFactory.Execution(test, assembly: assembly)], startOrdinal);

    private static void Seed(TestExecution[] executions, int startOrdinal = 0)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 6; i++)
            store.Write(TestSessionFactory.Session(startOrdinal + i, executions));
    }

    private static (int Code, string Output, string Error) Run(bool isTerminal, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(args, output, error, input: null, isTerminal, configureServices: OfflineCli.Configure);
        return (code, output.ToString(), error.ToString());
    }
}
