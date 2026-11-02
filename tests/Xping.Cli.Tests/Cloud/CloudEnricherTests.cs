/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Auth.Store;
using Xping.Cli.Cloud;
using Xping.Cli.Cloud.Digest;
using Xping.Cli.Hosting;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Tests.Auth.Store;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The enrichment's own rules — budget, concurrency, fallback, binding, status precedence,
/// stitching — with a scripted DataGateway client (cloud-data-in-cli-cli-spec §3, §4, §17.1). The
/// real pipeline against <see cref="FakeCloud"/> is <see cref="ReportEnrichmentTests"/>.
/// </summary>
public sealed class CloudEnricherTests : IDisposable
{
    private const string Project = "myapp-tests";
    private const string ApiKey = "team-read-key-0123456789";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "xping-enricher-tests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal) { ["XPING_PROJECTID"] = Project };
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeCredentialStore _keychain = new(CredentialStoreKind.Keychain, "Test Keychain");
    private readonly ScriptedFactory _factory = new();

    public CloudEnricherTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task NoCredentialAsksNothingAndSaysSo()
    {
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        EnrichmentResult result = await EnrichAsync(envelope);

        // Contract R5: the object is always there, so an agent never has to guess.
        Assert.Equal(
            new CloudContextDto("not-attempted", "none", 0, CredentialTestData.CloudUrl, null, null, null, null, null, 0, 0, [], []),
            result.Envelope.Context!.Cloud! with { Projects = [], Sessions = [] });
        Assert.Equal(envelope.Findings, result.Envelope.Findings);
        Assert.Empty(result.Hints);
        Assert.Empty(_factory.Created);
    }

    [Fact]
    public async Task EveryTestWithAFingerprintIsReadOnceAndTheContextSaysHow()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        EnrichmentResult result = await EnrichAsync(envelope);

