/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Flows;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Auth.Http;

/// <summary>
/// Hands out a current access token for one stored sign-in, refreshing it when needed
/// (cli-auth-cli-spec §9.2–§9.6).
/// </summary>
/// <remarks>
/// <para>
/// One instance per sign-in per command. Within the process, refreshes are single-flight: callers
/// that queue behind a refresh get its result rather than starting another one. Across processes,
/// the refresh runs under <see cref="CrossProcessLock"/> and re-reads the store first, so the
/// refresh token presented is never older than a few milliseconds and another process's rotation
/// is adopted rather than raced (contract §6.6).
/// </para>
/// <para>
/// Only <c>invalid_grant</c> ends the sign-in. Network failures, 5xx and the other OAuth errors
/// leave the tokens in place (contract §3.3).
/// </para>
/// </remarks>
internal sealed class TokenRefresher : IDisposable
{
    // Contract §3.3: refresh when less than this is left, so a request never carries a token that
    // expires on its way.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly CredentialStores _stores;
    private readonly DiscoveryClient _discovery;
    private readonly OAuthClient _oauth;
    private readonly CrossProcessLock _processLock;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenRefresher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<string> _warnings = [];

    private CredentialRecord? _record;
    private ICredentialStore _store;
    private int _invalidated;

    public TokenRefresher(
        StoredLogin login,
        CredentialStores stores,
        DiscoveryClient discovery,
        OAuthClient oauth,
        CrossProcessLock processLock,
        TimeProvider timeProvider,
        ILogger<TokenRefresher> logger)
    {
        ArgumentNullException.ThrowIfNull(login);

        _record = login.Record;
        _store = login.Store;
        _stores = stores;
        _discovery = discovery;
        _oauth = oauth;
        _processLock = processLock;
        _timeProvider = timeProvider;
        _logger = logger;

        CloudUrl = login.Record.CloudUrl;
        DataGatewayUri = login.Record.DataGatewayUri;

        Redaction.AddSecret(login.Record.RefreshToken);
        Redaction.AddSecret(login.Record.AccessToken);
    }

    /// <summary>
    /// Gets the normalized Cloud URL of the sign-in.
    /// </summary>
    public string CloudUrl { get; }

    /// <summary>
    /// Gets the only base URL the access token is sent to (contract §2).
    /// </summary>
    /// <remarks>Fixed at sign-in; a refresh does not move it.</remarks>
    public string DataGatewayUri { get; }

