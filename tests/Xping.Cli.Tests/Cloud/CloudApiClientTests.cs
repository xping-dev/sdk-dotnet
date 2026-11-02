/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud;
using Xping.Cli.Cloud.Digest;
using Xping.Cli.Configuration;

namespace Xping.Cli.Tests.Cloud;

public sealed class CloudApiClientTests : IAsyncDisposable
{
    private const string ApiKey = "team-read-key-0123456789";
    private const string Project = "shop-tests";
    private const string Fingerprint = "fp-checkout-0001";
    private const string DigestPath = "/gw/v1/projects/shop-tests/tests/digest";
    private const string Hash = "ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12";
    private const string SessionKey = "4f1c0c9e-0000-4000-8000-000000000001";

    private static readonly DigestRequest Request = new([new DigestRequestTest(Fingerprint, [Hash])], [SessionKey]);

    private readonly CliFlowHost _host = new();
    private readonly ServiceProvider _services;

    // On a clock nobody moves: these tests are about mapping, not time, so the budget never runs out.
    private readonly CloudBudget _budget = new(new FakeTimeProvider(), CancellationToken.None);

    public CloudApiClientTests()
    {
        _services = _host.BuildServices();
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddProject(Project, displayName: "Shop.Tests", slug: "shop");
        _host.Cloud.AddTest(Project, Fingerprint);
    }

    public async ValueTask DisposeAsync()
    {
        _budget.Dispose();
        await _services.DisposeAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task TheDigestIsAPostOfTheBatchInCamelCase()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        await client.GetDigestAsync(Project, Request, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.GatewayRequests);
        Assert.Equal(("POST", DigestPath), (request.Method, request.Path));
        Assert.StartsWith("application/json", request.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.Equal(
            $"{{\"tests\":[{{\"fingerprint\":\"{Fingerprint}\",\"errorMessageHashes\":[\"{Hash}\"]}}],\"sessionKeys\":[\"{SessionKey}\"]}}",
            request.Body);
    }

    [Fact]
    public async Task TheDigestIsReadWithEveryBlockAndEveryField()
    {
        _host.Cloud.AddTest(Project, "fp-seen", digest => digest["signatures"] = new JsonArray(new JsonObject
        {
            ["errorMessageHash"] = Hash,
            ["exceptionType"] = "InvalidOperationException",
            ["runs"] = 36,
            ["canonicalRuns"] = 34,
            ["ciRuns"] = 34,
            ["localRuns"] = 2,
            ["machines"] = 6,
            ["firstSeenAt"] = "2026-09-11T09:14:00Z",
            ["lastSeenAt"] = "2026-09-28T12:00:00Z",
            ["stackTraceHashes"] = new JsonArray("c0ffee"),
            ["window"] = new JsonObject { ["kind"] = "days", ["length"] = 35, ["end"] = "2026-09-28T12:05:00Z" }
        }));
        _host.Cloud.AddSession(SessionKey);
        using ICloudApiClient client = await ApiKeyClientAsync();

        DigestResponse? response = await client.GetDigestAsync(
            Project, new DigestRequest([new DigestRequestTest("fp-seen", [Hash])], [SessionKey]), CancellationToken.None);

        DigestWindow days35 = new("days", 35, At("2026-09-28T12:05:00Z"), null);
        DigestWindow days14 = new("days", 14, At("2026-09-28T12:05:00Z"), null);
        var expected = new DigestResponse(
            _host.Time.GetUtcNow(),
            "emh-1",
            365,
            [
                new TestDigest(
                    "fp-seen",
                    "ok",
                    "Shop.Tests.CheckoutTests.Checkout_Succeeds",
                    At("2026-09-28T12:00:00Z"),
                    812,
                    "known-flaky",
                    new DigestConfidence(
                        "scored", 0.62, 0.55, 0.69, "moderately-reliable", "stable", -0.03, 48.0, "robust", 40, 6, 2,
                        days35, days14, At("2026-09-28T12:05:00Z"), "current"),
                    new DigestFlaky(
                        "flaky", "timing", "major", At("2026-09-20T08:00:00Z"), At("2026-09-28T12:05:00Z"),
                        days14, At("2026-09-28T12:05:00Z"), "current"),
                    new DigestCanonical(10, 3, 0, null, "passed", At("2026-09-28T12:00:00Z"), new DigestWindow("runs", 10, null, At("2026-09-25T08:00:00Z"))),
                    new DigestTransition("flakiness-introduced", null, "major", At("2026-09-20T08:00:00Z")),
                    [
                        new DigestSignature(
                            Hash, "InvalidOperationException", true, 36, 34, 34, 2, 6,
                            At("2026-09-11T09:14:00Z"), At("2026-09-28T12:00:00Z"), ["c0ffee"], days35)
                    ])
            ],
            [new DigestSession(SessionKey, "complete", true, true)]);

        // Field by field, so a member the wire names differently from the record reads as missing.
        Assert.Equivalent(expected, response, strict: true);
    }

    [Fact]
    public async Task ARequestedSignatureCloudHasNotSeenIsAnsweredWithZeroRuns()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        DigestResponse? response = await client.GetDigestAsync(Project, Request, CancellationToken.None);

        DigestSignature signature = Assert.Single(Assert.Single(response!.Tests).Signatures!);
        Assert.Equal((Hash, true, 0), (signature.ErrorMessageHash, signature.Requested, signature.Runs));
        Assert.Equal("unknown", Assert.Single(response.Sessions!).State);
    }

