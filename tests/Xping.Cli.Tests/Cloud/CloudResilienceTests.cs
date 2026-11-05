/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Cloud;
using Xping.Cli.Cloud.Digest;
using Xping.Cli.Configuration;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The one retry of cloud-data-in-cli-cli-spec §3.2 (Amendment 1B), through a real client against
/// the fake DataGateway.
/// </summary>
public sealed class CloudResilienceTests : IAsyncDisposable
{
    private const string ApiKey = "team-read-key-0123456789";
    private const string Project = "shop-tests";
    private const string DigestPath = "/gw/v1/projects/shop-tests/tests/digest";

    private static readonly DigestRequest Request = new([new DigestRequestTest("fp-checkout-0001", [])], []);

    private readonly CliFlowHost _host = new();
    private readonly ServiceProvider _services;
    private readonly CloudBudget _budget;

    public CloudResilienceTests()
    {
        _services = _host.BuildServices();
        _host.Cloud.AddApiKey(ApiKey);
        _host.Cloud.AddTest(Project, "fp-checkout-0001");
        _budget = new CloudBudget(_host.Time, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        _budget.Dispose();
        await _services.DisposeAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AServerErrorIsRetriedOnceAfterTheFixedDelay(HttpStatusCode status)
    {
        _host.Cloud.FailGateway(DigestPath, 1, status, "Error.Unavailable");
        using ICloudApiClient client = await ClientAsync();
        DateTimeOffset start = _host.Time.GetUtcNow();

        DigestResponse? response = await _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None));

        Assert.NotNull(response);
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count);
        Assert.Equal(CloudResilience.RetryDelay, _host.Time.GetUtcNow() - start);
    }

    [Fact]
    public async Task ASecondServerErrorEndsTheReadAsAServerFailure()
    {
        _host.Cloud.FailGateway(DigestPath, 2, HttpStatusCode.ServiceUnavailable, "Error.Unavailable");
        using ICloudApiClient client = await ClientAsync();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None)));

        Assert.Equal(503, failure.StatusCode);
        Assert.Contains("HTTP 503", failure.Message, StringComparison.Ordinal);
        Assert.Equal(2, _host.Cloud.GatewayRequests.Count);
    }

    [Fact]
    public async Task NoRetryIsMadeWithLessThanASecondOfTheBudgetLeft()
    {
        _host.Time.Advance(CloudBudget.Total - CloudResilience.MinimumForRetry + TimeSpan.FromMilliseconds(1));
        _host.Cloud.FailGateway(DigestPath, 1, HttpStatusCode.ServiceUnavailable, "Error.Unavailable");
        using ICloudApiClient client = await ClientAsync();

        await Assert.ThrowsAsync<CloudApiException>(
            () => _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None)));

        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task RateLimitingIsNeitherRetriedNorWaitedFor(int? retryAfter)
    {
        _host.Cloud.FailGateway(DigestPath, 2, HttpStatusCode.TooManyRequests, "Error.ApiKey.RateLimitExceeded", retryAfter);
        using ICloudApiClient client = await ClientAsync();
        DateTimeOffset start = _host.Time.GetUtcNow();

        CloudApiException failure = await Assert.ThrowsAsync<CloudApiException>(
            () => _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None)));

        Assert.Equal(429, failure.StatusCode);
        Assert.Single(_host.Cloud.GatewayRequests);
        Assert.Equal(start, _host.Time.GetUtcNow());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task AClientErrorIsNotRetried(HttpStatusCode status)
    {
        _host.Cloud.FailGateway(DigestPath, 2, status, "Error.Something");
        using ICloudApiClient client = await ClientAsync();

        await Assert.ThrowsAsync<CloudApiException>(
            () => _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None)));

        Assert.Single(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task ALostConnectionIsNotRetried()
    {
        _host.Cloud.Drop(DigestPath, 2);
        using ICloudApiClient client = await ClientAsync();

        await Assert.ThrowsAsync<AuthFailureException>(
            () => _host.DriveAsync(client.GetDigestAsync(Project, Request, CancellationToken.None)));

        Assert.Single(_host.Cloud.GatewayRequests);
    }

    private async Task<ICloudApiClient> ClientAsync()
    {
        ResolvedCredential credential = CredentialResolver.FromApiKey(
            new ConfiguredValue(ApiKey, ConfigurationSource.Environment, "XPING_APIKEY"), shadowedLogin: false, [], []);

        ICloudApiClient client = await _host.DriveAsync(
            _services.GetRequiredService<CloudApiClientFactory>().CreateAsync(credential, _host.Cloud.CloudUrl, _budget, CancellationToken.None)).ConfigureAwait(false);

        // Discovery's own waits are not part of what these tests measure.
        _host.Time.DiscardCreatedTimers();
        return client;
    }
}
