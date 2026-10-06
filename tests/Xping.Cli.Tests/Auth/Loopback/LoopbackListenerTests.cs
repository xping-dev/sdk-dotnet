/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Loopback;

namespace Xping.Cli.Tests.Auth.Loopback;

/// <summary>
/// The real listener on an ephemeral port (cli-auth-cli-spec §4.3–§4.5).
/// </summary>
public sealed class LoopbackListenerTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly Pkce _pkce = Pkce.Create();
    private readonly HttpClient _browser = new();
    private LoopbackListener _listener = null!;

    public Task InitializeAsync()
    {
        _listener = LoopbackListener.Start(NullLogger.Instance);
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _listener.DisposeAsync().ConfigureAwait(false);
        _browser.Dispose();
        _pkce.Dispose();
    }

    [Fact]
    public void TheRedirectUriIsLoopbackByAddress() =>
        Assert.Matches(@"^http://127\.0\.0\.1:\d+/callback$", _listener.RedirectUri);

    [Fact]
    public async Task AGenuineCallbackReturnsTheCodeAndShowsTheSuccessPage()
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);

        using HttpResponseMessage response = await GetAsync($"?code=the-code&state={_pkce.State}").ConfigureAwait(true);
        CallbackResult result = await wait.ConfigureAwait(true);

        Assert.Equal("the-code", Assert.IsType<CallbackResult.Code>(result).Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSafeHeaders(response);

        string page = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        Assert.Contains("Signed in to Xping", page, StringComparison.Ordinal);
        Assert.DoesNotContain("the-code", page, StringComparison.Ordinal);
        Assert.DoesNotContain(_pkce.State, page, StringComparison.Ordinal);
        Assert.DoesNotContain(_listener.RedirectUri, page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/callback/extra")]
    [InlineData("GET", "/favicon.ico")]
    [InlineData("POST", "/callback")]
    public async Task OtherRequestsAre404AndTheListenerKeepsWaiting(string method, string path)
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);
        string root = _listener.RedirectUri[..^"/callback".Length];

        using var request = new HttpRequestMessage(new HttpMethod(method), root + path + $"?code=x&state={_pkce.State}");
        using HttpResponseMessage stray = await _browser.SendAsync(request).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
        Assert.False(wait.IsCompleted);

        using HttpResponseMessage genuine = await GetAsync($"?code=real&state={_pkce.State}").ConfigureAwait(true);
        Assert.Equal("real", Assert.IsType<CallbackResult.Code>(await wait.ConfigureAwait(true)).Value);
    }

    [Theory]
    [InlineData("?code=x&state=wrong")]
    [InlineData("?code=x")]
    [InlineData("?error=access_denied&state=wrong")]
    public async Task AWrongOrMissingStateIs400AndTheListenerKeepsWaiting(string query)
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);

        using HttpResponseMessage bad = await GetAsync(query).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        AssertSafeHeaders(bad);
        Assert.False(wait.IsCompleted);

        using HttpResponseMessage genuine = await GetAsync($"?code=real&state={_pkce.State}").ConfigureAwait(true);
        Assert.IsType<CallbackResult.Code>(await wait.ConfigureAwait(true));
    }

    [Fact]
    public async Task AMatchingStateWithoutCodeOrErrorIs400AndTheListenerKeepsWaiting()
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);

        using HttpResponseMessage empty = await GetAsync($"?state={_pkce.State}").ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.False(wait.IsCompleted);

        using HttpResponseMessage genuine = await GetAsync($"?code=real&state={_pkce.State}").ConfigureAwait(true);
        Assert.IsType<CallbackResult.Code>(await wait.ConfigureAwait(true));
    }

    [Fact]
    public async Task AnErrorRedirectEndsTheWaitAndShowsOnlyTheErrorCode()
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);

        using HttpResponseMessage response = await GetAsync(
            $"?error=access_denied&error_description=%3Cscript%3Ealert(1)%3C%2Fscript%3E&state={_pkce.State}").ConfigureAwait(true);

        var error = Assert.IsType<CallbackResult.Error>(await wait.ConfigureAwait(true));
        Assert.Equal("access_denied", error.Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string page = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        Assert.Contains("access_denied", page, StringComparison.Ordinal);
        Assert.DoesNotContain("script", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnErrorThatIsNotAnErrorCodeIsNotReflected()
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);

        using HttpResponseMessage response = await GetAsync($"?error=Visit%20evil.example&state={_pkce.State}").ConfigureAwait(true);
        await wait.ConfigureAwait(true);

        string page = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        Assert.DoesNotContain("evil", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheListenerStopsAfterItIsDisposed()
    {
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, CancellationToken.None);
        using (await GetAsync($"?code=real&state={_pkce.State}").ConfigureAwait(true))
            await wait.ConfigureAwait(true);

        await _listener.DisposeAsync().ConfigureAwait(true);

        using var client = new TcpClient();
        Assert.ThrowsAny<SocketException>(() => client.Connect(IPAddress.Loopback, new Uri(_listener.RedirectUri).Port));
    }

    [Fact]
    public async Task CancellationEndsTheWait()
    {
        using var cancellation = new CancellationTokenSource();
        Task<CallbackResult> wait = _listener.WaitForCallbackAsync(_pkce, cancellation.Token);

        await cancellation.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnOccupiedPortIsRetriedOnAFreshOne()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int taken = ((IPEndPoint)occupied.LocalEndpoint).Port;
        int probes = 0;

        await using LoopbackListener listener = LoopbackListener.Start(
            NullLogger.Instance,
            address => ++probes < 3 ? taken : FreePort(address));

        Assert.Equal(3, probes);
        Assert.NotEqual(taken, new Uri(listener.RedirectUri).Port);
        Assert.StartsWith("http://127.0.0.1:", listener.RedirectUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IPv6LoopbackIsTriedAfterThreeIPv4Attempts()
    {
        if (!Socket.OSSupportsIPv6)
            return;

        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int taken = ((IPEndPoint)occupied.LocalEndpoint).Port;
        List<IPAddress> probed = [];

        int Probe(IPAddress address)
        {
            probed.Add(address);
            return address.Equals(IPAddress.Loopback) ? taken : FreePort(address);
        }

        // The managed HttpListener of macOS and Linux refuses a bracketed IPv6 prefix, so [::1] binds
        // on Windows only (http.sys); elsewhere the attempt fails and the user is sent to --device.
        if (OperatingSystem.IsWindows())
        {
            await using LoopbackListener listener = LoopbackListener.Start(NullLogger.Instance, Probe);
            Assert.Matches(@"^http://\[::1\]:\d+/callback$", listener.RedirectUri);
        }
        else
        {
            Assert.Throws<AuthFailureException>(() => LoopbackListener.Start(NullLogger.Instance, Probe));
        }

        Assert.Equal([IPAddress.Loopback, IPAddress.Loopback, IPAddress.Loopback, IPAddress.IPv6Loopback], probed);
    }

    [Fact]
    public void WhenNoPortCanBeOpenedTheUserIsSentToTheDeviceFlow()
    {
        var failure = Assert.Throws<AuthFailureException>(() => LoopbackListener.Start(
            NullLogger.Instance,
            _ => throw new SocketException((int)SocketError.AddressFamilyNotSupported)));

        Assert.Equal(AuthExitCodes.LoginFailed, failure.ExitCode);
        Assert.Contains("xping login --device", failure.Message, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> GetAsync(string query) => _browser.GetAsync(new Uri(_listener.RedirectUri + query));

    private static void AssertSafeHeaders(HttpResponseMessage response)
    {
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static int FreePort(IPAddress address)
    {
        using var probe = new TcpListener(address, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
