/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth.Http;
using Xping.Cli.Auth.Store;
using Xping.Cli.Tests.Auth.Store;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth.Http;

public sealed class BearerTokenHandlerTests : IAsyncDisposable
{
    private const string InvalidToken = "Bearer realm=\"xping\", error=\"invalid_token\", error_description=\"The access token has expired.\"";

    private readonly AuthTestHost _host = new();
    private readonly FakeCredentialStore _store = new(CredentialStoreKind.File, "~/.xping/credentials.json");
    private readonly RecordingHandler _inner = new();
    private string _access = string.Empty;
    private TokenRefresher? _refresher;
    private BearerTokenHandler? _handler;

    public async ValueTask DisposeAsync()
    {
        _handler?.Dispose();
        _refresher?.Dispose();
        _inner.Dispose();
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Theory]
    [InlineData("http://elsewhere.invalid/gw/v1/projects")]
    [InlineData("{cloud}/connect/token")]
    [InlineData("{cloud}/gwx/v1/projects")]
    [InlineData("https://127.0.0.1/gw/v1/projects")]
    public async Task TheTokenIsNeverSentOutsideTheDataGateway(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        using HttpMessageInvoker invoker = Invoker();
        using var request = new HttpRequestMessage(HttpMethod.Get, target.Replace("{cloud}", _host.Cloud.CloudUrl, StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.SendAsync(request, CancellationToken.None));

        Assert.Empty(_inner.Requests);
    }

    [Fact]
    public async Task TheBearerTokenIsTheOnlyCredentialSent()
    {
        using HttpMessageInvoker invoker = Invoker();
        using HttpRequestMessage request = Get();
        request.Headers.Add(CredentialHeaders.ApiKey, "leftover-api-key-0123456789");

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        (AuthenticationHeaderValue? authorization, bool hasApiKey) = Assert.Single(_inner.Requests);
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal(_access, authorization?.Parameter);
        Assert.False(hasApiKey);
    }

    [Fact]
    public async Task A401InvalidTokenRefreshesOnceAndRetries()
    {
        _inner.Respond(Unauthorized(InvalidToken), new HttpResponseMessage(HttpStatusCode.OK));
        using HttpMessageInvoker invoker = Invoker();
        using HttpRequestMessage request = Get();

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _inner.Requests.Count);
        Assert.Equal(_access, _inner.Requests[0].Authorization?.Parameter);
        Assert.NotEqual(_access, _inner.Requests[1].Authorization?.Parameter);
        Assert.Single(_host.Cloud.RequestsTo("/connect/token"));
    }

    [Fact]
    public async Task ASecond401IsReturnedUnchanged()
    {
        _inner.Respond(Unauthorized(InvalidToken), Unauthorized(InvalidToken));
        using HttpMessageInvoker invoker = Invoker();
        using HttpRequestMessage request = Get();

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, _inner.Requests.Count);
        Assert.Single(_host.Cloud.RequestsTo("/connect/token"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Bearer realm=\"xping\"")]
    [InlineData(HttpStatusCode.Unauthorized, "Bearer realm=\"xping\", error_description=\"invalid_token\"")]
    [InlineData(HttpStatusCode.Forbidden, "Bearer realm=\"xping\", error=\"insufficient_scope\", scope=\"user:read\"")]
    public async Task OnlyA401InvalidTokenIsRetried(HttpStatusCode status, string challenge)
    {
        var refused = new HttpResponseMessage(status);
        refused.Headers.WwwAuthenticate.ParseAdd(challenge);
        _inner.Respond(refused);
        using HttpMessageInvoker invoker = Invoker();
        using HttpRequestMessage request = Get();

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Single(_inner.Requests);
        Assert.Empty(_host.Cloud.RequestsTo("/connect/token"));
    }

    private HttpRequestMessage Get() => new(HttpMethod.Get, _host.Cloud.GatewayUri + "/v1/projects");

    private static HttpResponseMessage Unauthorized(string challenge)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ParseAdd(challenge);
        return response;
    }

    private HttpMessageInvoker Invoker()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        _access = access;

        var record = new CredentialRecord(
            CredentialRecord.CurrentSchemaVersion,
            _host.Cloud.CloudUrl,
            refresh,
            access,
            _host.Time.GetUtcNow() + TimeSpan.FromMinutes(15),
            FakeCloud.WorkspaceId,
            "01J8SESSION",
            FakeCloud.UserId,
            FakeCloud.Email,
            _host.Cloud.GatewayUri,
            _host.Time.GetUtcNow());
        _store.Add(record);

        _refresher = new TokenRefresher(
            new StoredLogin(record, _store),
            new CredentialStores(_store, [_store], fallbackReason: null),
            _host.Discovery,
            _host.OAuth,
            _host.Services.GetRequiredService<CrossProcessLock>(),
            _host.Time,
            NullLogger<TokenRefresher>.Instance);

        _handler = new BearerTokenHandler(_refresher, NullLogger<BearerTokenHandler>.Instance) { InnerHandler = _inner };
        return new HttpMessageInvoker(_handler, disposeHandler: false);
    }

    /// <summary>
    /// Records the credential headers of each request and answers from a script, then 200.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<(AuthenticationHeaderValue? Authorization, bool HasApiKey)> Requests { get; } = [];

        public void Respond(params HttpResponseMessage[] responses)
        {
            foreach (HttpResponseMessage response in responses)
                _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Headers.Authorization, request.Headers.Contains(CredentialHeaders.ApiKey)));
            return Task.FromResult(_responses.TryDequeue(out HttpResponseMessage? response) ? response : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