    /// <summary>
    /// Gets problems the user should hear about that did not stop the command, worded for the user.
    /// </summary>
    public IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_warnings)
                return [.. _warnings];
        }
    }

    /// <summary>
    /// Returns an access token with more than a minute left, refreshing first when needed.
    /// </summary>
    /// <exception cref="LoginRequiredException">The sign-in is over; its tokens were deleted.</exception>
    /// <exception cref="AuthFailureException">Xping Cloud could not be reached or is incompatible.</exception>
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        CredentialRecord record = Volatile.Read(ref _record) ?? throw new LoginRequiredException();

        return IsFresh(record)
            ? Task.FromResult(record.AccessToken!)
            : RefreshAsync(rejectedToken: null, cancellationToken);
    }

    /// <summary>
    /// Returns a token other than <paramref name="rejectedToken"/>, which the DataGateway refused.
    /// </summary>
    /// <remarks>
    /// When another caller already replaced it, that token is returned without a request (§9.3).
    /// </remarks>
    /// <exception cref="LoginRequiredException">The sign-in is over; its tokens were deleted.</exception>
    /// <exception cref="AuthFailureException">Xping Cloud could not be reached or is incompatible.</exception>
    public Task<string> ForceRefreshAsync(string rejectedToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rejectedToken);
        return RefreshAsync(rejectedToken, cancellationToken);
    }

    /// <summary>
    /// Ends the sign-in: forgets the tokens and deletes them from every store (§9.6 step 2).
    /// </summary>
    /// <remarks>
    /// Runs once per instance. A store that cannot be changed is a warning, not a failure: the
    /// command still falls back or degrades, and the server has ended the session anyway.
    /// </remarks>
    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _record, null);

        if (Interlocked.Exchange(ref _invalidated, 1) == 1)
            return;

        _logger.LogInformation("The sign-in for {CloudUrl} is no longer valid; deleting it", CloudUrl);

        try
        {
            await _stores.DeleteAllAsync(CloudUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (CredentialStoreException ex)
        {
            Warn($"Could not remove the expired Xping Cloud sign-in: {ex.Message}");
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<string> RefreshAsync(string? rejectedToken, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CredentialRecord record = _record ?? throw new LoginRequiredException();

            // A caller that queued behind a refresh gets its result (§9.3).
            if (Usable(record, rejectedToken))
                return record.AccessToken!;

            using (await _processLock.AcquireAsync(CloudUrl, cancellationToken).ConfigureAwait(false))
                return await RefreshUnderLockAsync(record, rejectedToken, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> RefreshUnderLockAsync(CredentialRecord record, string? rejectedToken, CancellationToken cancellationToken)
    {
        // Another process may have refreshed while this one waited for the lock (§9.4 step 2).
        StoredLogin? stored = await RereadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            Adopt(stored);
            if (Usable(stored.Record, rejectedToken))
            {
                _logger.LogInformation("Using the access token another process refreshed");
                return stored.Record.AccessToken!;
            }

            record = stored.Record;
        }

        DiscoveryDocument discovery = await _discovery.GetAsync(CloudUrl, useCache: true, cancellationToken).ConfigureAwait(false);

        TokenResponse tokens;
        try
        {
            tokens = await RedeemAsync(discovery, record.RefreshToken, cancellationToken).ConfigureAwait(false);
        }
        catch (OAuthException ex) when (ex.Error.Error == OAuthProtocol.InvalidGrant)
        {
            // §9.6 step 1: a process that ran without the lock may have rotated the token just now.
            StoredLogin? rotated = await RereadAsync(cancellationToken).ConfigureAwait(false);
            if (rotated is null || string.Equals(rotated.Record.RefreshToken, record.RefreshToken, StringComparison.Ordinal))
                throw await EndAsync(cancellationToken).ConfigureAwait(false);

            Adopt(rotated);
            if (Usable(rotated.Record, rejectedToken))
                return rotated.Record.AccessToken!;

            record = rotated.Record;
            try
            {
                tokens = await RedeemAsync(discovery, record.RefreshToken, cancellationToken).ConfigureAwait(false);
            }
            catch (OAuthException again) when (again.Error.Error == OAuthProtocol.InvalidGrant)
            {
                throw await EndAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        DateTimeOffset receivedAt = _timeProvider.GetUtcNow();
        CredentialRecord refreshed = SignedInRecord.Create(CloudUrl, tokens, discovery, receivedAt) with
        {
            DataGatewayUri = record.DataGatewayUri
        };

        // The new pair is in memory before the store is touched, so a failed write still leaves this
        // process with working tokens (§9.5).
        Volatile.Write(ref _record, refreshed);
        _logger.LogInformation("Refreshed the access token for {CloudUrl}", CloudUrl);

        try
        {
            await _store.WriteAsync(refreshed, cancellationToken).ConfigureAwait(false);
        }
        catch (CredentialStoreException ex)
        {
            Warn($"Could not store the refreshed Xping Cloud sign-in: {ex.Message} The next command may ask you to sign in again.");
        }

        return refreshed.AccessToken!;
    }

    private async Task<TokenResponse> RedeemAsync(DiscoveryDocument discovery, string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            return await _oauth.RefreshAsync(discovery, refreshToken, cancellationToken).ConfigureAwait(false);
        }
        catch (OAuthException ex) when (ex.Error.Error != OAuthProtocol.InvalidGrant)
        {
            // Only invalid_grant ends a sign-in (contract §3.3). Anything else is the server or a
            // contract mismatch, and the tokens may work again once it is fixed.
            throw AuthFailureException.Unreachable(CloudUrl, $"the token endpoint answered {ex.Error.Error}", ex);
        }
    }

    private async Task<LoginRequiredException> EndAsync(CancellationToken cancellationToken)
    {
        await InvalidateAsync(cancellationToken).ConfigureAwait(false);
        return new LoginRequiredException();
    }

    // The sign-in as the stores hold it now. Null when it is gone, or when no store could be read,
    // in which case the in-memory record is the best there is.
    private async Task<StoredLogin?> RereadAsync(CancellationToken cancellationToken)
    {
        StoredLoginLookup lookup = await _stores.ReadAsync(CloudUrl, cancellationToken).ConfigureAwait(false);

        if (lookup.Login is null && lookup.Failures.Count == 0)
        {
            // Another process signed out, or ended the session; nothing here can bring it back.
            Volatile.Write(ref _record, null);
            throw new LoginRequiredException();
        }

        return lookup.Login;
    }

    private void Adopt(StoredLogin stored)
    {
        Redaction.AddSecret(stored.Record.RefreshToken);
        Redaction.AddSecret(stored.Record.AccessToken);

        Volatile.Write(ref _record, stored.Record);
        _store = stored.Store;
    }

    private bool Usable(CredentialRecord record, string? rejectedToken) =>
        IsFresh(record) && !string.Equals(record.AccessToken, rejectedToken, StringComparison.Ordinal);

    private bool IsFresh(CredentialRecord record) =>
        record.AccessToken is not null
        && record.AccessTokenExpiresAt is { } expiresAt
        && expiresAt - _timeProvider.GetUtcNow() > ExpiryMargin;

    private void Warn(string message)
    {
        _logger.LogWarning("{Warning}", message);
        lock (_warnings)
            _warnings.Add(message);
    }
}
