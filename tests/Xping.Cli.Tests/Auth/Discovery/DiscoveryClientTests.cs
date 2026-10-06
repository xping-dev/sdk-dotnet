/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Configuration;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth.Discovery;

public sealed class DiscoveryClientTests : IAsyncLifetime, IAsyncDisposable
{
    private const string Discovery = "/.well-known/openid-configuration";

    private AuthTestHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task TheEndpointsAreTakenFromTheDocumentAsPublished()
    {
        _host.Cloud.EditDiscovery = d => d["token_endpoint"] = _host.Cloud.CloudUrl + "/elsewhere/token";

        DiscoveryDocument document = await _host.DiscoverAsync();

        Assert.Equal(_host.Cloud.CloudUrl, document.CloudUrl);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/elsewhere/token"), document.TokenEndpoint);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/connect/authorize"), document.AuthorizationEndpoint);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/connect/revoke"), document.RevocationEndpoint);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/connect/device"), document.DeviceAuthorizationEndpoint);
        Assert.Equal(_host.Cloud.CloudUrl + "/gw", document.DataGatewayUri);
    }

    [Fact]
    public async Task TheRequestIdentifiesTheCli()
    {
        await _host.DiscoverAsync();

        RecordedRequest request = Assert.Single(_host.Cloud.RequestsTo(Discovery));
        Assert.Equal("GET", request.Method);
        Assert.Matches(@"^xping-cli/1\.0\.0 \((windows|macos|linux|other)\)$", request.Headers["User-Agent"]);
        Assert.Equal("application/json", request.Headers["Accept"]);
    }

    [Fact]
    public async Task UnknownMembersAreIgnored()
    {
        _host.Cloud.EditDiscovery = d =>
        {
            d["xping_future_field"] = new JsonObject { ["nested"] = true };
            d["grant_types_supported"] = new JsonArray("authorization_code");
        };

        DiscoveryDocument document = await _host.DiscoverAsync();

        Assert.Equal(_host.Cloud.CloudUrl + "/gw", document.DataGatewayUri);
    }

    [Theory]
    [InlineData("https://gateway.example.com/", "https://gateway.example.com")]
    [InlineData("HTTPS://Gateway.Example.com:443/api/", "https://gateway.example.com/api")]
    [InlineData("http://localhost:5100", "http://localhost:5100")]
    public async Task TheGatewayUriIsNormalized(string published, string expected)
    {
        _host.Cloud.EditDiscovery = d => d["xping_data_gateway_uri"] = published;

        DiscoveryDocument document = await _host.DiscoverAsync();

        Assert.Equal(expected, document.DataGatewayUri);
    }

    // Contract OQ-17: OpenIddict writes the issuer with one trailing slash, and nothing else matches.
    [Theory]
    [InlineData("{0}")]
    [InlineData("{0}//")]
    [InlineData("{0}/portal/")]
    [InlineData("http://localhost:{1}/")]
    [InlineData("https://app.xping.io/")]
    [InlineData(null)]
    public async Task AnIssuerOtherThanTheCloudUrlWithOneSlashFailsClosed(string? issuerFormat)
    {
        _host.Cloud.EditDiscovery = d => d["issuer"] = issuerFormat is null
            ? null
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, issuerFormat, _host.Cloud.CloudUrl, _host.Cloud.Port);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Equal(AuthErrorCodes.CloudUnreachable, failure.ErrorCode);
        Assert.Contains($"names an issuer other than {_host.Cloud.CloudUrl}/", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/gw")]
    [InlineData("http://gateway.example.com")]
    [InlineData("ftp://gateway.example.com")]
    [InlineData("https://user:pass@gateway.example.com")]
    [InlineData("https://gateway.example.com/?a=b")]
    [InlineData("https://gateway.example.com/#a")]
    public async Task AnUnusableGatewayUriFailsClosed(string? gateway)
    {
        _host.Cloud.EditDiscovery = d =>
        {
            if (gateway is null)
                d.Remove("xping_data_gateway_uri");
            else
                d["xping_data_gateway_uri"] = gateway;
        };

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("has no usable xping_data_gateway_uri", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, "the server reports 2. Upgrade the CLI.")]
    [InlineData(0, "the server reports 0. The server is older than this CLI.")]
    [InlineData(null, "the server reports none. The server is older than this CLI.")]
    public async Task AnotherContractVersionStopsTheCli(int? version, string expected)
    {
        _host.Cloud.EditDiscovery = d =>
        {
            if (version is null)
                d.Remove("xping_contract_version");
            else
                d["xping_contract_version"] = version;
        };

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, failure.ExitCode);
        Assert.Equal(AuthErrorCodes.VersionMismatch, failure.ErrorCode);
        Assert.Equal($"This CLI implements Cloud contract version 1; {expected}", failure.Message);
    }

    [Fact]
    public async Task AContractVersionThatIsNotAnIntegerFailsClosed()
    {
        _host.Cloud.EditDiscovery = d => d["xping_contract_version"] = "1";

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0", "0.9.9")]
    [InlineData("1.0.0", "1.0.0-rc.1")]
    [InlineData("1.3.0-beta.2", "1.3.0-beta.1")]
    public async Task ACliAtOrAboveTheMinimumVersionProceeds(string cli, string minimum)
    {
        await UseCliVersionAsync(cli);
        _host.Cloud.EditDiscovery = d => d["xping_cli_min_version"] = minimum;

        await _host.DiscoverAsync();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("0.9.0", "0.10.0")]
    [InlineData("not-a-version", "0.1.0")]
    public async Task ACliBelowTheMinimumVersionIsAskedToUpgrade(string cli, string minimum)
    {
        await UseCliVersionAsync(cli);
        _host.Cloud.EditDiscovery = d => d["xping_cli_min_version"] = minimum;

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, failure.ExitCode);
        Assert.Equal(
            $"Please upgrade the xping CLI: this server requires {minimum} or newer, you have {cli}.",
            failure.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.0")]
    [InlineData("latest")]
    public async Task AMinimumVersionThatIsNotSemVerFailsClosed(string? minimum)
    {
        _host.Cloud.EditDiscovery = d => d["xping_cli_min_version"] = minimum;

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("xping_cli_min_version", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("authorization_endpoint", null)]
    [InlineData("token_endpoint", "/connect/token")]
    [InlineData("revocation_endpoint", "http://app.example.com/connect/revoke")]
    [InlineData("device_authorization_endpoint", "https://user:pass@app.example.com/connect/device")]
    public async Task AMissingOrUnusableEndpointFailsClosed(string name, string? value)
    {
        _host.Cloud.EditDiscovery = d => d[name] = value;

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains($"has no usable {name}", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFirstFailingCheckIsTheOneReported()
    {
        _host.Cloud.EditDiscovery = d =>
        {
            d["issuer"] = "https://elsewhere.example.com/";
            d["xping_contract_version"] = 9;
        };

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("issuer", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADocumentThatIsNotJsonFailsClosed()
    {
        _host.Cloud.Fail(Discovery, 1, HttpStatusCode.OK, error: null);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("not valid JSON", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Found)]
    public async Task AnAnswerOtherThan200FailsClosed(HttpStatusCode status)
    {
        _host.Cloud.Fail(Discovery, 1, status, error: null);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Equal(
            $"Could not reach Xping Cloud at {_host.Cloud.CloudUrl}: discovery answered HTTP {(int)status}.",
            failure.Message);
    }

    [Fact]
    public async Task AServerThatIsNotListeningIsReportedAsConnectionRefused()
    {
        string cloudUrl = $"http://127.0.0.1:{UnusedPort()}";

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => _host.Discovery.GetAsync(cloudUrl, useCache: false, CancellationToken.None));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Equal($"Could not reach Xping Cloud at {cloudUrl}: connection refused.", failure.Message);
    }

    [Fact]
    public async Task ACachedDocumentAnswersForADayWithoutARequest()
    {
        await _host.DiscoverAsync();

        _host.Time.Advance(DiscoveryCache.MaxAge - TimeSpan.FromSeconds(1));
        DiscoveryDocument cached = await _host.DiscoverAsync(useCache: true);

        Assert.Single(_host.Cloud.RequestsTo(Discovery));
        Assert.Equal(_host.Cloud.CloudUrl + "/gw", cached.DataGatewayUri);
    }

    [Fact]
    public async Task ADocumentADayOldIsFetchedAgain()
    {
        await _host.DiscoverAsync();

        _host.Time.Advance(DiscoveryCache.MaxAge);
        await _host.DiscoverAsync(useCache: true);

        Assert.Equal(2, _host.Cloud.RequestsTo(Discovery).Count);
    }

    [Fact]
    public async Task LoginIgnoresTheCacheAndRefreshesIt()
    {
        await _host.DiscoverAsync();
        _host.Cloud.EditDiscovery = d => d["xping_data_gateway_uri"] = "https://moved.example.com";

        DiscoveryDocument fresh = await _host.DiscoverAsync(useCache: false);
        DiscoveryDocument cached = await _host.DiscoverAsync(useCache: true);

        Assert.Equal(2, _host.Cloud.RequestsTo(Discovery).Count);
        Assert.Equal("https://moved.example.com", fresh.DataGatewayUri);
        Assert.Equal("https://moved.example.com", cached.DataGatewayUri);
    }

    [Fact]
    public async Task ACachedDocumentThisCliNoLongerAcceptsIsFetchedAgain()
    {
        await _host.DiscoverAsync();
        await RewriteCacheAsync(entry => entry["document"]!["xping_contract_version"] = 2);

        DiscoveryDocument document = await _host.DiscoverAsync(useCache: true);

        Assert.Equal(2, _host.Cloud.RequestsTo(Discovery).Count);
        Assert.Equal(_host.Cloud.CloudUrl, document.CloudUrl);
    }

    [Fact]
    public async Task ACachedDocumentFromTheFutureIsNotTrusted()
    {
        await _host.DiscoverAsync();
        await RewriteCacheAsync(entry => entry["fetchedAt"] = _host.Time.GetUtcNow().AddHours(1).ToString("O"));

        await _host.DiscoverAsync(useCache: true);

        Assert.Equal(2, _host.Cloud.RequestsTo(Discovery).Count);
    }

    [Fact]
    public async Task ACorruptCacheEntryIsAMiss()
    {
        await _host.DiscoverAsync();
        await File.WriteAllTextAsync(CachePath(), "{ not json");

        await _host.DiscoverAsync(useCache: true);

        Assert.Equal(2, _host.Cloud.RequestsTo(Discovery).Count);
    }

    [Fact]
    public async Task TheCacheIsKeptPerCloudUrl()
    {
        await _host.DiscoverAsync();
        var other = new FakeCloud(_host.Time);
        await using ConfiguredAsyncDisposable _ = other.ConfigureAwait(false);

        await _host.Discovery.GetAsync(other.CloudUrl, useCache: true, CancellationToken.None);

        Assert.Single(other.RequestsTo(Discovery));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_host.HomeDirectory, "cache", "discovery")).Length);
    }

    [Fact]
    public async Task ARejectedDocumentIsNotCached()
    {
        _host.Cloud.EditDiscovery = d => d["xping_contract_version"] = 2;

        await Assert.ThrowsAsync<AuthFailureException>(() => _host.DiscoverAsync());

        Assert.False(File.Exists(CachePath()));
    }

    [Fact]
    public async Task TheCacheIsReadableOnlyByTheUser()
    {
        // Windows has no file modes; the owner-only ACL is set there instead.
        if (OperatingSystem.IsWindows())
            return;

        await _host.DiscoverAsync();

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(CachePath()));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(_host.HomeDirectory));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(CachePath())!, "*.tmp-*"));
    }

    private string CachePath() =>
        Path.Combine(_host.HomeDirectory, "cache", "discovery", CloudUrl.StorageKey(_host.Cloud.CloudUrl) + ".json");

    private async Task RewriteCacheAsync(Action<JsonNode> edit)
    {
        JsonNode entry = JsonNode.Parse(await File.ReadAllTextAsync(CachePath()).ConfigureAwait(false))!;
        edit(entry);
        await File.WriteAllTextAsync(CachePath(), entry.ToJsonString()).ConfigureAwait(false);
    }

    private async Task UseCliVersionAsync(string version)
    {
        await _host.DisposeAsync().ConfigureAwait(false);
        _host = new AuthTestHost(version);
    }

    private static int UnusedPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
