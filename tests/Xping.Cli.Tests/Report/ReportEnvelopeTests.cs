/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Cli.Tests.Report;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class ReportEnvelopeTests : IDisposable
{
    private readonly string _root;

    public ReportEnvelopeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-envelope-tests", Guid.NewGuid().ToString("N"));
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

    // The resolver that owns this layout is internal to the SDK and not visible here, so the path is
    // rebuilt. Kept in one place so a layout change surfaces as one failing helper, not ten tests.
    private string SessionsDirectory => Path.Combine(_root, "sessions");

    /// <summary>
    /// Writes sessions in which several tests stop running, so the report has findings to rank.
    /// </summary>
    private static void SeedVanishing(int vanishingTests = 1, int total = 8, int presentIn = 5)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < total; i++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            if (i < presentIn)
            {
                for (int t = 0; t < vanishingTests; t++)
                    executions.Add(TestSessionFactory.Execution($"Removed{t}"));
            }

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }

    private static void SeedWithRevision(int count, string sha, string branch)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < count; i++)
        {
            store.Write(TestSessionFactory.Session(
                i, [TestSessionFactory.Execution("Alpha")], sha: sha, branch: branch));
        }
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(["report", .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static JsonElement RunJson(params string[] args)
    {
        var (_, output, _) = Run([.. args, "--format", "json"]);

        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    [Fact]
    public void TheEnvelopeCarriesEveryDocumentedSection()
    {
        SeedVanishing();

        JsonElement root = RunJson();

        Assert.Equal("1.10", root.GetProperty("schemaVersion").GetString());

        JsonElement window = root.GetProperty("window");
        foreach (string key in
            (string[])["from", "to", "sessionCount", "resolution", "currentSliceSize", "sessionIds"])
        {
            Assert.True(window.TryGetProperty(key, out _), $"window.{key} missing");
        }

        JsonElement summary = root.GetProperty("summary");
        foreach (string key in (string[])
        [
            "tests", "findings", "healthy", "excludedLowEvidence", "excludedNotSignificant",
            "environmentalSessions", "incompleteSessions", "unreadableSessions", "failedProviders"
        ])
        {
            Assert.True(summary.TryGetProperty(key, out _), $"summary.{key} missing");
        }

        JsonElement truncated = root.GetProperty("truncated");
        Assert.True(truncated.TryGetProperty("shown", out _));
        Assert.True(truncated.TryGetProperty("total", out _));
        Assert.Equal(
            "xping report --all", truncated.GetProperty("command").GetString());

        Assert.True(root.TryGetProperty("context", out _));
        Assert.True(root.TryGetProperty("findings", out _));
    }

    [Fact]
    public void AFindingCarriesItsSubjectAndSourceLocation()
    {
        SeedVanishing();

        JsonElement finding = RunJson().GetProperty("findings")[0];

        Assert.StartsWith("f_", finding.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("Vanished", finding.GetProperty("kind").GetString());
        Assert.Equal("low", finding.GetProperty("severity").GetString());
        Assert.Equal("low", finding.GetProperty("evidenceLevel").GetString());

        JsonElement subject = finding.GetProperty("subject");
        Assert.Equal("test", subject.GetProperty("type").GetString());
        Assert.Equal("fp-Removed0", subject.GetProperty("fingerprint").GetString());
        Assert.Equal(
            "MyApp.Tests.SampleTests.Removed0",
            subject.GetProperty("fullyQualifiedName").GetString());

        // Never stripped for brevity: this is what lets an agent open the file.
        Assert.Equal("SampleTests.cs", subject.GetProperty("sourceFile").GetString());
        Assert.Equal(10, subject.GetProperty("sourceLineNumber").GetInt32());

        Assert.NotEqual(
            JsonValueKind.Null, finding.GetProperty("evidence").ValueKind);
        Assert.False(string.IsNullOrEmpty(finding.GetProperty("drillDown").GetString()));
    }

    [Fact]
    public void ContextIsPopulatedFromTheRecordedCommit()
    {
        SeedWithRevision(6, "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a", "main");

        JsonElement context = RunJson().GetProperty("context");

        Assert.Equal(
            "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a", context.GetProperty("sha").GetString());
        Assert.Equal("main", context.GetProperty("branch").GetString());

        // There is deliberately no `dirty`: the SDK records staged changes, which is a different
        // question, and a field that is wrong for unstaged edits is worse than one that is absent.
        Assert.False(context.TryGetProperty("dirty", out _));
    }

    [Fact]
    public void TruncationIsAccurateAndTheFullListIsReachable()
    {
        SeedVanishing(vanishingTests: 5);

        JsonElement limited = RunJson("--top", "2");

        Assert.Equal(2, limited.GetProperty("findings").GetArrayLength());
        Assert.Equal(2, limited.GetProperty("truncated").GetProperty("shown").GetInt32());
        Assert.Equal(5, limited.GetProperty("truncated").GetProperty("total").GetInt32());

        // The summary counts everything produced, not merely what is shown.
        Assert.Equal(5, limited.GetProperty("summary").GetProperty("findings").GetInt32());

        JsonElement all = RunJson("--all");
        Assert.Equal(5, all.GetProperty("findings").GetArrayLength());
        Assert.Equal(5, all.GetProperty("truncated").GetProperty("shown").GetInt32());
    }

    [Fact]
    public void TruncationDoesNotChangeTheExitCode()
    {
        // Hiding the sixth finding must not also hide its effect on the threshold, or --top would
        // quietly decide whether the build fails.
        SeedVanishing(vanishingTests: 5);

        var (limited, _, _) = Run("--top", "1", "--fail-on", "low", "--format", "json");
        var (full, _, _) = Run("--all", "--fail-on", "low", "--format", "json");

        Assert.Equal(full, limited);
        Assert.Equal(1, limited);
    }

    [Fact]
    public void HealthyCountsTestsNoFindingNamed()
    {
        SeedVanishing(vanishingTests: 2);

        JsonElement summary = RunJson().GetProperty("summary");

        // Three distinct tests in the window; two vanished.
        Assert.Equal(3, summary.GetProperty("tests").GetInt32());
        Assert.Equal(2, summary.GetProperty("findings").GetInt32());
        Assert.Equal(1, summary.GetProperty("healthy").GetInt32());
    }

    [Fact]
    public void UnreadableSessionsAreSkippedCountedAndWarnedAbout()
    {
        SeedVanishing();

        File.WriteAllText(
            Path.Combine(SessionsDirectory, "session-0000000000000000001-aaaaaaaa.json.gz"),
            "not gzip at all");

        string truncated =
            Path.Combine(SessionsDirectory, "session-0000000000000000002-bbbbbbbb.json.gz");
        using (var file = new FileStream(truncated, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write("{\"sessionId\":\"" + Guid.NewGuid().ToString("D") + "\",\"executions\":[{");
        }

        var (code, output, _) = Run("--format", "json");

        // The report still renders, and says how much history it could not see.
        Assert.Equal(0, code);

        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal(
            2, document.RootElement.GetProperty("summary").GetProperty("unreadableSessions").GetInt32());
        Assert.Equal(
            8, document.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void WarningsGoToStandardErrorSoJsonStaysParsable()
    {
        SeedVanishing();

        File.WriteAllText(
            Path.Combine(SessionsDirectory, "session-0000000000000000001-aaaaaaaa.json.gz"),
            "not gzip at all");

        var (_, output, _) = Run("--format", "json");

        // Would throw if a warning had been interleaved into stdout.
        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal("1.10", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void TwoRunsOverAnUnchangedStoreProduceByteIdenticalJson()
    {
        SeedVanishing(vanishingTests: 4);

        var (_, first, _) = Run("--format", "json");
        var (_, second, _) = Run("--format", "json");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheTextReportIsAlsoStableAcrossRuns()
    {
        SeedVanishing(vanishingTests: 4);

        var (_, first, _) = Run("--ascii");
        var (_, second, _) = Run("--ascii");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheKindFilterNarrowsTheFindings()
    {
        SeedVanishing(vanishingTests: 3);

        Assert.Equal(3, RunJson("--kind", "Vanished").GetProperty("findings").GetArrayLength());

        // A kind no provider implements yet is valid input and correctly yields nothing.
        Assert.Equal(0, RunJson("--kind", "Flaky").GetProperty("findings").GetArrayLength());
    }
}
