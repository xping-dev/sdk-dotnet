/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class CliSurfaceTests : IDisposable
{
    private readonly string _root;

    public CliSurfaceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-surface-tests", Guid.NewGuid().ToString("N"));
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

    private static void Seed(string assembly, params bool[] outcomes)
    {
        ILocalRunStore store = LocalRunStore.Create();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < outcomes.Length; i++)
        {
            store.Write(new LocalRun(
                new LocalRunHeader
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    StartedAtUtc = baseTime.AddMinutes(i).AddSeconds(assembly.Length),
                    DurationMs = 3000,
                    Assembly = assembly
                },
                [
                    new LocalTestRecord
                    {
                        Fingerprint = $"fp-{assembly}",
                        Name = $"{assembly}.Test",
                        Outcome = outcomes[i] ? OutcomeCodes.Passed : OutcomeCodes.Failed,
                        DurationMs = 50
                    }
                ]));
        }
    }

    private static (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(args, output, error);
        return (code, output.ToString() + error.ToString());
    }

    // -----------------------------------------------------------------------
    // --all
    // -----------------------------------------------------------------------

    [Fact]
    public void AllReportsAcrossEveryAssembly()
    {
        // Arrange
        Seed("Alpha.Tests", true, false, true);
        Seed("Beta.Tests", false, true, false);

        // Act
        var (code, output) = Run("report", "--all", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("2 assemblies", output, StringComparison.Ordinal);
        Assert.Contains("Alpha.Tests", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AllOmitsSingleRunOutcomeCounts()
    {
        // Arrange
        Seed("Alpha.Tests", true, false, true);
        Seed("Beta.Tests", true, true, true);

        // Act
        var (code, output) = Run("report", "--all", "--ascii");

        // Assert — across assemblies there is no single latest run those counts could describe.
        Assert.Equal(0, code);
        Assert.DoesNotContain("passed     ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("local run summary", output, StringComparison.Ordinal);
        Assert.Contains("local summary", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AllSaysSoWhenNothingIsUnstable()
    {
        Seed("Alpha.Tests", true, true, true);
        Seed("Beta.Tests", true, true, true);

        var (code, output) = Run("report", "--all", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("Nothing unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AllAndAssemblyAreMutuallyExclusive()
    {
        var (code, output) = Run("report", "--all", "--assembly", "Alpha.Tests");

        Assert.Equal(2, code);
        Assert.Contains("mutually exclusive", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AllDoesNotPrintTheScopeNotice()
    {
        // The notice exists to flag an omission; --all omits nothing.
        Seed("Alpha.Tests", true, false, true);
        Seed("Beta.Tests", true, false, true);

        var (_, output) = Run("report", "--all", "--ascii");

        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // --json
    // -----------------------------------------------------------------------

    [Fact]
    public void JsonEmitsParsableOutputWithAStableSchemaVersion()
    {
        // Arrange
        Seed("Alpha.Tests", true, false, true);

        // Act
        var (code, output) = Run("report", "--json");

        // Assert
        Assert.Equal(0, code);

        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(3, root.GetProperty("runsAnalysed").GetInt32());
        Assert.NotEqual(0, root.GetProperty("unstableTests").GetArrayLength());
    }

    [Fact]
    public void JsonIncludesFindingDetail()
    {
        Seed("Alpha.Tests", true, false, true);

        var (_, output) = Run("report", "--json");

        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement finding = doc.RootElement.GetProperty("unstableTests")[0];

        Assert.Equal("fp-Alpha.Tests", finding.GetProperty("fingerprint").GetString());
        Assert.Equal("FlakyAcrossRuns", finding.GetProperty("kind").GetString());
        Assert.Equal(2, finding.GetProperty("passCount").GetInt32());
        Assert.Equal(3, finding.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public void JsonCarriesAssemblyLabelsInAggregateMode()
    {
        Seed("Alpha.Tests", true, false, true);
        Seed("Beta.Tests", false, true, false);

        var (_, output) = Run("report", "--all", "--json");

        using JsonDocument doc = JsonDocument.Parse(output);

        Assert.Equal(2, doc.RootElement.GetProperty("assembliesAnalysed").GetInt32());
        Assert.All(
            doc.RootElement.GetProperty("unstableTests").EnumerateArray(),
            f => Assert.False(string.IsNullOrEmpty(f.GetProperty("assembly").GetString())));
    }

    [Fact]
    public void JsonEmitsNoRenderedReport()
    {
        // A script consuming stdout must not have to strip a box-drawn block first.
        Seed("Alpha.Tests", true, false, true);

        var (_, output) = Run("report", "--json");

        Assert.DoesNotContain("local run summary", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // where
    // -----------------------------------------------------------------------

    [Fact]
    public void WherePrintsTheStorePathAndPerAssemblyCounts()
    {
        // Arrange
        Seed("Alpha.Tests", true, true);
        Seed("Beta.Tests", true);

        // Act
        var (code, output) = Run("where");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains(_root, output, StringComparison.Ordinal);
        Assert.Contains("3 runs", output, StringComparison.Ordinal);
        Assert.Contains("Alpha.Tests", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereReportsAnEmptyStoreWithoutFailing()
    {
        var (code, output) = Run("where");

        Assert.Equal(0, code);
        Assert.Contains("no runs recorded yet", output, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // clear
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearRefusesWithoutConfirmationWhenInputIsRedirected()
    {
        // Arrange — under `dotnet test` stdin is redirected, which is exactly the non-interactive
        // case the guard exists for.
        Seed("Alpha.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear");

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("Refusing to delete", output, StringComparison.Ordinal);
        Assert.Equal(3, LocalRunStore.Create().ReadRecent(100).Count);
    }

    [Fact]
    public void ClearWithForceDeletesEverything()
    {
        // Arrange
        Seed("Alpha.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear", "--force");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Deleted 3 runs", output, StringComparison.Ordinal);
        Assert.Empty(LocalRunStore.Create().ReadRecent(100));
    }

    [Fact]
    public void ClearScopedToAnAssemblyLeavesOthersAlone()
    {
        // Arrange
        Seed("Alpha.Tests", true, true);
        Seed("Beta.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear", "--assembly", "Alpha.Tests", "--force");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Deleted 2 runs", output, StringComparison.Ordinal);

        var remaining = LocalRunStore.Create().ReadRecent(100);
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, r => Assert.Equal("Beta.Tests", r.Header.Assembly));
    }

    [Fact]
    public void ClearOnAnEmptyStoreSucceedsQuietly()
    {
        var (code, output) = Run("clear", "--force");

        Assert.Equal(0, code);
        Assert.Contains("Nothing to clear", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearReportsWhenTheAssemblyHasNoRuns()
    {
        Seed("Alpha.Tests", true);

        var (code, output) = Run("clear", "--assembly", "Nonexistent.Tests", "--force");

        Assert.Equal(0, code);
        Assert.Contains("No runs recorded for assembly", output, StringComparison.Ordinal);
        Assert.Single(LocalRunStore.Create().ReadRecent(100));
    }
}
