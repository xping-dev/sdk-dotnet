/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Http;
using Xping.Cli.Auth.Store;
using Xping.Cli.Cloud;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// A real sign-in, then DataGateway reads through the whole pipeline against <see cref="FakeCloud"/>.
/// </summary>
/// <remarks>
/// Phase 6 runs these at the pipeline level, because <c>report</c> does not call Cloud before
/// phase 7, which extends them to run <c>xping report</c> (cli-auth-cli-spec §18.2).
/// </remarks>
public sealed class AuthenticatedPipelineTests : IAsyncDisposable
{
    private const string Project = "shop-tests";
    private const string Fingerprint = "fp-checkout-0001";
    private const string ApiKey = "team-read-key-0123456789";

    private readonly CliFlowHost _host = new();

    public AuthenticatedPipelineTests()
    {
        _host.Cloud.AddProject(Project, displayName: "Shop.Tests");
        _host.Cloud.AddTest(Project, Fingerprint);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Report_Refresh_Rotation()
    {
        CredentialRecord before = _host.SignIn();
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        using Process process = await StartProcessAsync();

        CloudTest? test = await process.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None);

        Assert.NotNull(test);
        RecordedRequest refresh = Assert.Single(_host.Cloud.RequestsTo("/connect/token"), r => r.Form["grant_type"] == "refresh_token");
        Assert.Equal(before.RefreshToken, refresh.Form["refresh_token"]);

        CredentialRecord after = _host.StoredRecord()!;
        Assert.NotEqual(before.RefreshToken, after.RefreshToken);
        Assert.Equal(_host.Time.GetUtcNow() + TimeSpan.FromMinutes(15), after.AccessTokenExpiresAt);
        Assert.Equal("Bearer " + after.AccessToken, Assert.Single(_host.Cloud.GatewayRequests).Headers["Authorization"]);
    }

    [Fact]
    public async Task Report_ReuseDetected()
    {
        CredentialRecord before = _host.SignIn();

        // Another process refreshed with the stored token and never stored the new pair; once the
        // reuse leeway has passed, presenting the old token again is theft to the server.
        using (Process other = await StartProcessAsync())
        {
            DiscoveryDocument discovery = await other.Services.GetRequiredService<DiscoveryClient>()
                .GetAsync(_host.Cloud.CloudUrl, useCache: true, CancellationToken.None);
            await other.Services.GetRequiredService<OAuthClient>().RefreshAsync(discovery, before.RefreshToken, CancellationToken.None);
        }

        _host.Time.Advance(TimeSpan.FromMinutes(16));
        using Process process = await StartProcessAsync();

        await Assert.ThrowsAsync<LoginRequiredException>(
            () => process.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None));

        Assert.True(_host.Cloud.IsSessionRevoked(before.RefreshToken));
        Assert.Null(_host.StoredRecord());
        Assert.Empty(_host.Cloud.GatewayRequests);
    }

    [Fact]
    public async Task Report_TwoProcesses_Refresh()
    {
        CredentialRecord before = _host.SignIn();
        _host.Time.Advance(TimeSpan.FromMinutes(16));
        using Process first = await StartProcessAsync();
        using Process second = await StartProcessAsync();

        CloudTest?[] tests = await _host.DriveAsync(Task.WhenAll(
            Task.Run(() => first.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None)),
            Task.Run(() => second.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None))));

        Assert.All(tests, Assert.NotNull);

        // The lock makes the second process adopt the first one's pair; without it, both present
        // the same token inside the server's leeway. Either way the session survives.
        RecordedRequest[] refreshes = [.. _host.Cloud.RequestsTo("/connect/token").Where(r => r.Form["grant_type"] == "refresh_token")];
        Assert.InRange(refreshes.Length, 1, 2);
        Assert.All(refreshes, r => Assert.Equal(before.RefreshToken, r.Form["refresh_token"]));
        Assert.True(refreshes[^1].ReceivedAt - refreshes[0].ReceivedAt < TimeSpan.FromSeconds(30));

        CredentialRecord stored = _host.StoredRecord()!;
        Assert.False(_host.Cloud.IsSessionRevoked(stored.RefreshToken));
        Assert.True(stored.AccessTokenExpiresAt > _host.Time.GetUtcNow());
    }

    [Fact]
    public async Task Report_BothHeadersNever()
    {
        _host.SignIn();
        _host.Cloud.AddApiKey(ApiKey);
        _host.Environment["XPING_APIKEY"] = ApiKey;

        using (Process process = await StartProcessAsync())
        {
            await process.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None);

            // The fallback a definitive sign-in failure leads to (§8.1) gets a pipeline of its own.
            using ICloudApiClient fallback = await process.Factory.CreateAsync(
                process.Credential.FallbackToApiKey(), _host.Cloud.CloudUrl, CancellationToken.None);
            await fallback.GetTestAsync(Project, Fingerprint, CancellationToken.None);
        }

        using (Process flagged = await StartProcessAsync(apiKeyFlag: ApiKey))
            await flagged.Client.GetTestAsync(Project, Fingerprint, CancellationToken.None);

        Assert.Equal(
            ["Authorization", "X-API-Key", "X-API-Key"],
            _host.Cloud.GatewayRequests.Select(r => Assert.Single(r.Headers.Keys, k => k is "Authorization" or "X-API-Key")));

        // Neither credential ever reaches the Portal (contract §2).
        Assert.DoesNotContain(
            _host.Cloud.Requests.Where(r => !r.Path.StartsWith(FakeCloud.GatewayPrefix, StringComparison.Ordinal)),
            r => r.Headers.ContainsKey("Authorization") || r.Headers.ContainsKey("X-API-Key"));
    }

    // One xping process: its own services, its own credential resolution, its own client.
    private async Task<Process> StartProcessAsync(string? apiKeyFlag = null)
    {
        ServiceProvider services = _host.BuildServices();
        ResolvedCredential credential = await _host.ResolveAsync(services, apiKeyFlag).ConfigureAwait(false);
        CloudApiClientFactory factory = services.GetRequiredService<CloudApiClientFactory>();
        ICloudApiClient client = await factory.CreateAsync(credential, _host.Cloud.CloudUrl, CancellationToken.None).ConfigureAwait(false);

        return new Process(services, credential, factory, client);
    }

    private sealed record Process(
        ServiceProvider Services,
        ResolvedCredential Credential,
        CloudApiClientFactory Factory,
        ICloudApiClient Client) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Services.Dispose();
        }
    }
}
