/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Auth.Store;
using Xping.Cli.Tests.Auth.Store;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth.Http;

public sealed class TokenRefresherTests : IAsyncDisposable
{
    private const string Token = "/connect/token";

    private readonly AuthTestHost _host = new();
    private readonly FakeCredentialStore _store = new(CredentialStoreKind.File, "~/.xping/credentials.json");

    private DateTimeOffset Now => _host.Time.GetUtcNow();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task AFreshTokenIsReturnedWithoutARequest()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromMinutes(15)));

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(access, token);
        Assert.Empty(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task ATokenWithinAMinuteOfExpiryIsRefreshedAndTheNewPairStored()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromSeconds(59)));

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.NotEqual(access, token);
        RecordedRequest request = Assert.Single(_host.Cloud.RequestsTo(Token));
        Assert.Equal("refresh_token", request.Form["grant_type"]);
        Assert.Equal(refresh, request.Form["refresh_token"]);

        CredentialRecord stored = StoredRecord();
        Assert.Equal(token, stored.AccessToken);
        Assert.NotEqual(refresh, stored.RefreshToken);
        Assert.Equal(Now + TimeSpan.FromMinutes(15), stored.AccessTokenExpiresAt);
        Assert.Equal(FakeCloud.WorkspaceId, stored.WorkspaceId);
        Assert.Equal(_host.Cloud.GatewayUri, stored.DataGatewayUri);
    }

    [Fact]
    public async Task ARecordWithoutAnAccessTokenIsRefreshed()
    {
        (_, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access: null, expiresAt: null));

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.StartsWith("eyJ", token, StringComparison.Ordinal);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task ConcurrentCallersShareOneRefresh()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));

        string[] tokens = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => refresher.GetAccessTokenAsync(CancellationToken.None))));

        Assert.Single(_host.Cloud.RequestsTo(Token));
        Assert.Single(tokens.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AForcedRefreshOfATokenAlreadyReplacedMakesNoRequest()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromMinutes(15)));

        string first = await refresher.ForceRefreshAsync(access, CancellationToken.None);
        string second = await refresher.ForceRefreshAsync(access, CancellationToken.None);

        Assert.NotEqual(access, first);
        Assert.Equal(first, second);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task AFresherRecordAnotherProcessStoredIsAdopted()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));

        // Another process refreshed and stored its pair after this one loaded the record.
        (string newerAccess, string newerRefresh) = _host.Cloud.StartSession();
        _store.Add(Record(newerRefresh, newerAccess, Now + TimeSpan.FromMinutes(15)));

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(newerAccess, token);
        Assert.Empty(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task TheRefreshPresentsTheStoredTokenNotTheOneLoadedAtStart()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));

        (_, string rotated) = _host.Cloud.StartSession();
        _store.Add(Record(rotated, access: null, expiresAt: null));

        await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(rotated, Assert.Single(_host.Cloud.RequestsTo(Token)).Form["refresh_token"]);
    }

    [Fact]
    public async Task InvalidGrantWithATokenRotatedMeanwhileRetriesOnceWithTheStoredOne()
    {
        (_, string valid) = _host.Cloud.StartSession();
        CredentialRecord stale = Record("refresh-unknown-0123456789", access: null, expiresAt: null);

        // The store answers the stale record to the read before the refresh, and the rotated one
        // after it: another process, running without the lock, rotated in between.
        var store = new ScriptedStore(stale, Record(valid, access: null, expiresAt: null));
        using TokenRefresher refresher = Refresher(stale, store);

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.StartsWith("eyJ", token, StringComparison.Ordinal);
        Assert.Equal(
            ["refresh-unknown-0123456789", valid],
            _host.Cloud.RequestsTo(Token).Select(r => r.Form["refresh_token"]));
        Assert.Empty(store.Deleted);
    }

    [Fact]
    public async Task InvalidGrantOtherwiseDeletesTheSignInAndAsksForALogin()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        _host.Cloud.RevokeSession(refresh);
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));

        await Assert.ThrowsAsync<LoginRequiredException>(() => refresher.GetAccessTokenAsync(CancellationToken.None));

        Assert.False(_store.Contains(_host.Cloud.CloudUrl));
        Assert.Equal([_host.Cloud.CloudUrl], _store.Deleted);

        // The in-memory sign-in is gone too: no second request.
        await Assert.ThrowsAsync<LoginRequiredException>(() => refresher.GetAccessTokenAsync(CancellationToken.None));
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task ASignInRemovedByAnotherProcessIsNotRefreshed()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));
        await _store.DeleteAsync(_host.Cloud.CloudUrl, CancellationToken.None);

        await Assert.ThrowsAsync<LoginRequiredException>(() => refresher.GetAccessTokenAsync(CancellationToken.None));

        Assert.Empty(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task AStoreWriteFailureKeepsTheNewTokensInMemoryAndWarns()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));
        _store.WriteFails = true;

        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);
        string again = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(token, again);
        Assert.Single(_host.Cloud.RequestsTo(Token));
        string warning = Assert.Single(refresher.Warnings);
        Assert.Contains("Could not write", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerErrorLeavesTheSignInInPlace()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        CredentialRecord record = Record(refresh, access, Now);
        using TokenRefresher refresher = Refresher(record);
        _host.Cloud.Fail(Token, 2, HttpStatusCode.InternalServerError, "server_error");

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => _host.DriveAsync(refresher.GetAccessTokenAsync(CancellationToken.None)));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Equal(record, StoredRecord());
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task AnOAuthErrorOtherThanInvalidGrantLeavesTheSignInInPlace()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));
        _host.Cloud.Fail(Token, 1, HttpStatusCode.Unauthorized, "invalid_client");

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => refresher.GetAccessTokenAsync(CancellationToken.None));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("invalid_client", failure.Message, StringComparison.Ordinal);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task ACancelAfterTheServerRotatedStillStoresTheNewPair()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now));
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _host.Cloud.HoldTokenResponses = answer.Task;
        using var cancel = new CancellationTokenSource();

        Task<string> abandoned = refresher.GetAccessTokenAsync(cancel.Token);
        await WaitUntilAsync(() => _host.Cloud.RequestsTo(Token).Count == 1);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // The server rotated before the cancel; the next caller gets that pair, not a second refresh.
        answer.SetResult();
        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        Assert.Single(_host.Cloud.RequestsTo(Token));
        CredentialRecord stored = StoredRecord();
        Assert.Equal(token, stored.AccessToken);
        Assert.NotEqual(refresh, stored.RefreshToken);
    }

    [Fact]
    public async Task AfterAFailedWriteTheNextRefreshPresentsTheTokenInMemoryNotTheOlderStoredOne()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromMinutes(15)));
        _host.Time.Advance(TimeSpan.FromMinutes(15));
        _store.WriteFails = true;
        await refresher.GetAccessTokenAsync(CancellationToken.None);

        // The store still holds the first pair; past the reuse leeway, presenting it is theft.
        _store.WriteFails = false;
        _host.Time.Advance(TimeSpan.FromMinutes(15));
        string token = await refresher.GetAccessTokenAsync(CancellationToken.None);

        IReadOnlyList<RecordedRequest> refreshes = _host.Cloud.RequestsTo(Token);
        Assert.Equal(2, refreshes.Count);
        Assert.NotEqual(refresh, refreshes[1].Form["refresh_token"]);
        Assert.Equal(token, StoredRecord().AccessToken);
        Assert.False(_host.Cloud.IsSessionRevoked(refresh));
    }

    [Fact]
    public async Task InvalidatingKeepsANewerSignInStoredMeanwhile()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromMinutes(15)));

        // xping login in another terminal.
        _host.Time.Advance(TimeSpan.FromSeconds(1));
        (string newAccess, string newRefresh) = _host.Cloud.StartSession();
        _store.Add(Record(newRefresh, newAccess, Now + TimeSpan.FromMinutes(15)));

        await refresher.InvalidateAsync(CancellationToken.None);

        Assert.Equal(newRefresh, StoredRecord().RefreshToken);
        Assert.Empty(_store.Deleted);
        await Assert.ThrowsAsync<LoginRequiredException>(() => refresher.GetAccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InvalidatingDeletesTheSignInThisProcessUsed()
    {
        (string access, string refresh) = _host.Cloud.StartSession();
        using TokenRefresher refresher = Refresher(Record(refresh, access, Now + TimeSpan.FromMinutes(15)));

        await refresher.InvalidateAsync(CancellationToken.None);

        Assert.False(_store.Contains(_host.Cloud.CloudUrl));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!condition())
            await Task.Delay(10, patience.Token).ConfigureAwait(false);
    }

    private CredentialRecord Record(string refresh, string? access, DateTimeOffset? expiresAt) =>
        new(
            CredentialRecord.CurrentSchemaVersion,
            _host.Cloud.CloudUrl,
            refresh,
            access,
            expiresAt,
            FakeCloud.WorkspaceId,
            "01J8SESSION",
            FakeCloud.UserId,
            FakeCloud.Email,
            _host.Cloud.GatewayUri,
            Now);

    private TokenRefresher Refresher(CredentialRecord record, ICredentialStore? store = null)
    {
        if (store is null)
        {
            _store.Add(record);
            store = _store;
        }

        return new TokenRefresher(
            new StoredLogin(record, store),
            new CredentialStores(store, [store], fallbackReason: null),
            _host.Discovery,
            _host.OAuth,
            _host.Services.GetRequiredService<CrossProcessLock>(),
            _host.Time,
            NullLogger<TokenRefresher>.Instance);
    }

    private CredentialRecord StoredRecord() =>
        _store.ReadAsync(_host.Cloud.CloudUrl, CancellationToken.None).GetAwaiter().GetResult().Record
        ?? throw new InvalidOperationException("Nothing is stored.");

    /// <summary>
    /// Answers each read with the next record, then repeats the last.
    /// </summary>
    private sealed class ScriptedStore(params CredentialRecord[] reads) : ICredentialStore
    {
        private int _read;

        public CredentialStoreKind Kind => CredentialStoreKind.File;

        public string DisplayName => "scripted";

        public List<string> Deleted { get; } = [];

        public Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialReadResult(reads[Math.Min(_read++, reads.Length - 1)], null));

        public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken)
        {
            Deleted.Add(cloudUrl);
            return Task.FromResult(true);
        }
    }
}
