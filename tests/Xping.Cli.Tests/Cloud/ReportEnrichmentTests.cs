/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Text.Json;
using Xping.Cli.Auth.Store;
using Xping.Cli.Cloud;
using Xping.Cli.Commands;
using Xping.Cli.Services;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// <c>xping report</c> run in-process against <see cref="FakeCloud"/>, end to end: credential,
/// pipeline, binding, enrichment, hints and exit code (cli-auth-cli-spec §11, §18.2;
/// cloud-data-in-cli-cli-spec §17.2).
/// </summary>
/// <remarks>
/// Sequential because the CTA banner is suppressed through a process-wide variable.
/// </remarks>
[Collection("Sequential")]
public sealed class ReportEnrichmentTests : IAsyncDisposable
{
    private const string Assembly = TestSessionFactory.DefaultAssembly;
    private const string Project = "myapp-tests";
    private const string FindingFingerprint = "fp-Removed0";
    private const string FailureFingerprint = "fp-Stable0";
    private const string ApiKey = "team-read-key-0123456789";
    private const string FlagKey = "flag-read-key-0123456789";
    private const string DigestPath = $"{FakeCloud.GatewayPrefix}v1/projects/{Project}/tests/digest";

    private readonly CliFlowHost _host = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-report-enrichment", Guid.NewGuid().ToString("N"));

    public ReportEnrichmentTests()
    {
        Directory.CreateDirectory(_root);
        _host.Environment[LocalSessionStoreFactory.StoreVariable] = _root;
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");

        _host.Cloud.AddProject(Project, displayName: Assembly);
        _host.Cloud.AddTest(Project, FindingFingerprint);
        _host.Cloud.AddTest(Project, FailureFingerprint, test => test["confidence"]!["score"] = 0.94);
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddApiKey(FlagKey);
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
        await _host.DisposeAsync().ConfigureAwait(false);

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Report_Digest_Ok()
    {
        Seed();
        _host.Cloud.AddSession(TestSessionFactory.SessionIdFor(7).ToString("D"));
        _host.SignIn();

        CliResult text = Report("--ascii");
        JsonElement json = Report("--json").Json();

        Assert.Equal(0, text.Code);
        Assert.EndsWith(" | cloud", ReportText.Lines(text.Output).First(l => l.Length > 0), StringComparison.Ordinal);
        string[] lines = ReportText.Lines(text.Output);
        Assert.Contains(lines, l => l.EndsWith("  cloud known-flaky", StringComparison.Ordinal));
        Assert.Contains("    cloud: confidence 0.62 v0.03 - moderately reliable - 35 d", lines);
        Assert.Contains("    local: flaky in 8 runs here", lines);
        Assert.Contains(lines, l => l.EndsWith("  local stopped running", StringComparison.Ordinal));

        JsonElement cloud = json.GetProperty("context").GetProperty("cloud");
        Assert.Equal("login", cloud.GetProperty("credential").GetString());
        Assert.Equal("ok", cloud.GetProperty("status").GetString());
        Assert.Equal(FakeCloud.WorkspaceId, cloud.GetProperty("workspaceId").GetString());
        Assert.Equal(Project, ProjectRow(json).GetProperty("projectKey").GetString());
        Assert.Equal("name-match", ProjectRow(json).GetProperty("source").GetString());

        JsonElement finding = Finding(json, FindingFingerprint);
        Assert.Equal(0.62, finding.GetProperty("cloud").GetProperty("confidence").GetProperty("score").GetDouble());
        Assert.Equal("moderately-reliable", finding.GetProperty("cloud").GetProperty("confidence").GetProperty("category").GetString());
        Assert.Equal("1.28", json.GetProperty("schemaVersion").GetString());
        JsonElement session = Assert.Single(cloud.GetProperty("sessions").EnumerateArray());
        Assert.Equal(("complete", true), (session.GetProperty("state").GetString(), session.GetProperty("scored").GetBoolean()));
        Assert.Empty(text.Error);
    }

    [Fact]
    public void Report_Digest_PartialBatch()
    {
        // More tests than one digest request takes, so the report asks twice, and one of the two is
        // refused: the answered half keeps its Cloud data, and the other half says it was not answered.
        Seed(alsoFlaky: CloudEnricher.MaxDigestBatchSize + 20);
        CliResult local = Report("--ascii");
        _host.SignIn();
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.BadRequest, "Injected");

        CliResult text = Report("--ascii", "--no-color", "--all");

        Assert.Equal(local.Code, text.Code);
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count(r => r.Path == DigestPath));
        string[] lines = ReportText.Lines(text.Output);
        string reason = Assert.Single(lines, l => l.StartsWith("cloud: partial, ", StringComparison.Ordinal));
        Assert.Matches(@"^cloud: partial, \d+ of \d+ tests answered \(server\)$", reason);