    [Fact]
    public async Task ATestTheProjectDoesNotKnowIsAnsweredAsUnknown()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        DigestResponse? response = await client.GetDigestAsync(
            Project, new DigestRequest([new DigestRequestTest("fp-unknown", [])], []), CancellationToken.None);

        TestDigest digest = Assert.Single(response!.Tests);
        Assert.Equal(("fp-unknown", "unknown", null), (digest.Fingerprint, digest.State, digest.Confidence));
    }

    [Fact]
    public async Task TheRunHistoryIsAGetWithItsLimit()
    {
        _host.Cloud.AddRun(Project, Fingerprint, run =>
        {
            run["sessionKey"] = SessionKey;
            run["attempts"] = 2;
            run["firstAttemptOutcome"] = "passed";
            run["pullRequestNumber"] = 42;
            run["errorMessageHash"] = Hash;
        });
        _host.Cloud.AddRun(Project, Fingerprint, run => run["outcome"] = "passed");
        using ICloudApiClient client = await ApiKeyClientAsync();

        RunsResponse? history = await client.GetRunsAsync(Project, Fingerprint, 1, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.GatewayRequests);
        Assert.Equal(("GET", "/gw/v1/projects/shop-tests/tests/fp-checkout-0001/runs", "?limit=1"), (request.Method, request.Path, request.Query));
        Assert.Equivalent(
            new RunsResponse(
                365,
                true,
                [
                    new CloudRun(
                        SessionKey, At("2026-09-28T12:00:00Z"), "failed", 2, "passed", 812, "canonical", true, "gh-runner-17",
                        "main", "0053b0ef0053b0ef0053b0ef0053b0ef0053b0ef", 42, "InvalidOperationException", Hash)
                ]),
            history,
            strict: true);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"tests\":[{\"state\":\"ok\"}]}")]
    [InlineData("{\"tests\":[null]}")]
    [InlineData("{\"tests\":[],\"sessions\":[{\"sessionKey\":\"k\"}]}")]
    public async Task ADigestThatCannotBeReadIsAnInvalidResponse(string body)
    {
        _host.Cloud.SetGatewayBody(DigestPath, body);
        using ICloudApiClient client = await ApiKeyClientAsync();

        await Assert.ThrowsAsync<CloudResponseException>(() => client.GetDigestAsync(Project, Request, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"hasMore\":false}")]
    [InlineData("{\"runs\":[null]}")]
    public async Task ARunHistoryThatCannotBeReadIsAnInvalidResponse(string body)
    {
        _host.Cloud.SetGatewayBody("/gw/v1/projects/shop-tests/tests/fp-checkout-0001/runs", body);
        using ICloudApiClient client = await ApiKeyClientAsync();

        await Assert.ThrowsAsync<CloudResponseException>(() => client.GetRunsAsync(Project, Fingerprint, 50, CancellationToken.None));
    }

    [Fact]
    public async Task EveryRequestCarriesTheKeyTheUserAgentAndNoBearer()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        await client.GetDigestAsync(Project, Request, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.GatewayRequests);
        Assert.Equal(ApiKey, request.Headers["X-API-Key"]);
        Assert.False(request.Headers.ContainsKey("Authorization"));
        Assert.StartsWith("xping-cli/", request.Headers["User-Agent"], StringComparison.Ordinal);
        Assert.Equal("application/json", request.Headers["Accept"]);
    }

    [Fact]
    public async Task NotFoundIsNoProjectOrNoTest()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        Assert.Null(await client.GetDigestAsync("unknown-project", Request, CancellationToken.None));
        Assert.Null(await client.GetRunsAsync(Project, "fp-unknown", 50, CancellationToken.None));
    }

    [Fact]
    public async Task ProjectsAreListedWithTheirNames()
    {
        _host.Cloud.AddProject("api-tests", displayName: "Api.Tests");
        using ICloudApiClient client = await ApiKeyClientAsync();

        PagedResult<ProjectSummary> first = await client.ListProjectsAsync(1, 1, CancellationToken.None);
        PagedResult<ProjectSummary> second = await client.ListProjectsAsync(2, 1, CancellationToken.None);

        Assert.Equal([new ProjectSummary(Project, "Shop.Tests", "shop")], first.Items);
        Assert.True(first.HasNextPage);
        Assert.Equal("Api.Tests", Assert.Single(second.Items).DisplayName);
        Assert.False(second.HasNextPage);
        Assert.Equal("?pageNumber=2&pageSize=1", _host.Cloud.GatewayRequests[1].Query);
    }

    [Fact]
    public async Task RouteValuesAreEscaped()
    {
        _host.Cloud.AddTest("a b", "fp/1?x");
        using ICloudApiClient client = await ApiKeyClientAsync();

        RunsResponse? history = await client.GetRunsAsync("a b", "fp/1?x", 50, CancellationToken.None);
        DigestResponse? digest = await client.GetDigestAsync("a b", new DigestRequest([new DigestRequestTest("fp/1?x", [])], []), CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal("ok", Assert.Single(digest!.Tests).State);
        Assert.Equal(
            ["/gw/v1/projects/a%20b/tests/fp%2F1%3Fx/runs", "/gw/v1/projects/a%20b/tests/digest"],
            _host.Cloud.GatewayRequests.Select(r => r.Path));
    }

    [Theory]
    [InlineData(true, "Error.ApiKey.InsufficientScope", "scope")]
    [InlineData(false, "Error.ApiKey.FeatureNotAvailable", "plan")]
    public async Task AKeyThatCannotReadIsToldToSignInInstead(bool uploadOnly, string title, string reason)
    {
        _host.Cloud.AddApiKey("limited-key-0123456789", uploadOnly ? ApiKeyScript.UploadOnly : ApiKeyScript.NoApiAccess);
        using ICloudApiClient client = await ApiKeyClientAsync("limited-key-0123456789");

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Equal(403, failure.StatusCode);
        Assert.Equal(title, failure.Title);
        Assert.Contains(reason, failure.Message, StringComparison.Ordinal);
        Assert.Contains("xping login", failure.Message, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task ARefusedKeyIsNotRetriedAndKeepsItsTitle()
    {
        using ICloudApiClient client = await ApiKeyClientAsync("unknown-key-0123456789");

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Equal(401, failure.StatusCode);
        Assert.Equal("Error.ApiKey.Invalid", failure.Title);
        Assert.DoesNotContain("unknown-key-0123456789", failure.Message, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task AmbiguousCredentialsAreReportedAsACliBug()
    {
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.BadRequest, "Error.Authentication.AmbiguousCredentials");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.StartsWith("CLI bug", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpgradeRequiredIsAVersionMismatch()
    {
        _host.Cloud.FailGateway(DigestPath, 1, (HttpStatusCode)426, "Error.UpgradeRequired");
        using ICloudApiClient client = await ApiKeyClientAsync();

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, failure.ExitCode);
    }

    [Fact]
    public async Task AnyOtherRefusalCarriesTitleAndDetail()
    {
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.Conflict, "Error.Something");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Equal("Xping Cloud refused the request (Error.Something): Injected fault.", failure.Message);
    }

    [Fact]
    public async Task MissingCredentialsWithASignInAsksForALoginWithoutDeletingIt()
    {
        _host.SignIn();
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.Unauthorized, "Error.Authentication.MissingCredentials");
        using ICloudApiClient client = await SignedInClientAsync();

        await Assert.ThrowsAsync<LoginRequiredException>(() => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.NotNull(_host.StoredRecord());
    }

    [Fact]
    public async Task AFirst401WithoutAnInvalidTokenChallengeKeepsTheSignIn()
    {
        _host.SignIn();

        // A proxy's 401, or a challenge stripped on the way: the handler has nothing to refresh on.
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.Unauthorized, "Error.AccessToken.Expired");
        using ICloudApiClient client = await SignedInClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Equal(401, failure.StatusCode);
        Assert.NotNull(_host.StoredRecord());
        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task AProjectListWithoutItemsIsAnIncompleteAnswer()
    {
        _host.Cloud.SetGatewayBody("/gw/v1/projects", "{\"totalCount\":0,\"pageNumber\":1,\"pageSize\":50}");
        using ICloudApiClient client = await ApiKeyClientAsync();

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => client.ListProjectsAsync(1, 50, CancellationToken.None));

        Assert.Contains("incomplete project list", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullAndIdlessProjectsAreLeftOutOfTheList()
    {
        _host.Cloud.SetGatewayBody(
            "/gw/v1/projects",
            "{\"items\":[null,{\"slug\":\"no-id\"},{\"id\":\"api-tests\"}],\"totalCount\":3,\"pageNumber\":1,\"pageSize\":50}");
        using ICloudApiClient client = await ApiKeyClientAsync();

        PagedResult<ProjectSummary> page = await client.ListProjectsAsync(1, 50, CancellationToken.None);

        Assert.Equal("api-tests", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task ASecond401AfterTheRefreshDeletesTheSignIn()
    {
        _host.SignIn();

        // The refresh works, but the DataGateway refuses the new token as well.
        _host.Cloud.RefuseAccessTokens = true;
        using ICloudApiClient client = await SignedInClientAsync();

        await Assert.ThrowsAsync<LoginRequiredException>(() => client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.Null(_host.StoredRecord());
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count);

        // The retry after the refresh is the same POST, body included.
        Assert.Equal(_host.Cloud.GatewayRequests[0].Body, _host.Cloud.GatewayRequests[1].Body);
        Assert.NotEmpty(_host.Cloud.GatewayRequests[1].Body);
        Assert.Single(_host.Cloud.RequestsTo("/connect/token"), r => r.Form["grant_type"] == "refresh_token");
    }

    [Fact]
    public async Task AnApiKeyClientLearnsTheDataGatewayFromDiscovery()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        await client.GetDigestAsync(Project, Request, CancellationToken.None);

        Assert.Single(_host.Cloud.RequestsTo("/.well-known/openid-configuration"));
        Assert.Equal(DigestPath, Assert.Single(_host.Cloud.GatewayRequests).Path);
    }

    private async Task<ICloudApiClient> ApiKeyClientAsync(string key = ApiKey)
    {
        ResolvedCredential credential = CredentialResolver.FromApiKey(
            new ConfiguredValue(key, ConfigurationSource.Environment, "XPING_APIKEY"), shadowedLogin: false, [], []);

        return await Factory.CreateAsync(credential, _host.Cloud.CloudUrl, _budget, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<ICloudApiClient> SignedInClientAsync()
    {
        ResolvedCredential credential = await _host.ResolveAsync(_services).ConfigureAwait(false);
        Assert.Equal(CredentialSource.StoredLogin, credential.Source);
        return await Factory.CreateAsync(credential, _host.Cloud.CloudUrl, _budget, CancellationToken.None).ConfigureAwait(false);
    }

    private CloudApiClientFactory Factory => _services.GetRequiredService<CloudApiClientFactory>();

    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
