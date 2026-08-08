/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands;
using Xping.Sdk.Core.Models.Local;
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

    private static void Seed(string assembly, params bool[] outcomesPerRun)
    {
        ILocalRunStore store = LocalRunStore.Create();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < outcomesPerRun.Length; i++)
        {
            var records = new List<LocalTestRecord>
            {
                new()
                {
                    Fingerprint = "fingerprint-target",
                    Name = "MyTests.TargetTest",
                    Outcome = outcomesPerRun[i] ? OutcomeCodes.Passed : OutcomeCodes.Failed,
                    DurationMs = 120,
                    Attempt = 1
                }
            };

            store.Write(new LocalRun(
                new LocalRunHeader
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    StartedAtUtc = baseTime.AddMinutes(i),
                    DurationMs = 4000,
                    Assembly = assembly
                },
                records));
        }
    }

    private static (int Code, string Output) RunReport(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(["report", .. args], output, error);
        return (code, output.ToString() + error.ToString());
    }

    [Fact]
    public void ReportsFlakinessFromStoredRuns()
    {
        // Arrange
        Seed("MyTests", true, false, true, false);

        // Act
        var (code, output) = RunReport("--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("local run summary", output, StringComparison.Ordinal);
        Assert.Contains("MyTests.TargetTest", output, StringComparison.Ordinal);
        Assert.Contains("unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitsNonZeroWhenNoRunsAreStored()
    {
        var (code, output) = RunReport();

        Assert.NotEqual(0, code);
        Assert.Contains("No runs recorded yet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopesTheReportToTheRequestedAssembly()
    {
        // Arrange — two suites sharing one store.
        Seed("Alpha.Tests", true, false, true);
        Seed("Beta.Tests", true, true, true);

        // Act
        var (code, output) = RunReport("--assembly", "Beta.Tests", "--ascii");

        // Assert — Beta is stable, so nothing should be flagged.
        Assert.Equal(0, code);
        Assert.DoesNotContain("unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void LimitsTheWindowWithLast()
    {
        // Arrange — the oldest runs fail, the newest three pass.
        Seed("MyTests", false, false, true, true, true);

        // Act
        var (code, output) = RunReport("--last", "3", "--ascii");

        // Assert — restricted to the passing tail, nothing is unstable.
        Assert.Equal(0, code);
        Assert.DoesNotContain("unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintsPerTestHistoryWithDetails()
    {
        // Arrange
        Seed("MyTests", true, false, true, false);

        // Act
        var (code, output) = RunReport("--details", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Details", output, StringComparison.Ordinal);
        Assert.Contains("fingerprint-target", output, StringComparison.Ordinal);
        Assert.Contains("FAIL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopesToTheNewestAssemblyWhenNoneIsRequested()
    {
        // Arrange — Alpha is stable and older; Beta is flaky and newest. Reporting across both would
        // give headline counts from one run and history from several unrelated suites.
        Seed("Alpha.Tests", true, true, true);
        Seed("Beta.Tests", true, false, true, false);

        // Act
        var (code, output) = RunReport("--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Reporting on Beta.Tests", output, StringComparison.Ordinal);
        Assert.Contains("1 other assembly", output, StringComparison.Ordinal);
        Assert.Contains("unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotAnnounceScopeWhenTheStoreHoldsOneAssembly()
    {
        // Arrange
        Seed("Only.Tests", true, false, true);

        // Act
        var (code, output) = RunReport("--ascii");

        // Assert — the notice is there to make an omission visible; with nothing omitted it is noise.
        Assert.Equal(0, code);
        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotAnnounceScopeWhenTheAssemblyWasRequested()
    {
        // Arrange
        Seed("Alpha.Tests", true, true, true);
        Seed("Beta.Tests", true, false, true);

        // Act
        var (code, output) = RunReport("--assembly", "Alpha.Tests", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAClearMessageForAnUnknownAssembly()
    {
        // Arrange
        Seed("Alpha.Tests", true, true, true);

        // Act
        var (code, output) = RunReport("--assembly", "Nonexistent.Tests");

        // Assert
        Assert.NotEqual(0, code);
        Assert.Contains("Nonexistent.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsUnknownOptions()
    {
        var (code, output) = RunReport("--nonsense");

        Assert.Equal(2, code);
        Assert.Contains("Unknown option", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNonNumericLast()
    {
        var (code, output) = RunReport("--last", "banana");

        Assert.Equal(2, code);
        Assert.Contains("positive number", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingOptionValue()
    {
        var (code, output) = RunReport("--assembly");

        Assert.Equal(2, code);
        Assert.Contains("expects a value", output, StringComparison.Ordinal);
    }
}