        // Either request may be the refused one; both hold tests Cloud does not know.
        Assert.Contains("    cloud: not answered", lines);
        Assert.Contains($"    cloud: not in project {Project}", lines);
    }

    [Fact]
    public async Task Report_Digest_Timeout_LocalIntact()
    {
        // The pin binds the project without listing projects, so the one request held is the digest.
        Seed(pin: Project);
        CliResult local = Report();
        _host.SignIn();
        var never = new TaskCompletionSource();
        _host.Cloud.HoldGatewayResponses = never.Task;

        Task<CliResult> report = _host.Start(ReportArgs());
        await WaitForRequestAsync(DigestPath);
        _host.Time.Advance(CloudBudget.Total);
        CliResult result = await _host.DriveAsync(report);

        Assert.Equal(local.Code, result.Code);
        Assert.Equal(local.Output, WithoutReasonLine(result.Output));
        Assert.Contains("cloud: unavailable (budget)", result.Output, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.GatewayRequests, r => r.Path == DigestPath);
    }

    [Fact]
    public async Task Report_Digest_RateLimited_NoWait()
    {
        Seed();
        int local = Report().Code;
        _host.SignIn();
        _host.Cloud.FailGateway(DigestPath, 50, HttpStatusCode.TooManyRequests, "Injected", retryAfter: 30);
        DateTimeOffset started = _host.Time.GetUtcNow();

        CliResult result = await _host.DriveAsync(_host.Start(ReportArgs("--json")));

        // Neither the Retry-After nor a retry: a 429 costs the Cloud data at once (contract §7.3).
        Assert.Equal(local, result.Code);
        Assert.Single(_host.Cloud.GatewayRequests, r => r.Path == DigestPath);
        Assert.True(_host.Time.GetUtcNow() - started < CloudResilience.MinimumForRetry, "The report waited on Cloud.");
        JsonElement cloud = result.Json().GetProperty("context").GetProperty("cloud");
        Assert.Equal(("unavailable", "rate-limited"), (cloud.GetProperty("status").GetString(), cloud.GetProperty("reason").GetString()));
    }

    [Fact]
    public void VerboseNamesTheCredentialAndNeverItsSecret()
    {
        Seed();
        CredentialRecord record = _host.SignIn();

        CliResult result = Report("--verbose");

        Assert.Contains(
            $"credential: stored login (", result.Error, StringComparison.Ordinal);
        Assert.Contains($"workspace {FakeCloud.WorkspaceId}, expires in 15 min", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(record.RefreshToken, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(record.AccessToken!, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseSaysWhenTheKeyTookOver()
    {
        Seed();
        CredentialRecord record = _host.SignIn();
        _host.Cloud.RevokeSession(record.RefreshToken);
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        _host.Environment["XPING_APIKEY"] = ApiKey;

        CliResult result = Report("--verbose");

        Assert.Contains(
            "credential: fell back to API key (XPING_APIKEY) because the stored login is no longer valid",
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePinRecordedWithTheSessionsBindsWithoutListingProjects()
    {
        Seed(pin: Project);
        _host.SignIn();

        JsonElement json = Report("--json").Json();

        Assert.Equal("session", ProjectRow(json).GetProperty("source").GetString());
        Assert.DoesNotContain(_host.Cloud.GatewayRequests, r => r.Path == FakeCloud.GatewayPrefix + "v1/projects");
    }

    [Fact]
    public void APinRemovedSinceIsNotRevivedFromAnOlderRun()
    {
        Seed(pin: "legacy-project");
        Seed(startOrdinal: 8);
        _host.SignIn();

        JsonElement json = Report("--json").Json();

        Assert.Equal("name-match", ProjectRow(json).GetProperty("source").GetString());
    }

    [Fact]
    public void AReportThatReadCloudDataDoesNotPitchTheCloud()
    {
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
        Seed();
        _host.SignIn();

        CliResult enriched = Report();
        Assert.Equal(0, _host.Run("logout").Code);
        CliResult local = Report();

        Assert.DoesNotContain("xping.io/start", enriched.Error, StringComparison.Ordinal);
        Assert.Contains("xping.io/start", local.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("xping.io/start", local.Output, StringComparison.Ordinal);

        // Only the stream changed: a blank line still sets the invitation off, and one still closes
        // the report (§11).
        Assert.Equal(string.Empty, ReportText.Lines(local.Error)[0]);
        Assert.Equal(string.Empty, ReportText.Lines(local.Output)[^2]);
    }

    [Fact]
    public void TheProjectFlagOverridesEverything()
    {
        Seed(pin: "something-else");
        _host.SignIn();

        JsonElement json = Report("--json", "--project", Project).Json();

        JsonElement project = ProjectRow(json);
        Assert.Equal((Project, "flag"), (project.GetProperty("projectKey").GetString(), project.GetProperty("source").GetString()));
    }

    [Fact]
    public void UnderTestOnlyTheSelectedTestIsAskedAbout()
    {
        // The page shows one test; asking about every finding behind it would spend the budget its
        // run history needs.
        Seed();
        _host.SignIn();

        JsonElement json = Report("--json", "--test", "Stable0").Json();

        string[] bodies = [.. _host.Cloud.GatewayRequests.Where(r => r.Path.EndsWith("/tests/digest", StringComparison.Ordinal)).Select(r => r.Body)];
        Assert.Contains(bodies, b => b.Contains(FailureFingerprint, StringComparison.Ordinal));
        Assert.DoesNotContain(bodies, b => b.Contains(FindingFingerprint, StringComparison.Ordinal));
        Assert.Equal("ok", json.GetProperty("test").GetProperty("cloud").GetProperty("digest").GetProperty("state").GetString());
    }

    [Fact]
    public void AnInvalidProjectFlagIsAParseError()
    {
        Seed();

        Assert.Equal(2, Report("--project", "a/b").Code);
    }

    [Fact]
    public void Report_NoCredential_NotAttemptedObject()
    {
        Seed();

        CliResult result = Report("--json");
        CliResult text = Report();

        // The object is there for an agent to read even when nothing was asked (contract §8.2 F5),
        // and the text is the local report with nothing Cloud in it (Amendment 3E).
        Assert.Equal(0, result.Code);
        JsonElement cloud = result.Json().GetProperty("context").GetProperty("cloud");
        Assert.Equal("not-attempted", cloud.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cloud.GetProperty("credential").ValueKind);
        Assert.Empty(cloud.GetProperty("projects").EnumerateArray());
        Assert.Empty(_host.Cloud.Requests);
        Assert.Empty(result.Error);
        Assert.DoesNotContain("cloud", text.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_ApiKey_Forbidden_Hint()
    {
        Seed();
        _host.Cloud.AddApiKey("no-api-plan-key-0123456789", ApiKeyScript.NoApiAccess);
        _host.IsTerminal = false;

        CliResult result = Report("--api-key", "no-api-plan-key-0123456789");

        // Said even on a pipe: unlike a Cloud that is down, a refused key does not fix itself (F2).
        Assert.Equal(0, result.Code);
        Assert.Contains("cloud: forbidden - API keys need the Team plan; run xping login instead", ReportText.Lines(result.Output));
        string hint = Assert.Single(ReportText.Lines(result.Error), l => l.Length > 0);
        Assert.StartsWith("Cloud data unavailable: ", hint, StringComparison.Ordinal);
        Assert.Contains("xping login", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_OtherAssembly_NoProject()
    {
        // One report covers one assembly (Amendment 4A): the bound one reads Cloud, and the other,
        // which no project is named after, says so once and keeps its local labels.
        Seed(assembly: "Other.Tests");
        Seed(startOrdinal: 8);
        _host.SignIn();

        JsonElement bound = Report("--json").Json().GetProperty("context").GetProperty("cloud");
        JsonElement other = Report("--json", "--assembly", "Other.Tests").Json().GetProperty("context").GetProperty("cloud");
        CliResult text = Report("--ascii", "--no-color", "--assembly", "Other.Tests");

        Assert.Equal("ok", bound.GetProperty("status").GetString());
        Assert.Equal("no-project", other.GetProperty("status").GetString());
        JsonElement row = Assert.Single(other.GetProperty("projects").EnumerateArray());
        Assert.Equal(("Other.Tests", "no-project"), (row.GetProperty("assembly").GetString(), row.GetProperty("status").GetString()));

        string[] lines = ReportText.Lines(text.Output);
        int reason = Array.IndexOf(lines, "cloud: no project in this workspace for Other.Tests - pass --project");
        Assert.Equal("       <key>", lines[reason + 1]);
        Assert.DoesNotContain(lines, l => l.StartsWith("    cloud: ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("  local flaky", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportTest_CloudBlock_And_Runs()
    {
        Seed();
        _host.Cloud.AddRun(Project, FailureFingerprint);
        _host.Cloud.AddRun(Project, FailureFingerprint, run => (run["outcome"], run["isCi"]) = ("passed", false));
        _host.Cloud.RunsHaveMore = true;
        _host.SignIn();

        CliResult text = Report("--ascii", "--no-color", "--test", "Stable0");
        JsonElement history = Report("--json", "--test", "Stable0").Json().GetProperty("test").GetProperty("cloud");

        string[] lines = ReportText.Lines(text.Output);
        Assert.Contains(lines, l => l.StartsWith("CLOUD ", StringComparison.Ordinal) && l.Contains(Project, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("    confidence  0.94 ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("RUNS IN CLOUD ", StringComparison.Ordinal) && l.EndsWith("newest 2 runs in Cloud (retention 365 days)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  09-28 12:00  x failed   canonical  CI  ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  09-28 12:00  + passed   canonical  -   ", StringComparison.Ordinal));
        Assert.Contains("  more runs in Cloud", lines);

        Assert.Equal(("ok", true), (history.GetProperty("runsState").GetString(), history.GetProperty("hasMore").GetBoolean()));
        Assert.Equal(2, history.GetProperty("runs").GetArrayLength());
        Assert.All(
            _host.Cloud.GatewayRequests.Where(r => r.Path.EndsWith("/runs", StringComparison.Ordinal)),
            r => Assert.Equal($"{FakeCloud.GatewayPrefix}v1/projects/{Project}/tests/{FailureFingerprint}/runs", r.Path));
    }

    [Fact]
    public async Task ReportTest_Runs_SkippedOnBudget()
    {
        // The project list and the digest are held until the clock has used more than three of the
        // four seconds, each under its own three-second attempt timeout: less than the second the
        // run history needs is left, so it is not asked for (contract §7.3).
        Seed();
        _host.Cloud.AddRun(Project, FailureFingerprint);
        _host.SignIn();
        var projects = new TaskCompletionSource();
        var digest = new TaskCompletionSource();
        _host.Cloud.HoldGatewayResponses = projects.Task;

        Task<CliResult> report = _host.Start(ReportArgs("--json", "--test", "Stable0"));
        await WaitForRequestAsync($"{FakeCloud.GatewayPrefix}v1/projects");
        _host.Time.Advance(TimeSpan.FromSeconds(2.5));
        _host.Cloud.HoldGatewayResponses = digest.Task;
        projects.SetResult();
        await WaitForRequestAsync(DigestPath);
        _host.Time.Advance(TimeSpan.FromSeconds(0.6));
        digest.SetResult();
        CliResult result = await _host.DriveAsync(report);

        JsonElement history = result.Json().GetProperty("test").GetProperty("cloud");
        Assert.Equal("budget", history.GetProperty("runsState").GetString());
        Assert.Equal("ok", history.GetProperty("digest").GetProperty("state").GetString());
        Assert.DoesNotContain(_host.Cloud.GatewayRequests, r => r.Path.EndsWith("/runs", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_Banner_OnStderr()
    {
        // Two assemblies, so the report names the one it chose; no credential, so it invites.
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
        Seed(assembly: "Older.Tests");
        Seed(startOrdinal: 8);

        CliResult result = Report();

        // Stdout is the report alone, so a redirect captures exactly that (contract §8.2 F3).
        Assert.DoesNotContain("Reporting on ", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("xping.io/start", result.Output, StringComparison.Ordinal);
        Assert.Contains($"Reporting on {Assembly} ", result.Error, StringComparison.Ordinal);
        Assert.Contains("xping.io/start", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Report_CloudDown()
    {
        Seed();
        CliResult local = Report();
        _host.SignIn();
        FailEveryTestRead(HttpStatusCode.ServiceUnavailable);

        CliResult down = await _host.DriveAsync(_host.Start(ReportArgs()));

        // The local report is intact; the one line added says why Cloud is missing from it (§7.6).
        Assert.Equal(local.Code, down.Code);
        Assert.Equal(local.Output, WithoutReasonLine(down.Output));
        Assert.Contains("cloud: unavailable (server)", down.Output, StringComparison.Ordinal);
        string hint = Assert.Single(ReportText.Lines(down.Error), l => l.Length > 0);
        Assert.StartsWith("Cloud data unavailable: could not reach ", hint, StringComparison.Ordinal);
        Assert.EndsWith(". Showing local results only.", hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACloudHintStaysOffAPipe()
    {
        Seed();
        _host.SignIn();
        _host.IsTerminal = false;
        FailEveryTestRead(HttpStatusCode.ServiceUnavailable);

        CliResult down = await _host.DriveAsync(_host.Start(ReportArgs("--json")));

        Assert.Empty(down.Error);
        Assert.Equal("unavailable", down.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
    }

    [Fact]
    public void Report_ApiKey_Precedence()
    {
        Seed();
        _host.SignIn();
        _host.Environment["XPING_APIKEY"] = ApiKey;

        JsonElement withFlag = Report("--json", "--api-key", FlagKey).Json();
        Assert.Equal("api-key", Credential(withFlag));
        Assert.All(_host.Cloud.GatewayRequests, r => Assert.Equal(FlagKey, r.Headers["X-API-Key"]));

        int seen = _host.Cloud.GatewayRequests.Count;
        JsonElement withLogin = Report("--json").Json();
        Assert.Equal("login", Credential(withLogin));
        Assert.All(_host.Cloud.GatewayRequests.Skip(seen), r =>
        {
            Assert.StartsWith("Bearer ", r.Headers["Authorization"], StringComparison.Ordinal);
            Assert.False(r.Headers.ContainsKey("X-API-Key"));
        });

        Assert.Equal(0, _host.Run("logout").Code);
        seen = _host.Cloud.GatewayRequests.Count;
        JsonElement keyOnly = Report("--json").Json();
        Assert.Equal("api-key", Credential(keyOnly));
        Assert.All(_host.Cloud.GatewayRequests.Skip(seen), r => Assert.Equal(ApiKey, r.Headers["X-API-Key"]));
    }

    [Fact]
    public void Report_Fallback_LoginInvalid_ThenKey()
    {
        Seed();
        CredentialRecord record = _host.SignIn();
        _host.Cloud.RevokeSession(record.RefreshToken);
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        _host.Environment["XPING_APIKEY"] = ApiKey;
        _host.IsTerminal = false;

        CliResult result = Report("--json");

        Assert.Equal(0, result.Code);
        Assert.Equal(
            "Cloud data unavailable: your sign-in is no longer valid. Run xping login.",
            Assert.Single(ReportText.Lines(result.Error), l => l.Length > 0));
        JsonElement cloud = result.Json().GetProperty("context").GetProperty("cloud");
        Assert.Equal(("api-key", "ok"), (cloud.GetProperty("credential").GetString(), cloud.GetProperty("status").GetString()));
        Assert.Null(_host.StoredRecord());
        Assert.NotEqual(JsonValueKind.Null, Finding(result.Json(), FindingFingerprint).GetProperty("cloud").ValueKind);
    }

    [Fact]
    public void Report_ReuseDetected()
    {
        Seed();
        CliResult local = Report("--json");
        CredentialRecord record = _host.SignIn();
        _host.Cloud.RevokeSession(record.RefreshToken);
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        _host.IsTerminal = false;

        CliResult result = Report("--json");

        Assert.Equal(local.Code, result.Code);
        Assert.Equal(
            "Cloud data unavailable: your sign-in is no longer valid. Run xping login.",
            Assert.Single(ReportText.Lines(result.Error), l => l.Length > 0));
        Assert.Equal("login-required", result.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
        Assert.All(result.Json().GetProperty("findings").EnumerateArray(), f => Assert.Equal(JsonValueKind.Null, f.GetProperty("cloud").ValueKind));
        Assert.Null(_host.StoredRecord());
    }

    [Fact]
    public void Report_Refresh_Rotation()
    {
        Seed();
        CredentialRecord before = _host.SignIn();
        _host.Time.Advance(TimeSpan.FromMinutes(16));

        CliResult result = Report("--json");

        Assert.Equal("ok", result.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
        Assert.NotEqual(before.RefreshToken, _host.StoredRecord()!.RefreshToken);
        Assert.Single(_host.Cloud.RequestsTo("/connect/token"), r => r.Form["grant_type"] == "refresh_token");
    }

    [Fact]
    public void Report_Fallback_UploadOnlyKey()
    {
        Seed();
        CliResult local = Report();
        _host.Cloud.AddApiKey("upload-only-key-0123456789", ApiKeyScript.UploadOnly);
        _host.Environment["XPING_APIKEY"] = "upload-only-key-0123456789";

        CliResult result = Report();

        Assert.Equal(local.Code, result.Code);
        Assert.Equal(local.Output, WithoutReasonLine(result.Output));
        string hint = Assert.Single(ReportText.Lines(result.Error), l => l.Length > 0);
        Assert.StartsWith("Cloud data unavailable: ", hint, StringComparison.Ordinal);
        Assert.Contains("xping login", hint, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task Report_NoFallback_OnNetworkError()
    {
        Seed();
        _host.SignIn();
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        _host.Environment["XPING_APIKEY"] = ApiKey;
        _host.Cloud.Drop("/connect/token", count: 20);

        CliResult result = await _host.DriveAsync(_host.Start(ReportArgs("--json")));

        Assert.Equal(0, result.Code);
        Assert.StartsWith("Cloud data unavailable: could not reach ", result.Error, StringComparison.Ordinal);
        Assert.Equal("login", Credential(result.Json()));
        Assert.DoesNotContain(_host.Cloud.Requests, r => r.Headers.ContainsKey("X-API-Key"));
        Assert.NotNull(_host.StoredRecord());
    }

    [Fact]
    public void AnAssemblyNoProjectMatchesGetsTheProjectHint()
    {
        Seed(assembly: "Unknown.Tests");
        _host.SignIn();

        CliResult result = Report("--json");

        Assert.Equal(
            "Cloud data unavailable: no matching Cloud project for Unknown.Tests. Use --project <key>.",
            Assert.Single(ReportText.Lines(result.Error), l => l.Length > 0));
        Assert.Equal("no-project", result.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.UpgradeRequired)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ReportNeverReturnsACloudExitCode(HttpStatusCode status)
    {
        Seed();
        int local = Report().Code;
        _host.SignIn();
        FailEveryTestRead(status);

        CliResult result = await _host.DriveAsync(_host.Start(ReportArgs()));

        Assert.Equal(local, result.Code);
        Assert.InRange(result.Code, 0, 3);
    }

    /// <summary>
    /// Writes eight runs in which <c>Removed0</c> vanishes after five and whose newest run fails
    /// <c>Stable0</c>: two findings, each about one test.
    /// </summary>
    /// <param name="assembly">The assembly every run is of.</param>
    /// <param name="pin">The Cloud project the runs were recorded for, or <see langword="null"/>.</param>
    /// <param name="startOrdinal">The first run's ordinal, after another seed's.</param>
    /// <param name="alsoFlaky">How many more tests run in every run and fail in one of them.</param>
    private void Seed(string assembly = Assembly, string? pin = null, int startOrdinal = 0, int alsoFlaky = 0)
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);
        Dictionary<string, string>? properties = pin is null ? null : new() { [LocalSessionProperties.ProjectId] = pin };

        for (int i = 0; i < 8; i++)
        {
            bool newest = i == 7;
            List<TestExecution> executions =
            [
                TestSessionFactory.Execution(
                    "Stable0",
                    newest ? TestOutcome.Failed : TestOutcome.Passed,
                    assembly: assembly,
                    exceptionType: newest ? "System.NullReferenceException" : null,
                    errorMessage: newest ? "boom" : null),
                TestSessionFactory.Execution("Stable1", assembly: assembly)
            ];

            if (i < 5)
                executions.Add(TestSessionFactory.Execution("Removed0", assembly: assembly));

            // A few of them fail in each run, too few for the run to look environmental, and each its
            // own way, so they are findings of their own rather than one shared failure.
            executions.AddRange(Enumerable.Range(0, alsoFlaky).Select(n => n % 8 == i
                ? TestSessionFactory.Execution($"Flaky{n}", TestOutcome.Failed, assembly: assembly, exceptionType: $"Flaky{n}Exception", errorMessage: $"flaky {n}")
                : TestSessionFactory.Execution($"Flaky{n}", assembly: assembly)));

            store.Write(TestSessionFactory.Session(startOrdinal + i, executions, customProperties: properties));
        }
    }

    private void FailEveryTestRead(HttpStatusCode status)
    {
        _host.Cloud.FailGateway(DigestPath, 50, status, "Injected");
    }

    /// <summary>
    /// Waits until the command under test has sent a request to <paramref name="path"/>.
    /// </summary>
    private async Task WaitForRequestAsync(string path)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        while (!_host.Cloud.GatewayRequests.Any(r => r.Path == path))
            await Task.Delay(10, patience.Token).ConfigureAwait(false);
    }

    private string[] ReportArgs(params string[] args) => ["report", "--directory", _root, .. args];

    private CliResult Report(params string[] args) => _host.Run(ReportArgs(args));

    private static JsonElement ProjectRow(JsonElement report) =>
        report.GetProperty("context").GetProperty("cloud").GetProperty("projects")[0];

    private static string? Credential(JsonElement report) =>
        report.GetProperty("context").GetProperty("cloud").GetProperty("credential").GetString();

    // What a Cloud that did not answer adds to the text: the reason line (§7.6), and "local" in front
    // of each kind, which says whose word it is (Amendment 3E). Everything else is the local report.
    private static string WithoutReasonLine(string output) =>
        string.Join('\n', output.Split('\n')
            .Where(line => !line.Contains("cloud: ", StringComparison.Ordinal))
            .Select(line => line.Replace("  local ", "  ", StringComparison.Ordinal)));

    private static JsonElement Finding(JsonElement report, string fingerprint) =>
        report.GetProperty("findings").EnumerateArray()
            .First(f => f.GetProperty("subject").GetProperty("fingerprint").GetString() == fingerprint);
}
