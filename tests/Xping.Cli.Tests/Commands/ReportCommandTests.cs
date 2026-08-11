/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class ReportCommandTests : IDisposable
{
    private readonly string _root;

    public ReportCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-cli-tests", Guid.NewGuid().ToString("N"));
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

    /// <summary>
    /// Writes one session per entry, containing the named tests.
    /// </summary>
    private static void Seed(
        string assembly, int startOrdinal, params string[][] testsPerSession)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < testsPerSession.Length; i++)
        {
            List<TestExecution> executions =
                [.. testsPerSession[i].Select(n => TestSessionFactory.Execution(n, assembly: assembly))];

            store.Write(TestSessionFactory.Session(startOrdinal + i, executions));
        }
    }

    /// <summary>
    /// Writes <paramref name="count"/> sessions running the same tests every time.
    /// </summary>
    private static void SeedStable(string assembly, int count, int startOrdinal = 0) =>
        Seed(assembly, startOrdinal, [.. Enumerable.Repeat<string[]>(["Alpha", "Beta"], count)]);

    private static (int Code, string Output) RunReport(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(["report", .. args], output, error);
        return (code, output.ToString() + error.ToString());
    }

    private static JsonElement RunJson(params string[] args)
    {
        var (_, output) = RunReport([.. args, "--format", "json"]);

        // Cloned so the document can be disposed while the element stays usable.
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    [Fact]
    public void ExitsWithInsufficientDataWhenNoRunsAreStored()
    {
        var (code, output) = RunReport();

        Assert.Equal(2, code);
        Assert.Contains("No runs recorded yet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsTheWindowItAnalysed()
    {
        SeedStable("MyTests", 6);

        JsonElement root = RunJson();
        JsonElement window = root.GetProperty("window");

        Assert.Equal(6, window.GetProperty("sessionCount").GetInt32());
        Assert.Equal(6, window.GetProperty("sessionIds").GetArrayLength());
        Assert.Equal(1, window.GetProperty("currentSliceSize").GetInt32());
    }

    [Fact]
    public void ASmallWindowStillProducesAReport()
    {
        // Below the reporting floor there are no findings, but the counts, the window and the
        // context are all still true and useful. Refusing to report would be discouraging and wrong.
        SeedStable("MyTests", 2);

        var (code, output) = RunReport("--format", "json");

        Assert.Equal(0, code);
        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal(2, doc.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Empty(doc.RootElement.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public void ScopesTheReportToTheRequestedAssembly()
    {
        SeedStable("Alpha.Tests", 5);
        SeedStable("Beta.Tests", 5, startOrdinal: 10);

        JsonElement root = RunJson("--assembly", "Alpha.Tests");

        Assert.Equal(5, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal("Alpha.Tests", root.GetProperty("context").GetProperty("assembly").GetString());
    }

    [Fact]
    public void LimitsTheWindowWithRuns()
    {
        SeedStable("MyTests", 8);

        JsonElement root = RunJson("--runs", "3");

        Assert.Equal(3, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal("runs", root.GetProperty("window").GetProperty("resolution").GetString());
    }

    [Fact]
    public void ReportsAClearMessageForAnUnknownAssembly()
    {
        SeedStable("Alpha.Tests", 5);

        var (code, output) = RunReport("--assembly", "Nonexistent.Tests");

        Assert.Equal(2, code);
        Assert.Contains("Nonexistent.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextIsOmittedRatherThanInventedWhenNothingIsKnown()
    {
        // Sessions recorded outside a git checkout, or in CI, carry no commit. A fabricated context
        // would be quietly wrong; a null one is honest.
        SeedStable("MyTests", 5);

        JsonElement root = RunJson();
        JsonElement context = root.GetProperty("context");

        Assert.Equal(JsonValueKind.Null, context.GetProperty("sha").ValueKind);
        Assert.Equal(JsonValueKind.Null, context.GetProperty("branch").ValueKind);
        Assert.Equal("MyTests", context.GetProperty("assembly").GetString());
    }

    [Fact]
    public void RejectsUnknownOptions()
    {
        var (code, output) = RunReport("--nonsense");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNonNumericRuns()
    {
        var (code, output) = RunReport("--runs", "banana");

        Assert.Equal(2, code);
        Assert.Contains("positive number", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingOptionValue()
    {
        var (code, output) = RunReport("--assembly");

        Assert.Equal(2, code);
        Assert.Contains("Required argument missing", output, StringComparison.Ordinal);
    }
}
