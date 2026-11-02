/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Xping.Cli.Auth.Store;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Services;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The same store gives the same exit code whatever Cloud answered, and whether or not a credential
/// exists (contract §7.6, §8.3; cloud-data-in-cli-cli-spec §9, §17.1).
/// </summary>
/// <remarks>
/// Sequential because the CTA banner is suppressed through a process-wide variable.
/// </remarks>
[Collection("Sequential")]
public sealed class ExitCodeIndependenceTests : IAsyncDisposable
{
    private const string Project = "myapp-tests";
    private const string ApiKey = "team-read-key-0123456789";
    private const string UploadOnlyKey = "upload-only-key-0123456789";

    private readonly CliFlowHost _host = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-exit-independence", Guid.NewGuid().ToString("N"));

    public ExitCodeIndependenceTests()
    {
        Directory.CreateDirectory(_root);
        _host.Environment[LocalSessionStoreFactory.StoreVariable] = _root;
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");

        _host.Cloud.AddProject(Project, displayName: TestSessionFactory.DefaultAssembly);
        _host.Cloud.AddTest(Project, "fp-Removed0");
        _host.Cloud.AddTest(Project, "fp-Stable0");
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddApiKey(UploadOnlyKey, ApiKeyScript.UploadOnly);
        Seed();
    }

    public static TheoryData<string> Statuses() =>
        ["not-attempted", "ok", "signature-algorithm", "unavailable", "rate-limited", "login-required", "forbidden", "no-project"];

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

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task TheExitCodeIsTheLocalOneUnderEveryCloudStatus(string scenario)
    {
        int expected = Run("report", "--directory", _root, "--fail-on", "low").Code;

        string[] args = Arrange(scenario);
        CliResult result = await _host.DriveAsync(_host.Start(["report", "--directory", _root, "--fail-on", "low", "--json", .. args]));

        // Meaningful only when the local layer fails the command: a report that exits 0 everywhere
        // would pass for any rule.
        Assert.Equal(ExitCodes.FindingsAtThreshold, expected);
        Assert.Equal(expected, result.Code);
        Assert.Equal(
            scenario switch
            {
                "signature-algorithm" => "ok",
                "rate-limited" => "unavailable",
                _ => scenario
            },
            result.Json().GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
    }

    private string[] Arrange(string scenario)
    {
        switch (scenario)
        {
            case "not-attempted":
                return [];
            case "ok":
                _host.SignIn();
                return [];
            case "signature-algorithm":
                _host.SignIn();
                _host.Cloud.SignatureAlgorithm = "emh-2";
                return [];
            case "unavailable":
                _host.SignIn();
                FailDigest(HttpStatusCode.ServiceUnavailable);
                return [];
            case "rate-limited":
                _host.SignIn();
                FailDigest(HttpStatusCode.TooManyRequests);
                return [];
            case "login-required":
                CredentialRecord record = _host.SignIn();
                _host.Cloud.RevokeSession(record.RefreshToken);
                _host.Time.Advance(TimeSpan.FromMinutes(16));
                return [];
            case "forbidden":
                return ["--api-key", UploadOnlyKey];
            case "no-project":
                _host.SignIn();
                return ["--project", "no-such-project"];
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private void FailDigest(HttpStatusCode status) =>
        _host.Cloud.FailGateway($"{FakeCloud.GatewayPrefix}v1/projects/{Project}/tests/digest", 50, status, "Injected");

    private CliResult Run(params string[] args) => _host.Run(args);

    /// <summary>
    /// Eight runs in which <c>Removed0</c> vanishes after five and the newest fails <c>Stable0</c>.
    /// </summary>
    private void Seed()
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);

        for (int i = 0; i < 8; i++)
        {
            bool newest = i == 7;
            List<TestExecution> executions =
            [
                TestSessionFactory.Execution(
                    "Stable0",
                    newest ? TestOutcome.Failed : TestOutcome.Passed,
                    exceptionType: newest ? "System.NullReferenceException" : null,
                    errorMessage: newest ? "boom" : null),
                TestSessionFactory.Execution("Stable1")
            ];

            if (i < 5)
                executions.Add(TestSessionFactory.Execution("Removed0"));

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }
}