        List<string> expected = Fingerprints(envelope);
        Assert.Equal(expected.Order(), _factory.Client.Reads.Order());
        Assert.All(result.Envelope.Findings.Where(f => f.Subject.Fingerprint != null), f => Assert.NotNull(f.Cloud));
        Assert.All(result.Envelope.LatestRun!.Failures, f => Assert.NotNull(f.Cloud));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("api-key", "ok", "none"), (cloud.Credential, cloud.Status, cloud.Reason));
        Assert.Equal((expected.Count, expected.Count, 365, "emh-1"), (cloud.RequestedTests, cloud.EnrichedTests, cloud.RetentionDays, cloud.SignatureAlgorithm));
        Assert.Equal(_time.GetUtcNow(), cloud.FetchedAt);
        Assert.Equal(
            [.. expected.Select(f => Subjects(envelope).First(s => s.Fingerprint == f).Assembly!).Distinct().Select(a => new CloudProjectDto(a, Project, "env", "ok", "none"))],
            cloud.Projects);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public async Task TheDigestIsStitchedWithEveryBlockAndStampedWithTheReadTime()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        CloudDigestDto cloud = result.Envelope.Findings.First(f => f.Cloud != null).Cloud!;
        Assert.Equivalent(
            new CloudDigestDto(
                "ok",
                _time.GetUtcNow(),
                DisagreesWithLocal: false,
                Identity: null,
                "known-flaky",
                null,
                null,
                812,
                new CloudConfidenceDto(
                    "scored", 0.62, null, null, "moderately-reliable", "stable", -0.03, 48, "robust", null, null, null,
                    new CloudWindowDto("days", 35, ScriptedClient.Computed, null), null, ScriptedClient.Computed, "current"),
                new CloudFlakyDto("flaky", null, null, null, null, null, null, "current"),
                new CloudCanonicalDto(10, 2, 0, null, "passed", ScriptedClient.Computed, new CloudWindowDto("runs", 10, null, ScriptedClient.Computed)),
                new CloudTransitionDto("flakiness-introduced", "reliable", "unreliable", ScriptedClient.Computed),
                [
                    new CloudSignatureDto(ScriptedClient.Hash, "System.TimeoutException", true, 3, 1, 2, 1, 2, ScriptedClient.Computed, ScriptedClient.Computed, ["c0ffee"], null),
                    new CloudSignatureDto(new string('b', 64), null, true, 0, 0, 0, 0, 0, null, null, [], null)
                ]),
            cloud,
            strict: true);
    }

    [Fact]
    public async Task ATestCloudDoesNotKnowIsStitchedAsUnknownAndTheStatusStaysOk()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Missing = true;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Equal("ok", result.Envelope.Context!.Cloud!.Status);
        CloudDigestDto unknown = result.Envelope.Findings.First(f => f.Subject.Fingerprint != null).Cloud!;
        Assert.Equal(("unknown", null, null), (unknown.State, unknown.Verdict, unknown.Confidence));
        Assert.All(result.Envelope.LatestRun!.Failures, f => Assert.Equal("unknown", f.Cloud!.Match));
        Assert.Empty(result.Hints);
    }

    [Fact]
    public async Task AnItemInAStateTheCliCannotReadCostsThatTestAsUnknownNotItsBatch()
    {
        // Contract §8.2 F4.
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.BadState.Add("fp-1");

        EnrichmentResult result = await EnrichAsync(Many(3));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("partial", "invalid-response", 3), (cloud.Status, cloud.Reason, cloud.EnrichedTests));
        Assert.Equal("unknown", result.Envelope.Findings[1].Cloud!.State);
        Assert.Equal("ok", result.Envelope.Findings[2].Cloud!.State);
    }

    [Fact]
    public async Task AGroupFindingIsNotRead()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope cluster = ReportFixtures.Get("cluster");

        EnrichmentResult result = await EnrichAsync(cluster);

        Assert.Empty(_factory.Created);
        Assert.Null(result.Envelope.Findings[0].Cloud);
        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("not-attempted", "api-key"), (cloud.Status, cloud.Credential));
    }

    [Fact]
    public async Task NoMoreThanFourDigestRequestsAreInFlightAtOnce()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Delay = TimeSpan.FromMilliseconds(20);

        await EnrichAsync(Many(TargetPlanner.MaxEnrichedTests));

        Assert.Equal(TargetPlanner.MaxEnrichedTests, _factory.Client.Reads.Count);
        Assert.InRange(_factory.Client.MaxInFlight, 1, CloudEnricher.MaxConcurrency);
    }

    [Fact]
    public async Task TestsAreAskedAboutInBatchesOfAHundredInRankOrderUpToTheCap()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(Many(250));

        Assert.Equal([100, 100], _factory.Client.Batches.Select(b => b.Count));
        Assert.Contains(_factory.Client.Batches, b => b[0] == "fp-0" && b[^1] == "fp-99");
        Assert.DoesNotContain(_factory.Client.Reads, f => f == "fp-200");
        Assert.Equal(200, result.Envelope.Context!.Cloud!.RequestedTests);
    }

    [Fact]
    public async Task TheSessionKeysRideOnTheFirstBatchOfEachProjectOnly()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = Many(150);
        TestSession session = Session(envelope.Findings[0].Subject.Assembly!);

        EnrichmentResult result = await EnrichAsync(envelope, sessions: [session]);

        Assert.Equal(
            [[session.SessionId.ToString()], []],
            _factory.Client.Requests.OrderByDescending(r => r.Tests.Count).Select(r => r.SessionKeys));
        Assert.Equal(
            new CloudSessionDto(session.SessionId.ToString(), Project, "complete", true, true),
            Assert.Single(result.Envelope.Context!.Cloud!.Sessions));
    }

    [Fact]
    public async Task ABatchStillPendingWhenTheBudgetRunsOutIsLeftOutAndTheStatusIsPartial()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        // The second batch hangs; the first answers.
        _factory.Client.Hang.Add("fp-120");

        Task<EnrichmentResult> enrichment = EnrichAsync(Many(150));
        await _factory.Client.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(30));
        while (_factory.Client.Reads.Count < 100)
            await Task.Delay(10);
        _time.Advance(CloudBudget.Total);
        EnrichmentResult result = await enrichment.WaitAsync(TimeSpan.FromSeconds(30));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("partial", "budget", 150, 100), (cloud.Status, cloud.Reason, cloud.RequestedTests, cloud.EnrichedTests));
        Assert.Equal(("partial", "budget"), (cloud.Projects[0].Status, cloud.Projects[0].Reason));
        Assert.All(result.Envelope.Findings.Take(100), f => Assert.NotNull(f.Cloud));
        Assert.All(result.Envelope.Findings.Skip(100), f => Assert.Null(f.Cloud));
        CloudHint hint = Assert.Single(result.Hints);
        Assert.Equal(("Cloud data partial: 100 of 150 tests answered (budget).", false), (hint.Text, hint.Always));
    }

    [Fact]
    public async Task AKeychainThatBlocksPastTheCredentialSliceCostsTheCloudDataNotTheReport()
    {
        SignIn();
        using var prompt = new ManualResetEventSlim();
        _keychain.BlockProbe = prompt;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        // On another thread: without the slice around it, the blocked probe holds the caller too.
        Task<EnrichmentResult> enrichment = Task.Run(() => EnrichAsync(envelope));
        while (_keychain.Probed.Count == 0)
            await Task.Delay(10);
        _time.Advance(CloudBudget.Credential);

        try
        {
            EnrichmentResult result = await enrichment.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(envelope.Findings, result.Envelope.Findings);
            Assert.Equal(("unavailable", "credential-timeout"), (result.Envelope.Context!.Cloud!.Status, result.Envelope.Context.Cloud.Reason));
            Assert.Empty(_factory.Created);
            Assert.Equal(
                "Cloud data unavailable: reading the Xping Cloud sign-in took too long. Showing local results only.",
                Assert.Single(result.Hints).Text);
        }
        finally
        {
            prompt.Set();
        }
    }

    [Fact]
    public async Task AnUnreadableBatchCostsOnlyItsOwnTests()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Unreadable.Add("fp-120");

        EnrichmentResult result = await EnrichAsync(Many(200));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("partial", "invalid-response"), (cloud.Status, cloud.Reason));
        Assert.Equal(2, _factory.Client.Batches.Count);
        Assert.Equal(100, result.Envelope.Findings.Count(f => f.Cloud != null));
        Assert.All(result.Envelope.Findings.Skip(100), f => Assert.Null(f.Cloud));
    }

    [Fact]
    public async Task AnAnswerForATestNotAskedAboutDoesNotCountAsAnAnswer()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Stray.Add("fp-1");

        EnrichmentResult result = await EnrichAsync(Many(3));

        Assert.Equal(("partial", "invalid-response"), (result.Envelope.Context!.Cloud!.Status, result.Envelope.Context.Cloud.Reason));
        Assert.Null(result.Envelope.Findings[1].Cloud);
    }

    public static TheoryData<Exception, string, string> ReadFailures() => new()
    {
        { new CloudApiException(503, null, "Xping Cloud failed to answer (HTTP 503)."), "server", $"could not reach {CredentialTestData.CloudUrl} (HTTP 503). Showing local results only." },
        { new CloudApiException(429, null, "Xping Cloud is rate limiting requests; try again in a minute."), "rate-limited", "Xping Cloud is rate limiting requests; try again in a minute." },
        { new AuthFailureException(1, AuthErrorCodes.CloudUnreachable, "x") { Reason = "timeout", Target = "https://api.tests.invalid" }, "timeout", "could not reach https://api.tests.invalid (timeout). Showing local results only." },
        { new AuthFailureException(1, AuthErrorCodes.CloudUnreachable, "x") { Reason = "connection refused", Target = "https://api.tests.invalid" }, "network", "could not reach https://api.tests.invalid (connection refused). Showing local results only." },
        { new CloudResponseException("The DataGateway answered the digest with a body the CLI cannot read."), "invalid-response", "The DataGateway answered the digest with a body the CLI cannot read." },
        { AuthFailureException.VersionMismatch("Xping Cloud no longer accepts this version of the CLI. Please upgrade the xping CLI."), "server", "Xping Cloud no longer accepts this version of the CLI. Please upgrade the xping CLI." },
        { new AuthFailureException(1, AuthErrorCodes.OAuthError, "The sign-in could not be refreshed."), "network", "The sign-in could not be refreshed." }
    };

    [Theory]
    [MemberData(nameof(ReadFailures))]
    public async Task NothingAnsweredIsUnavailableWithTheReasonOfTheFailure(Exception failure, string reason, string hint)
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Throw = failure;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("unavailable", reason), (cloud.Status, cloud.Reason));
        Assert.Equal(("unavailable", reason), (cloud.Projects[0].Status, cloud.Projects[0].Reason));
        Assert.Equal("Cloud data unavailable: " + hint, Assert.Single(result.Hints).Text);
        Assert.False(result.Hints[0].Always);
        Assert.All(result.Envelope.Findings, f => Assert.Null(f.Cloud));
    }

    [Fact]
    public async Task AnInvalidSignInFallsBackToTheAmbientKeyForTheTestsStillUnanswered()
    {
        SignIn();
        _environment["XPING_APIKEY"] = ApiKey;
        using var login = new ScriptedClient();
        login.ThrowFor["fp-120"] = new LoginRequiredException();
        using var key = new ScriptedClient();
        _factory.ClientFor = credential => credential.Source == CredentialSource.StoredLogin ? login : key;

        EnrichmentResult result = await EnrichAsync(Many(150));

        Assert.Equal([CredentialSource.StoredLogin, CredentialSource.ApiKeyEnv], _factory.Created.Select(c => c.Source));
        Assert.Empty(login.Reads.Intersect(key.Reads));
        Assert.Equal(150, login.Reads.Count + key.Reads.Count);
        Assert.Equal(Enumerable.Range(100, 50).Select(i => $"fp-{i}"), key.Reads.Order(StringComparer.Ordinal).OrderBy(f => f.Length));

        CloudHint hint = Assert.Single(result.Hints);
        Assert.Equal(("Cloud data unavailable: your sign-in is no longer valid. Run xping login.", true), (hint.Text, hint.Always));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("api-key", "ok", (string?)null, 150), (cloud.Credential, cloud.Status, cloud.WorkspaceId, cloud.EnrichedTests));
    }

    [Fact]
    public async Task AnInvalidSignInWithNoKeyBehindItReportsLoginRequired()
    {
        SignIn();
        _factory.Client.Throw = new LoginRequiredException();

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Single(_factory.Created);
        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("login", "login-required", "none"), (cloud.Credential, cloud.Status, cloud.Reason));
        Assert.Equal("login-required", cloud.Projects[0].Status);
        Assert.Equal("Cloud data unavailable: your sign-in is no longer valid. Run xping login.", Assert.Single(result.Hints).Text);
        Assert.Equal(CredentialTestData.Record().WorkspaceId, cloud.WorkspaceId);
        Assert.True(result.Hints[0].Always);
    }

    [Fact]
    public async Task ARefusedKeyIsForbiddenAndItsMessageIsTheHintEvenOnAPipe()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Throw = new CloudApiException(403, "Error.ApiKey.InsufficientScope",
            "This API key cannot read Cloud data (upload only). Sign in with `xping login` instead.");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Single(_factory.Created);
        Assert.Equal(("forbidden", "none"), (result.Envelope.Context!.Cloud!.Status, result.Envelope.Context.Cloud.Reason));
        CloudHint hint = Assert.Single(result.Hints);
        Assert.Equal(
            ("Cloud data unavailable: This API key cannot read Cloud data (upload only). Sign in with `xping login` instead.", true),
            (hint.Text, hint.Always));
    }

    [Fact]
    public async Task AKeyCloudDoesNotRecogniseIsLoginRequired()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Throw = new CloudApiException(401, null, "Xping Cloud refused the request (HTTP 401).");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Equal("login-required", result.Envelope.Context!.Cloud!.Status);
        Assert.True(Assert.Single(result.Hints).Always);
    }

    [Fact]
    public async Task AnAssemblyNoSourceBindsIsNoProject()
    {
        _environment.Remove("XPING_PROJECTID");
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");
        string assembly = envelope.Findings[0].Subject.Assembly!;

        EnrichmentResult result = await EnrichAsync(envelope);

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(
            $"Cloud data unavailable: no matching Cloud project for {assembly}. Use --project <key>.",
            Assert.Single(result.Hints).Text);
        Assert.Equal(("no-project", "not-found"), (cloud.Status, cloud.Reason));
        Assert.Equal(new CloudProjectDto(assembly, null, null, "no-project", "not-found"), cloud.Projects[0]);
        Assert.All(cloud.Projects, p => Assert.Equal("no-project", p.Status));
        Assert.Empty(_factory.Client.Reads);
    }

    [Fact]
    public async Task TwoAssembliesOneUnboundIsPartialWithARowEach()
    {
        // Contract §9.4: a report is partial when any project row is not ok.
        _environment.Remove("XPING_PROJECTID");
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = Many(2);
        envelope = envelope with
        {
            Findings = [envelope.Findings[0], envelope.Findings[1] with { Subject = envelope.Findings[1].Subject with { Assembly = "Other.Tests" } }]
        };
        string assembly = envelope.Findings[0].Subject.Assembly!;

        EnrichmentResult result = await EnrichAsync(envelope, pins: new Dictionary<string, string> { [assembly] = Project });

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("partial", "not-found"), (cloud.Status, cloud.Reason));
        Assert.Equal(
            [new CloudProjectDto(assembly, Project, "session", "ok", "none"), new CloudProjectDto("Other.Tests", null, null, "no-project", "not-found")],
            cloud.Projects);
        Assert.Equal("Cloud data partial: 1 of 2 tests answered (not-found).", Assert.Single(result.Hints).Text);
    }

    [Fact]
    public async Task AssembliesOnOneProjectAreAskedInOneBatch()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = Many(2);
        envelope = envelope with
        {
            Findings = [envelope.Findings[0], envelope.Findings[1] with { Subject = envelope.Findings[1].Subject with { Assembly = "Other.Tests" } }]
        };

        EnrichmentResult result = await EnrichAsync(envelope);

        Assert.Equal(["fp-0", "fp-1"], Assert.Single(_factory.Client.Batches));
        Assert.Equal(2, result.Envelope.Context!.Cloud!.Projects.Count);
    }

    [Fact]
    public async Task TheSessionPinBindsWhenNothingIsConfigured()
    {
        _environment.Remove("XPING_PROJECTID");
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");
        string assembly = envelope.Findings[0].Subject.Assembly!;

        EnrichmentResult result = await EnrichAsync(envelope, pins: new Dictionary<string, string> { [assembly] = "pinned" });

        Assert.Equal(new CloudProjectDto(assembly, "pinned", "session", "ok", "none"), result.Envelope.Context!.Cloud!.Projects[0]);
        Assert.All(_factory.Client.ProjectsRead, key => Assert.Equal("pinned", key));
    }

    [Fact]
    public async Task TheProjectFlagWinsOverTheEnvironment()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"), projectFlag: "from-flag");

        Assert.Equal("flag", result.Envelope.Context!.Cloud!.Projects[0].Source);
        Assert.All(_factory.Client.ProjectsRead, key => Assert.Equal("from-flag", key));
    }

    [Theory]
    [InlineData("--project")]
    [InlineData("XPING_PROJECTID")]
    public async Task AConfiguredKeyThatNamesNoProjectIsSaidRatherThanReadAsNoData(string origin)
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.ProjectMissing = true;

        EnrichmentResult result = await EnrichAsync(
            ReportFixtures.Get("latest-run"), projectFlag: origin == "--project" ? "checkout-tset" : null);

        string key = origin == "--project" ? "checkout-tset" : Project;
        Assert.Equal(
            $"Cloud data unavailable: no Cloud project '{key}' (from {origin}). Check the key, or pass --project <key>.",
            Assert.Single(result.Hints).Text);
        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("no-project", "not-found"), (cloud.Status, cloud.Reason));
        Assert.Equal(key, cloud.Projects[0].ProjectKey);
    }

    [Fact]
    public async Task AnotherSignatureAlgorithmKeepsTheDigestAndMakesEveryMatchUnknown()
    {
        // Contract §11.
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Algorithm = "emh-2";
        ReportEnvelope envelope = WithModes(ReportFixtures.Get("latest-run"));

        EnrichmentResult result = await EnrichAsync(envelope, sessions: [Failed(envelope)]);

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("ok", "signature-algorithm", "emh-2"), (cloud.Status, cloud.Reason, cloud.SignatureAlgorithm));
        FindingDto finding = result.Envelope.Findings.First(f => f.Cloud != null);
        Assert.Equal("known-flaky", finding.Cloud!.Verdict);
        Assert.All(finding.FailureModes, m => Assert.Equal("unknown", m.Cloud!.Match));
        Assert.All(result.Envelope.LatestRun!.Failures, f => Assert.Equal("unknown", f.Cloud!.Match));
    }

    [Fact]
    public async Task EveryModeOfAnEnrichedFindingIsMatchedOnItsMessageHash()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = WithModes(ReportFixtures.Get("latest-run"));

        EnrichmentResult result = await EnrichAsync(envelope);

        FindingDto finding = result.Envelope.Findings.First(f => f.Cloud != null);
        Assert.Equal(
            [("matched", ScriptedClient.Hash), ("unseen", null), ("unsignable", null)],
            finding.FailureModes.Select(m => (m.Cloud!.Match, m.Cloud.Signature?.ErrorMessageHash)));
    }

    [Fact]
    public async Task ALatestRunFailureIsMatchedOnTheHashOfItsFinalAttempt()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        EnrichmentResult result = await EnrichAsync(envelope, sessions: [Failed(envelope)]);

        CloudFailureDto failure = result.Envelope.LatestRun!.Failures[0].Cloud!;
        Assert.Equal(("matched", ScriptedClient.Hash), (failure.Match, failure.Signature!.ErrorMessageHash));
        Assert.Equal([ScriptedClient.Hash], _factory.Client.Requests.SelectMany(r => r.Tests).First(t => t.Fingerprint == envelope.LatestRun!.Failures[0].Subject.Fingerprint).ErrorMessageHashes);
    }

    [Theory]
    [InlineData("Flaky", "passing", "not-flaky", true)]
    [InlineData("Flaky", "known-flaky", "not-flaky", false)]
    [InlineData("Flaky", "passing", "not-examined", false)]
    [InlineData("AlwaysFailing", "passing", "flaky", true)]
    [InlineData("AlwaysFailing", "broken", "not-flaky", false)]
    [InlineData("RetryMasked", "passing", "not-flaky", false)]
    public async Task OnlyAFlakyOrAlwaysFailingFindingCanDisagreeWithCloud(string kind, string verdict, string flaky, bool disagrees)
    {
        // Contract L3, and L4: an observation never disagrees.
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Verdict = verdict;
        _factory.Client.FlakyState = flaky;
        ReportEnvelope envelope = Many(1);
        envelope = envelope with { Findings = [envelope.Findings[0] with { Kind = kind }] };

        EnrichmentResult result = await EnrichAsync(envelope);

        Assert.Equal(disagrees, result.Envelope.Findings[0].Cloud!.DisagreesWithLocal);
    }

    [Fact]
    public async Task ANameCloudDoesNotShareIsAMismatchAndAFingerprintTwoTestsCarryIsShared()
    {
        // Contract §9.3: both come from a pinned fingerprint reused.
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = Many(3);
        SubjectDto first = envelope.Findings[0].Subject;
        envelope = envelope with
        {
            Findings =
            [
                envelope.Findings[0],
                envelope.Findings[1] with { Subject = first with { FullyQualifiedName = first.FullyQualifiedName + "Renamed" } },
                envelope.Findings[2]
            ]
        };
        _factory.Client.TestName = fingerprint => fingerprint == "fp-2" ? "Some.Other.Name" : null;

        EnrichmentResult result = await EnrichAsync(envelope);

        Assert.Equal(["shared", "shared", "name-mismatch"], result.Envelope.Findings.Select(f => f.Cloud!.Identity));
    }

    [Fact]
    public async Task TheTestSubjectIsAskedFirstAndItsRunsAreRead()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("history-flaky");
        string fingerprint = envelope.Test!.Test!.Fingerprint!;

        EnrichmentResult result = await EnrichAsync(envelope);

        Assert.Equal(fingerprint, _factory.Client.Requests[0].Tests[0].Fingerprint);
        CloudTestHistoryDto test = result.Envelope.Test!.Cloud!;
        Assert.Equal(("ok", 365, true), (test.RunsState, test.RetentionDays, test.HasMore));
        Assert.Equal("ok", test.Digest!.State);
        Assert.Equivalent(
            new CloudRunDto("4f1c0c9e", ScriptedClient.Computed, "failed", 2, "failed", 812, "canonical", true, "gh-runner-17", "main", "0053b0ef", null, "System.TimeoutException", ScriptedClient.Hash),
            Assert.Single(test.Runs),
            strict: true);
        Assert.Equal((Project, fingerprint, CloudEnricher.RunsLimit), Assert.Single(_factory.Client.RunsRead));
    }

    [Fact]
    public async Task TheRunsAreNotAskedForWithLessThanASecondLeft()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.OnDigest = () => _time.Advance(CloudBudget.Total - TimeSpan.FromMilliseconds(500));

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("history-flaky"));

        Assert.Equal("budget", result.Envelope.Test!.Cloud!.RunsState);
        Assert.Empty(_factory.Client.RunsRead);
    }

    [Fact]
    public async Task RunsThatCannotBeReadCostOnlyTheRuns()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.RunsThrow = new CloudApiException(503, null, "Xping Cloud failed to answer (HTTP 503).");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("history-flaky"));

        Assert.Equal("unavailable", result.Envelope.Test!.Cloud!.RunsState);
        Assert.Equal("ok", result.Envelope.Context!.Cloud!.Status);
        Assert.NotNull(result.Envelope.Test.Cloud.Digest);
    }

    [Fact]
    public async Task WithoutACredentialTheTestSaysItsRunsWereNotAttempted()
    {
        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("history-flaky"));

        Assert.Equal(new CloudTestHistoryDto(null, [], "not-attempted", null, false), result.Envelope.Test!.Cloud! with { Runs = [] });
    }

    [Fact]
    public async Task ATestCloudDoesNotKnowHasNoRunsToRead()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Missing = true;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("history-flaky"));

        Assert.Equal(("ok", "unknown"), (result.Envelope.Test!.Cloud!.RunsState, result.Envelope.Test.Cloud.Digest!.State));
        Assert.Empty(result.Envelope.Test.Cloud.Runs);
        Assert.Empty(_factory.Client.RunsRead);
    }

    [Fact]
    public async Task ASettingsFileThatCannotBeReadAsksNothingAndSaysWhy()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        await File.WriteAllTextAsync(Path.Combine(_directory, "appsettings.json"), "{ not json");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("not-attempted", null, null), (cloud.Status, cloud.CloudUrl, cloud.Credential));
        Assert.StartsWith("Cloud data unavailable: Could not read ", Assert.Single(result.Hints).Text, StringComparison.Ordinal);
        Assert.Empty(_factory.Created);
    }

    [Fact]
    public async Task AClientThatCannotBeBuiltCostsEveryTest()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.ClientFor = _ => throw new AuthFailureException(1, AuthErrorCodes.CloudUnreachable, "x") { Reason = "connection refused" };

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("unavailable", "network", 0), (cloud.Status, cloud.Reason, cloud.EnrichedTests));
        Assert.All(cloud.Projects, p => Assert.Equal(("unavailable", "network", (string?)null), (p.Status, p.Reason, p.ProjectKey)));
    }

    [Fact]
    public async Task ARememberedNameMatchCloudNoLongerHasIsListedAgainAndRead()
    {
        // §11.2: the digest's 404 is the one sign a cached match went stale.
        SignIn();
        _environment.Remove("XPING_PROJECTID");
        ReportEnvelope envelope = Many(1);
        string assembly = envelope.Findings[0].Subject.Assembly!;
        _factory.Client.Projects = [new ProjectSummary("old-key", assembly, null)];
        await EnrichAsync(envelope);

        _factory.Client.Projects = [new ProjectSummary("new-key", assembly, null)];
        _factory.Client.MissingProjects.Add("old-key");
        EnrichmentResult result = await EnrichAsync(envelope);

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("ok", "new-key", "name-match"), (cloud.Status, cloud.Projects[0].ProjectKey, cloud.Projects[0].Source));
        Assert.Equal(["old-key", "old-key", "new-key"], _factory.Client.ProjectsRead);
        Assert.NotNull(result.Envelope.Findings[0].Cloud);
    }

    [Fact]
    public async Task AFailureOutsideTheReadsStillLeavesTheLocalReport()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.ClientFor = _ => throw new InvalidOperationException("defect");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.All(result.Envelope.Findings, f => Assert.Null(f.Cloud));
        Assert.Equal(("unavailable", "none"), (result.Envelope.Context!.Cloud!.Status, result.Envelope.Context.Cloud.Reason));
    }

    private void SignIn() =>
        _keychain.Add(CredentialTestData.Record() with { CloudUrl = CredentialTestData.CloudUrl });

    private async Task<EnrichmentResult> EnrichAsync(
        ReportEnvelope envelope,
        string? projectFlag = null,
        Dictionary<string, string>? pins = null,
        IReadOnlyList<TestSession>? sessions = null)
    {
        using ServiceProvider services = BuildServices();

        var request = new EnrichmentRequest(_directory, projectFlag, pins ?? [], sessions ?? [], FindingsOf(envelope));
        return await services.GetRequiredService<CloudEnricher>().EnrichAsync(envelope, request, CancellationToken.None).ConfigureAwait(false);
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false));
        services.AddSingleton(new GlobalOptions { CloudUrl = CredentialTestData.CloudUrl });
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(new XpingHome(Path.Combine(_directory, ".xping-home")));
        services.AddSingleton<IEnvironmentVariableProvider>(new DictionaryEnvironment(_environment));
        services.AddSingleton(provider => new CredentialStoreSelector(provider.GetRequiredService<FileCredentialStore>(), _keychain));
        services.AddSingleton<ICloudApiClientFactory>(_factory);
        _factory.Time = _time;
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The ranked findings behind an envelope fixture, as the coordinator would have passed them.
    /// </summary>
    private static List<Finding> FindingsOf(ReportEnvelope envelope) =>
    [
        .. envelope.Findings
            .Where(f => f.Subject.Fingerprint is not null)
            .Select(f => new Finding(
                f.Id,
                Enum.TryParse(f.Kind, out FindingKind kind) ? kind : FindingKind.Flaky,
                Severity.High,
                EvidenceLevel.High,
                20,
                new FindingSubject.SingleTest(new TestReference(
                    f.Subject.Fingerprint!,
                    f.Subject.FullyQualifiedName ?? f.Subject.Fingerprint!,
                    f.Subject.DisplayName ?? f.Subject.Fingerprint!,
                    null,
                    null,
                    f.Subject.Assembly!)),
                new NoEvidence(),
                0.5))
    ];

    private static IEnumerable<SubjectDto> Subjects(ReportEnvelope envelope) =>
        envelope.Findings.Select(f => f.Subject).Concat(envelope.LatestRun?.Failures.Select(f => f.Subject) ?? []);

    private static List<string> Fingerprints(ReportEnvelope envelope) =>
    [
        .. Subjects(envelope)
            .Select(s => s.Fingerprint)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
    ];

    private static ReportEnvelope Many(int count)
    {
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");
        FindingDto template = envelope.Findings.First(f => f.Subject.Fingerprint != null);

        return envelope with
        {
            LatestRun = null,
            Findings =
            [
                .. Enumerable.Range(0, count).Select(i =>
                    template with
                    {
                        Id = $"f_{i}",
                        Subject = template.Subject with { Fingerprint = $"fp-{i}", FullyQualifiedName = $"Tests.T{i}" }
                    })
            ]
        };
    }

    /// <summary>
    /// The first single-test finding given three modes: one Cloud has seen, one it has not, and one
    /// with no message.
    /// </summary>
    private static ReportEnvelope WithModes(ReportEnvelope envelope)
    {
        FindingDto finding = envelope.Findings.First(f => f.Subject.Fingerprint != null);
        FailureModeDto mode = new(
            "abc123", "System.TimeoutException", "timed out", [], false, false, false, 3, DateTime.UnixEpoch, null, 0, false, false, ScriptedClient.Hash);

        FindingDto withModes = finding with
        {
            FailureModes = [mode, mode with { ErrorMessageHash = new string('b', 64) }, mode with { ErrorMessageHash = null }]
        };

        return envelope with { Findings = [.. envelope.Findings.Select(f => ReferenceEquals(f, finding) ? withModes : f)] };
    }

    /// <summary>
    /// The newest session, in which every latest-run failure failed with <see cref="ScriptedClient.Hash"/>.
    /// </summary>
    private static TestSession Failed(ReportEnvelope envelope)
    {
        return TestSessionFactory.Session(
            0,
            [
                .. envelope.LatestRun!.Failures.Select(f => TestSessionFactory.Execution(
                    f.Subject.ShortName ?? "Failed",
                    Sdk.Core.Models.Executions.TestOutcome.Failed,
                    assembly: f.Subject.Assembly!,
                    errorMessage: "timed out",
                    fingerprint: f.Subject.Fingerprint,
                    errorMessageHash: ScriptedClient.Hash))
            ]);
    }

    private static TestSession Session(string assembly) =>
        TestSessionFactory.Session(0, [TestSessionFactory.Execution("Any", assembly: assembly)]);

    private sealed record NoEvidence : FindingEvidence;

    private sealed class DictionaryEnvironment(Dictionary<string, string> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }

    private sealed class ScriptedFactory : ICloudApiClientFactory
    {
        public ScriptedClient Client { get; } = new();

        public Func<ResolvedCredential, ScriptedClient>? ClientFor { get; set; }

        public List<ResolvedCredential> Created { get; } = [];

        public TimeProvider? Time { get; set; }

        public Task<ICloudApiClient> CreateAsync(ResolvedCredential credential, string cloudUrl, CloudBudget budget, CancellationToken cancellationToken)
        {
            Created.Add(credential);
            return Task.FromResult<ICloudApiClient>(ClientFor?.Invoke(credential) ?? Client);
        }
    }

    private sealed class ScriptedClient : ICloudApiClient
    {
        public const string Hash = "ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12";

        public static readonly DateTimeOffset Computed = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

        private int _inFlight;

        public ConcurrentQueue<string> ReadsQueue { get; } = new();

        public IReadOnlyList<string> Reads => [.. ReadsQueue];

        public ConcurrentQueue<DigestRequest> RequestsQueue { get; } = new();

        public IReadOnlyList<DigestRequest> Requests => [.. RequestsQueue];

        public IReadOnlyList<IReadOnlyList<string>> Batches => [.. Requests.Select(r => (IReadOnlyList<string>)[.. r.Tests.Select(t => t.Fingerprint)])];

        public ConcurrentQueue<string> ProjectsRead { get; } = new();

        public ConcurrentQueue<(string Project, string Fingerprint, int Limit)> RunsRead { get; } = new();

        public HashSet<string> Hang { get; } = [];

        public HashSet<string> Unreadable { get; } = [];

        public HashSet<string> BadState { get; } = [];

        public HashSet<string> Stray { get; } = [];

        public Dictionary<string, Exception> ThrowFor { get; } = [];

        public string FlakyState { get; set; } = "flaky";

        public string Verdict { get; set; } = "known-flaky";

        public string Algorithm { get; set; } = "emh-1";

        public Func<string, string?> TestName { get; set; } = _ => null;

        public TaskCompletionSource Hanging { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Throw { get; set; }

        public Exception? RunsThrow { get; set; }

        public Action? OnDigest { get; set; }

        public bool Missing { get; set; }

        public bool ProjectMissing { get; set; }

        public TimeSpan Delay { get; set; }

        public int MaxInFlight { get; private set; }

        public IReadOnlyList<string> Warnings => [];

        public IReadOnlyList<ProjectSummary> Projects { get; set; } = [];

        public HashSet<string> MissingProjects { get; } = [];

        public Task<PagedResult<ProjectSummary>> ListProjectsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ProjectSummary>(Projects, Projects.Count, pageNumber, pageSize));

        public async Task<DigestResponse?> GetDigestAsync(string projectKey, DigestRequest request, CancellationToken cancellationToken)
        {
            if (Throw is { } exception)
                throw exception;

            int inFlight = Interlocked.Increment(ref _inFlight);
            lock (ReadsQueue)
                MaxInFlight = Math.Max(MaxInFlight, inFlight);

            try
            {
                ProjectsRead.Enqueue(projectKey);
                if (ProjectMissing || MissingProjects.Contains(projectKey))
                    return null;

                List<string> fingerprints = [.. request.Tests.Select(t => t.Fingerprint)];
                RequestsQueue.Enqueue(request);

                if (fingerprints.Exists(Unreadable.Contains))
                    throw new CloudResponseException("The DataGateway answered the digest with a body the CLI cannot read.");

                if (fingerprints.Find(ThrowFor.ContainsKey) is { } thrower)
                {
                    // After the other batches have answered, so the fallback has something to skip.
                    while (ReadsQueue.Count < 100)
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

                    throw ThrowFor[thrower];
                }

                if (fingerprints.Exists(Hang.Contains))
                {
                    Hanging.TrySetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                if (Delay > TimeSpan.Zero)
                    await Task.Delay(Delay, CancellationToken.None).ConfigureAwait(false);

                foreach (string fingerprint in fingerprints)
                    ReadsQueue.Enqueue(fingerprint);

                OnDigest?.Invoke();

                // A stray answer takes the place of the test it was scripted for.
                return new DigestResponse(
                    DateTimeOffset.UnixEpoch,
                    Algorithm,
                    365,
                    [.. fingerprints.Select(f => Digest(Stray.Contains(f) ? "fp-stray" : f))],
                    [.. request.SessionKeys.Select(k => new DigestSession(k, "complete", true, true))]);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Task<RunsResponse?> GetRunsAsync(string projectKey, string fingerprint, int limit, CancellationToken cancellationToken)
        {
            RunsRead.Enqueue((projectKey, fingerprint, limit));
            if (RunsThrow is { } exception)
                throw exception;

            return Task.FromResult<RunsResponse?>(new RunsResponse(
                365,
                true,
                [new CloudRun("4f1c0c9e", Computed, "failed", 2, "failed", 812, "canonical", true, "gh-runner-17", "main", "0053b0ef", null, "System.TimeoutException", Hash)]));
        }

        private TestDigest Digest(string fingerprint) =>
            Missing
                ? new TestDigest(fingerprint, "unknown", null, null, null, null, null, null, null, null, null)
                : new TestDigest(
                    fingerprint,
                    BadState.Contains(fingerprint) ? "archived" : "ok",
                    TestName(fingerprint),
                    null,
                    812,
                    Verdict,
                    new DigestConfidence(
                        "scored", 0.62, null, null, "moderately-reliable", "stable", -0.03, 48, "robust", null, null, null,
                        new DigestWindow("days", 35, Computed, null), null, Computed, "current"),
                    new DigestFlaky(FlakyState, null, null, null, null, null, null, "current"),
                    new DigestCanonical(10, 2, 0, null, "passed", Computed, new DigestWindow("runs", 10, null, Computed)),
                    new DigestTransition("flakiness-introduced", "reliable", "unreliable", Computed),
                    [
                        new DigestSignature(Hash, "System.TimeoutException", true, 3, 1, 2, 1, 2, Computed, Computed, ["c0ffee"], null),
                        new DigestSignature(new string('b', 64), null, true, 0, 0, 0, 0, 0, null, null, [], null)
                    ]);

        public void Dispose()
        {
        }
    }
}
