/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Text.Json;
using Xping.Cli.Auth.Store;
using Xping.Cli.Commands;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// <c>xping report</c> run in-process against <see cref="FakeCloud"/>, end to end: credential,
/// pipeline, binding, enrichment, hints and exit code (cli-auth-cli-spec §11, §18.2).
/// </summary>
/// <remarks>
/// Sequential because the local store is found through the process-wide <c>XPING_LOCAL_STORE</c>,
/// as the other report tests find theirs.
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

    private readonly CliFlowHost _host = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-report-enrichment", Guid.NewGuid().ToString("N"));

    public ReportEnrichmentTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");

        _host.Cloud.AddProject(Project, displayName: Assembly);
        _host.Cloud.AddTest(Project, FindingFingerprint);
        _host.Cloud.AddTest(Project, FailureFingerprint, test => test["confidenceScore"] = 0.94);
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddApiKey(FlagKey);
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
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
    public void Report_Enriched()
    {
        Seed();
        _host.SignIn();

        CliResult text = Report("--ascii");
        JsonElement json = Report("--json").Json();

        Assert.Equal(0, text.Code);
        Assert.EndsWith(" | cloud", ReportText.Lines(text.Output).First(l => l.Length > 0), StringComparison.Ordinal);
        Assert.Contains("evidence low | confidence 0.62 | ", text.Output, StringComparison.Ordinal);
        Assert.Contains("evidence moderate | confidence 0.94 | ", text.Output, StringComparison.Ordinal);

        JsonElement cloud = json.GetProperty("context").GetProperty("cloud");
        Assert.Equal("stored-login", cloud.GetProperty("credential").GetString());
        Assert.Equal("ok", cloud.GetProperty("status").GetString());
        Assert.Equal(FakeCloud.WorkspaceId, cloud.GetProperty("workspaceId").GetString());
        Assert.Equal(Project, cloud.GetProperty("project").GetProperty("projectKey").GetString());
        Assert.Equal("name-match", cloud.GetProperty("project").GetProperty("source").GetString());

        JsonElement finding = Finding(json, FindingFingerprint);
        Assert.Equal(0.62, finding.GetProperty("cloud").GetProperty("confidence").GetDouble());
        Assert.Equal("moderately-reliable", finding.GetProperty("cloud").GetProperty("category").GetString());
        Assert.Equal("1.22", json.GetProperty("schemaVersion").GetString());
        Assert.Empty(text.Error);
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

        Assert.Equal("session", json.GetProperty("context").GetProperty("cloud").GetProperty("project").GetProperty("source").GetString());
        Assert.DoesNotContain(_host.Cloud.GatewayRequests, r => r.Path == FakeCloud.GatewayPrefix + "v1/projects");
    }

    [Fact]
    public void APinRemovedSinceIsNotRevivedFromAnOlderRun()
    {
        Seed(pin: "legacy-project");
        Seed(startOrdinal: 8);
        _host.SignIn();

        JsonElement json = Report("--json").Json();

        Assert.Equal("name-match", json.GetProperty("context").GetProperty("cloud").GetProperty("project").GetProperty("source").GetString());
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

        Assert.DoesNotContain("xping.io/start", enriched.Output, StringComparison.Ordinal);
        Assert.Contains("xping.io/start", local.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProjectFlagOverridesEverything()
    {
        Seed(pin: "something-else");
        _host.SignIn();

        JsonElement json = Report("--json", "--project", Project).Json();

        JsonElement project = json.GetProperty("context").GetProperty("cloud").GetProperty("project");
        Assert.Equal((Project, "flag"), (project.GetProperty("projectKey").GetString(), project.GetProperty("source").GetString()));
    }

    [Fact]
    public void AnInvalidProjectFlagIsAParseError()
    {
        Seed();

        Assert.Equal(2, Report("--project", "a/b").Code);
    }

    [Fact]
    public void Report_NoCredential_MakesNoRequest()
    {
        Seed();

        CliResult result = Report("--json");

        Assert.Equal(0, result.Code);
        Assert.Equal(JsonValueKind.Null, result.Json().GetProperty("context").GetProperty("cloud").ValueKind);
        Assert.Empty(_host.Cloud.Requests);
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task Report_CloudDown()
    {
        Seed();
        CliResult local = Report();
        _host.SignIn();
        FailEveryTestRead(HttpStatusCode.ServiceUnavailable);

        CliResult down = await _host.DriveAsync(_host.Start(ReportArgs()));

        Assert.Equal(local.Code, down.Code);
        Assert.Equal(local.Output, down.Output);
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
        Assert.Equal("stored-login", Credential(withLogin));
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
        Assert.Equal(local.Output, result.Output);
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
        Assert.Equal("stored-login", Credential(result.Json()));
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
        Assert.Equal("unavailable", result.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
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
    private static void Seed(string assembly = Assembly, string? pin = null, int startOrdinal = 0)
    {
        ILocalSessionStore store = LocalSessionStore.Create();
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

            store.Write(TestSessionFactory.Session(startOrdinal + i, executions, customProperties: properties));
        }
    }

    private void FailEveryTestRead(HttpStatusCode status)
    {
        foreach (string fingerprint in (string[])[FindingFingerprint, FailureFingerprint])
            _host.Cloud.FailGateway($"{FakeCloud.GatewayPrefix}v1/projects/{Project}/tests/{fingerprint}", 50, status, "Injected");
    }

    private string[] ReportArgs(params string[] args) => ["report", "--directory", _root, .. args];

    private CliResult Report(params string[] args) => _host.Run(ReportArgs(args));

    private static string? Credential(JsonElement report) =>
        report.GetProperty("context").GetProperty("cloud").GetProperty("credential").GetString();

    private static JsonElement Finding(JsonElement report, string fingerprint) =>
        report.GetProperty("findings").EnumerateArray()
            .First(f => f.GetProperty("subject").GetProperty("fingerprint").GetString() == fingerprint);
}
