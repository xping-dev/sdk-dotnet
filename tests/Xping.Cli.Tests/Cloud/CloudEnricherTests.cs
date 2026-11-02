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
using Xping.Cli.Tests.Auth.Store;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The enrichment's own rules — budget, concurrency, fallback, binding, status — with a scripted
/// DataGateway client. The real pipeline against <see cref="FakeCloud"/> is
/// <see cref="ReportEnrichmentTests"/>.
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
    public async Task NoCredentialLeavesTheEnvelopeAloneAndAsksNothing()
    {
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        EnrichmentResult result = await EnrichAsync(envelope);

        Assert.Same(envelope, result.Envelope);
        Assert.Empty(result.Hints);
        Assert.Empty(_factory.Created);
    }

    [Fact]
    public async Task EveryTestWithAFingerprintIsReadOnceAndLabelledAsCloud()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");

        EnrichmentResult result = await EnrichAsync(envelope);

        IReadOnlyList<string> expected = Fingerprints(envelope);
        Assert.Equal(expected.Order(), _factory.Client.Reads.Order());
        Assert.All(result.Envelope.Findings.Where(f => f.Subject.Fingerprint != null), f => Assert.NotNull(f.Cloud));
        Assert.All(result.Envelope.LatestRun!.Failures, f => Assert.NotNull(f.Cloud));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("api-key", "ok", null), (cloud.Credential, cloud.Status, cloud.Reason));
        Assert.Equal(new CloudProjectDto(envelope.Context!.Assembly!, Project, "env"), cloud.Project);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public async Task TheDigestIsMappedOntoTheCloudViewAndStampedWithTheReadTime()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        CloudTestDto cloud = result.Envelope.Findings.First(f => f.Cloud != null).Cloud!;
        Assert.Equal(
            new CloudTestDto(0.62, "moderately-reliable", "robust", 812, "stable", -0.03, true, null, _time.GetUtcNow()),
            cloud);
    }

    [Fact]
    public async Task AGroupFindingIsNotRead()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope cluster = ReportFixtures.Get("cluster");

        EnrichmentResult result = await EnrichAsync(cluster);

        Assert.Empty(_factory.Created);
        Assert.Null(result.Envelope.Findings[0].Cloud);
        Assert.Equal("not-attempted", result.Envelope.Context!.Cloud!.Status);
    }

    [Fact]
    public async Task NoMoreThanFourDigestRequestsAreInFlightAtOnce()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Delay = TimeSpan.FromMilliseconds(20);

        await EnrichAsync(Many(1000));

        Assert.Equal(1000, _factory.Client.Reads.Count);
        Assert.InRange(_factory.Client.MaxInFlight, 2, CloudEnricher.MaxConcurrency);
    }

    [Fact]
    public async Task TestsAreAskedAboutInBatchesOfAHundredInReportOrder()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(Many(250));

        Assert.Equal([100, 100, 50], _factory.Client.Batches.Select(b => b.Count).Order().Reverse());
        Assert.Contains(_factory.Client.Batches, b => b[0] == "fp-0" && b[^1] == "fp-99");
        Assert.All(result.Envelope.Findings, f => Assert.NotNull(f.Cloud));
    }

    [Fact]
    public async Task ReadsStillPendingWhenTheBudgetRunsOutAreLeftOutAndTheStatusIsPartial()
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
        Assert.Equal("partial", cloud.Status);
        Assert.Contains("timeout", cloud.Reason, StringComparison.Ordinal);
        Assert.All(result.Envelope.Findings.Take(100), f => Assert.NotNull(f.Cloud));
        Assert.All(result.Envelope.Findings.Skip(100), f => Assert.Null(f.Cloud));
        Assert.False(Assert.Single(result.Hints).Always);
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

            Assert.Same(envelope, result.Envelope);
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

        EnrichmentResult result = await EnrichAsync(Many(250));

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal("partial", cloud.Status);
        Assert.Contains("cannot read", cloud.Reason, StringComparison.Ordinal);
        Assert.Equal(3, _factory.Client.Batches.Count);
        Assert.Equal(150, result.Envelope.Findings.Count(f => f.Cloud != null));
        Assert.All(result.Envelope.Findings.Skip(100).Take(100), f => Assert.Null(f.Cloud));
    }

    [Fact]
    public async Task AnAnswerForATestNotAskedAboutDoesNotCountAsAnAnswer()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Stray.Add("fp-1");

        EnrichmentResult result = await EnrichAsync(Many(3));

        Assert.Equal("partial", result.Envelope.Context!.Cloud!.Status);
        Assert.Null(result.Envelope.Findings[1].Cloud);
    }

    [Fact]
    public async Task AnUnreachableCloudStopsTheReadsAndFallsBackToNothing()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Throw = new AuthFailureException(1, AuthErrorCodes.CloudUnreachable, "x") { Reason = "HTTP 503", Target = "https://api.tests.invalid" };

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Equal(
            "Cloud data unavailable: could not reach https://api.tests.invalid (HTTP 503). Showing local results only.",
            Assert.Single(result.Hints).Text);
        Assert.Equal("unavailable", result.Envelope.Context!.Cloud!.Status);
        Assert.All(result.Envelope.Findings, f => Assert.Null(f.Cloud));
    }

    [Fact]
    public async Task AnInvalidSignInFallsBackToTheAmbientKeyOnANewClient()
    {
        SignIn();
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.ClientFor = credential => credential.Source == CredentialSource.StoredLogin
            ? new ScriptedClient { Throw = new LoginRequiredException() }
            : new ScriptedClient();

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Equal([CredentialSource.StoredLogin, CredentialSource.ApiKeyEnv], _factory.Created.Select(c => c.Source));
        CloudHint hint = Assert.Single(result.Hints);
        Assert.True(hint.Always);
        Assert.Equal("Cloud data unavailable: your sign-in is no longer valid. Run xping login.", hint.Text);

        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("api-key", "ok", (string?)null), (cloud.Credential, cloud.Status, cloud.WorkspaceId));
    }

    [Fact]
    public async Task AnInvalidSignInWithNoKeyBehindItReportsLoginRequired()
    {
        SignIn();
        _factory.Client.Throw = new LoginRequiredException();

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Single(_factory.Created);
        CloudContextDto cloud = result.Envelope.Context!.Cloud!;
        Assert.Equal(("stored-login", "login-required"), (cloud.Credential, cloud.Status));
        Assert.Equal(Assert.Single(result.Hints).Text, "Cloud data unavailable: " + cloud.Reason);
        Assert.Equal(CredentialTestData.Record().WorkspaceId, cloud.WorkspaceId);
        Assert.True(result.Hints[0].Always);
    }

    [Fact]
    public async Task ARefusedKeyIsNotRetriedAndItsMessageIsTheHint()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Throw = new CloudApiException(403, "Error.ApiKey.InsufficientScope",
            "This API key cannot read Cloud data (upload only). Sign in with `xping login` instead.");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Single(_factory.Created);
        Assert.Equal(
            "Cloud data unavailable: This API key cannot read Cloud data (upload only). Sign in with `xping login` instead.",
            Assert.Single(result.Hints).Text);
    }

    [Fact]
    public async Task AnAssemblyNoSourceBindsGetsTheProjectHint()
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
        Assert.Equal(("unavailable", $"no matching Cloud project for {assembly}. Use --project <key>."), (cloud.Status, cloud.Reason));
        Assert.Empty(_factory.Client.Reads);
    }

    [Fact]
    public async Task TheSessionPinBindsWhenNothingIsConfigured()
    {
        _environment.Remove("XPING_PROJECTID");
        _environment["XPING_APIKEY"] = ApiKey;
        ReportEnvelope envelope = ReportFixtures.Get("latest-run");
        string assembly = envelope.Findings[0].Subject.Assembly!;

        EnrichmentResult result = await EnrichAsync(envelope, pins: new Dictionary<string, string> { [assembly] = "pinned" });

        Assert.Equal(new CloudProjectDto(assembly, "pinned", "session"), result.Envelope.Context!.Cloud!.Project);
        Assert.All(_factory.Client.ProjectsRead, key => Assert.Equal("pinned", key));
    }

    [Fact]
    public async Task TheProjectFlagWinsOverTheEnvironment()
    {
        _environment["XPING_APIKEY"] = ApiKey;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"), projectFlag: "from-flag");

        Assert.Equal("flag", result.Envelope.Context!.Cloud!.Project!.Source);
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
        Assert.Equal("unavailable", result.Envelope.Context!.Cloud!.Status);
    }

    [Fact]
    public async Task ATestCloudHasNoDataForIsAnsweredWithoutData()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.Client.Missing = true;

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.Equal("ok", result.Envelope.Context!.Cloud!.Status);
        Assert.All(result.Envelope.Findings, f => Assert.Null(f.Cloud));
        Assert.Empty(result.Hints);
    }

    [Fact]
    public async Task AFailureOutsideTheReadsStillLeavesTheLocalReport()
    {
        _environment["XPING_APIKEY"] = ApiKey;
        _factory.ClientFor = _ => throw new InvalidOperationException("defect");

        EnrichmentResult result = await EnrichAsync(ReportFixtures.Get("latest-run"));

        Assert.All(result.Envelope.Findings, f => Assert.Null(f.Cloud));
        Assert.Equal("unavailable", result.Envelope.Context!.Cloud!.Status);
    }

    private void SignIn() =>
        _keychain.Add(CredentialTestData.Record() with { CloudUrl = CredentialTestData.CloudUrl });

    private async Task<EnrichmentResult> EnrichAsync(
        ReportEnvelope envelope, string? projectFlag = null, Dictionary<string, string>? pins = null)
    {
        using ServiceProvider services = BuildServices();

        return await services.GetRequiredService<CloudEnricher>().EnrichAsync(
            envelope, new EnrichmentRequest(_directory, projectFlag, pins ?? []), CancellationToken.None).ConfigureAwait(false);
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
        return services.BuildServiceProvider();
    }

    private static List<string> Fingerprints(ReportEnvelope envelope) =>
    [
        .. envelope.Findings.Select(f => f.Subject)
            .Concat(envelope.LatestRun?.Failures.Select(f => f.Subject) ?? [])
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
                    template with { Subject = template.Subject with { Fingerprint = $"fp-{i}" } })
            ]
        };
    }

    private sealed class DictionaryEnvironment(Dictionary<string, string> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }

    private sealed class ScriptedFactory : ICloudApiClientFactory
    {
        public ScriptedClient Client { get; } = new();

        public Func<ResolvedCredential, ScriptedClient>? ClientFor { get; set; }

        public List<ResolvedCredential> Created { get; } = [];

        public Task<ICloudApiClient> CreateAsync(ResolvedCredential credential, string cloudUrl, CloudBudget budget, CancellationToken cancellationToken)
        {
            Created.Add(credential);
            return Task.FromResult<ICloudApiClient>(ClientFor?.Invoke(credential) ?? Client);
        }
    }

    private sealed class ScriptedClient : ICloudApiClient
    {
        private int _inFlight;

        public ConcurrentQueue<string> ReadsQueue { get; } = new();

        public IReadOnlyList<string> Reads => [.. ReadsQueue];

        public ConcurrentQueue<IReadOnlyList<string>> BatchesQueue { get; } = new();

        public IReadOnlyList<IReadOnlyList<string>> Batches => [.. BatchesQueue];

        public ConcurrentQueue<string> ProjectsRead { get; } = new();

        public HashSet<string> Hang { get; } = [];

        public HashSet<string> Unreadable { get; } = [];

        public HashSet<string> Stray { get; } = [];

        public TaskCompletionSource Hanging { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Throw { get; set; }

        public bool Missing { get; set; }

        public bool ProjectMissing { get; set; }

        public TimeSpan Delay { get; set; }

        public int MaxInFlight { get; private set; }

        public IReadOnlyList<string> Warnings => [];

        public Task<PagedResult<ProjectSummary>> ListProjectsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ProjectSummary>([], 0, pageNumber, pageSize));

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
                if (ProjectMissing)
                    return null;

                List<string> fingerprints = [.. request.Tests.Select(t => t.Fingerprint)];
                BatchesQueue.Enqueue(fingerprints);

                if (fingerprints.Exists(Unreadable.Contains))
                    throw new CloudResponseException("The DataGateway answered the digest with a body the CLI cannot read.");

                if (fingerprints.Exists(Hang.Contains))
                {
                    Hanging.TrySetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                if (Delay > TimeSpan.Zero)
                    await Task.Delay(Delay, CancellationToken.None).ConfigureAwait(false);

                foreach (string fingerprint in fingerprints)
                    ReadsQueue.Enqueue(fingerprint);

                // A stray answer takes the place of the test it was scripted for.
                return new DigestResponse(
                    DateTimeOffset.UnixEpoch,
                    "emh-1",
                    365,
                    [.. fingerprints.Select(f => Digest(Stray.Contains(f) ? "fp-stray" : f))],
                    []);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Task<RunsResponse?> GetRunsAsync(string projectKey, string fingerprint, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<RunsResponse?>(null);

        private TestDigest Digest(string fingerprint) =>
            Missing
                ? new TestDigest(fingerprint, "unknown", null, null, null, null, null, null, null, null, null)
                : new TestDigest(
                    fingerprint,
                    "ok",
                    null,
                    null,
                    812,
                    "known-flaky",
                    new DigestConfidence(
                        "scored", 0.62, null, null, "moderately-reliable", "stable", -0.03, 48, "robust", null, null, null, null, null, null, "current"),
                    new DigestFlaky("flaky", null, null, null, null, null, null, "current"),
                    null,
                    null,
                    []);

        public void Dispose()
        {
        }
    }
}
