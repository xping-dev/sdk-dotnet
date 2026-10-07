/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud;
using Xping.Cli.Configuration;

namespace Xping.Cli.Tests.Cloud;

public sealed class CloudApiClientTests : IAsyncDisposable
{
    private const string ApiKey = "team-read-key-0123456789";
    private const string Project = "shop-tests";
    private const string Fingerprint = "fp-checkout-0001";
    private const string TestPath = "/gw/v1/projects/shop-tests/tests/fp-checkout-0001";

    private readonly CliFlowHost _host = new();
    private readonly ServiceProvider _services;

    public CloudApiClientTests()
    {
        _services = _host.BuildServices();
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddProject(Project, displayName: "Shop.Tests", slug: "shop");
        _host.Cloud.AddTest(Project, Fingerprint);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task ATestIsReadIntoTheCloudView()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudTest? test = await client.GetTestAsync(Project, Fingerprint, CancellationToken.None);

        Assert.Equal(
            new CloudTest(Fingerprint, 0.62, "ModeratelyReliable", "Robust", 812, true, "Stable", -0.03,
                new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)),
            test);
    }

    [Fact]
    public async Task EveryRequestCarriesTheKeyTheUserAgentAndNoBearer()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        await client.GetTestAsync(Project, Fingerprint, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.GatewayRequests);
        Assert.Equal(ApiKey, request.Headers["X-API-Key"]);
        Assert.False(request.Headers.ContainsKey("Authorization"));
        Assert.StartsWith("xping-cli/", request.Headers["User-Agent"], StringComparison.Ordinal);
        Assert.Equal("application/json", request.Headers["Accept"]);
    }

    [Fact]
    public async Task NotFoundIsNoData()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        Assert.Null(await client.GetTestAsync(Project, "fp-unknown", CancellationToken.None));
        Assert.Null(await client.GetProjectAsync("unknown-project", CancellationToken.None));
    }

    [Fact]
    public async Task ProjectsAreListedAndReadWithTheirNames()
    {
        _host.Cloud.AddProject("api-tests", displayName: "Api.Tests");
        using ICloudApiClient client = await ApiKeyClientAsync();

        PagedResult<ProjectSummary> first = await client.ListProjectsAsync(1, 1, CancellationToken.None);
        PagedResult<ProjectSummary> second = await client.ListProjectsAsync(2, 1, CancellationToken.None);
        ProjectSummary? project = await client.GetProjectAsync(Project, CancellationToken.None);

        Assert.Equal([new ProjectSummary(Project, "Shop.Tests", "shop")], first.Items);
        Assert.True(first.HasNextPage);
        Assert.Equal("Api.Tests", Assert.Single(second.Items).DisplayName);
        Assert.False(second.HasNextPage);
        Assert.Equal(new ProjectSummary(Project, "Shop.Tests", "shop"), project);
        Assert.Equal("?pageNumber=2&pageSize=1", _host.Cloud.GatewayRequests[1].Query);
    }

    [Fact]
    public async Task RouteValuesAreEscaped()
    {
        _host.Cloud.AddTest("a b", "fp/1?x");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudTest? test = await client.GetTestAsync("a b", "fp/1?x", CancellationToken.None);

        Assert.Equal("fp/1?x", test?.TestFingerprint);
        Assert.Equal("/gw/v1/projects/a%20b/tests/fp%2F1%3Fx", Assert.Single(_host.Cloud.GatewayRequests).Path);
    }

    [Fact]
    public async Task ServerErrorsAreRetriedThreeTimesThenCloudIsUnreachable()
    {
        _host.Cloud.FailGateway(TestPath, 4, HttpStatusCode.ServiceUnavailable, "Error.Unavailable");
        using ICloudApiClient client = await ApiKeyClientAsync();

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => DriveAsync(client.GetTestAsync(Project, Fingerprint, CancellationToken.None)));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains(_host.Cloud.GatewayUri, failure.Message, StringComparison.Ordinal);
        Assert.Contains("HTTP 503", failure.Message, StringComparison.Ordinal);
        Assert.Equal(4, _host.Cloud.GatewayRequests.Count);
    }

    [Fact]
    public async Task AServerErrorThatPassesIsRetried()
    {
        _host.Cloud.FailGateway(TestPath, 1, HttpStatusCode.InternalServerError, "Error.Internal");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudTest? test = await DriveAsync(client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.NotNull(test);
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count);
    }

    [Fact]
    public async Task RateLimitingWaitsRetryAfterAndRetriesOnce()
    {
        _host.Cloud.FailGateway(TestPath, 1, HttpStatusCode.TooManyRequests, "Error.ApiKey.RateLimitExceeded", retryAfter: 3);
        using ICloudApiClient client = await ApiKeyClientAsync();
        DateTimeOffset start = _host.Time.GetUtcNow();

        CloudTest? test = await DriveAsync(client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.NotNull(test);
        Assert.Equal(TimeSpan.FromSeconds(3), _host.Time.GetUtcNow() - start);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(null, 1)]
    [InlineData(11, 1)]
    public async Task RateLimitingIsRetriedAtMostOnceAndOnlyWithAShortRetryAfter(int? retryAfter, int requests)
    {
        _host.Cloud.FailGateway(TestPath, 2, HttpStatusCode.TooManyRequests, "Error.ApiKey.RateLimitExceeded", retryAfter);
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => DriveAsync(client.GetTestAsync(Project, Fingerprint, CancellationToken.None)));

        Assert.Equal(429, failure.StatusCode);
        Assert.Equal(requests, _host.Cloud.GatewayRequests.Count);
    }

    [Theory]
    [InlineData(true, "Error.ApiKey.InsufficientScope", "scope")]
    [InlineData(false, "Error.ApiKey.FeatureNotAvailable", "plan")]
    public async Task AKeyThatCannotReadIsToldToSignInInstead(bool uploadOnly, string title, string reason)
    {
        _host.Cloud.AddApiKey("limited-key-0123456789", uploadOnly ? ApiKeyScript.UploadOnly : ApiKeyScript.NoApiAccess);
        using ICloudApiClient client = await ApiKeyClientAsync("limited-key-0123456789");

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

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
            () => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.Equal(401, failure.StatusCode);
        Assert.Equal("Error.ApiKey.Invalid", failure.Title);
        Assert.DoesNotContain("unknown-key-0123456789", failure.Message, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task AmbiguousCredentialsAreReportedAsACliBug()
    {
        _host.Cloud.FailGateway(TestPath, 1, HttpStatusCode.BadRequest, "Error.Authentication.AmbiguousCredentials");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.StartsWith("CLI bug", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpgradeRequiredIsAVersionMismatch()
    {
        _host.Cloud.FailGateway(TestPath, 1, (HttpStatusCode)426, "Error.UpgradeRequired");
        using ICloudApiClient client = await ApiKeyClientAsync();

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, failure.ExitCode);
    }

    [Fact]
    public async Task AnyOtherRefusalCarriesTitleAndDetail()
    {
        _host.Cloud.FailGateway(TestPath, 1, HttpStatusCode.Conflict, "Error.Something");
        using ICloudApiClient client = await ApiKeyClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.Equal("Xping Cloud refused the request (Error.Something): Injected fault.", failure.Message);
    }

    [Fact]
    public async Task MissingCredentialsWithASignInAsksForALoginWithoutDeletingIt()
    {
        _host.SignIn();
        _host.Cloud.FailGateway(TestPath, 1, HttpStatusCode.Unauthorized, "Error.Authentication.MissingCredentials");
        using ICloudApiClient client = await SignedInClientAsync();

        await Assert.ThrowsAsync<LoginRequiredException>(() => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.NotNull(_host.StoredRecord());
    }

    [Fact]
    public async Task ASecond401AfterTheRefreshDeletesTheSignIn()
    {
        _host.SignIn();

        // The refresh works, but the DataGateway refuses the new token as well.
        _host.Cloud.RefuseAccessTokens = true;
        using ICloudApiClient client = await SignedInClientAsync();

        await Assert.ThrowsAsync<LoginRequiredException>(() => client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.Null(_host.StoredRecord());
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count);
        Assert.Single(_host.Cloud.RequestsTo("/connect/token"), r => r.Form["grant_type"] == "refresh_token");
    }

    [Fact]
    public async Task AnApiKeyClientLearnsTheDataGatewayFromDiscovery()
    {
        using ICloudApiClient client = await ApiKeyClientAsync();

        await client.GetProjectAsync(Project, CancellationToken.None);

        Assert.Single(_host.Cloud.RequestsTo("/.well-known/openid-configuration"));
        Assert.Equal("/gw/v1/projects/shop-tests", Assert.Single(_host.Cloud.GatewayRequests).Path);
    }

    private async Task<ICloudApiClient> ApiKeyClientAsync(string key = ApiKey)
    {
        ResolvedCredential credential = CredentialResolver.FromApiKey(
            new ConfiguredValue(key, ConfigurationSource.Environment, "XPING_APIKEY"), shadowedLogin: false, [], []);

        return await Factory.CreateAsync(credential, _host.Cloud.CloudUrl, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<ICloudApiClient> SignedInClientAsync()
    {
        ResolvedCredential credential = await _host.ResolveAsync(_services).ConfigureAwait(false);
        Assert.Equal(CredentialSource.StoredLogin, credential.Source);
        return await Factory.CreateAsync(credential, _host.Cloud.CloudUrl, CancellationToken.None).ConfigureAwait(false);
    }

    private CloudApiClientFactory Factory => _services.GetRequiredService<CloudApiClientFactory>();

    private Task<T> DriveAsync<T>(Task<T> task) => _host.DriveAsync(task);
}
