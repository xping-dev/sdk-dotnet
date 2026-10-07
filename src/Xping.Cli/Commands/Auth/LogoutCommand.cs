/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// Revokes the stored sign-in and removes it from this machine (cli-auth-cli-spec §12).
/// </summary>
/// <remarks>
/// Revocation is attempted first, but the local credentials are removed whether or not it worked
/// (contract §3.4): a user who signs out offline must not stay signed in. No prompt, no browser, and
/// an API key is never touched.
/// </remarks>
internal sealed class LogoutCommand(
    AuthCommandRunner runner,
    XpingHome home,
    CredentialStoreSelector selector,
    DiscoveryClient discovery,
    DiscoveryCache discoveryCache,
    OAuthClient oauth,
    TimeProvider timeProvider,
    ILogger<LogoutCommand> logger)
{
    /// <summary>
    /// How long revocation may take, retries included, before the sign-out goes ahead locally.
    /// </summary>
    public static readonly TimeSpan RevokeTimeout = TimeSpan.FromSeconds(15);

    public Task<int> RunAsync(bool json, CancellationToken cancellationToken) =>
        runner.RunAsync(json, (session, configuration) => SignOutAsync(session, configuration, cancellationToken), cancellationToken);

    private async Task<int> SignOutAsync(AuthSession session, CliConfiguration configuration, CancellationToken cancellationToken)
    {
        string cloudUrl = session.CloudUrl;
        CredentialStores stores = selector.Select(cloudUrl);
        StoredLoginLookup lookup = await stores.ReadAsync(cloudUrl, cancellationToken).ConfigureAwait(false);

        string? warning = null;
        bool revoked = false;

        if (lookup.Login is { } login)
            (revoked, warning) = await RevokeAsync(cloudUrl, login.Record.RefreshToken, cancellationToken).ConfigureAwait(false);

        // Past this point the sign-out completes: a Ctrl+C that arrives during deletion must not
        // leave half the entries behind.
        bool deleted = await DeleteEverywhereAsync(stores, cloudUrl).ConfigureAwait(false);

        if (lookup.Login is null && !deleted)
            return NotSignedIn(session, configuration.ApiKey);

        AuthText text = session.Text;
        string result;
        if (revoked)
        {
            result = "signed-out";
            text.Done(lookup.Login?.Record.Email is { } email
                ? $"Signed out of {cloudUrl} ({email})."
                : $"Signed out of {cloudUrl}.");
        }
        else
        {
            result = "signed-out-locally";

            // An entry that could not be read has no refresh token to revoke; the warning says
            // what was removed instead.
            warning = Redaction.Scrub(warning ?? "Removed stored credentials that could not be read; no session was revoked.");
            text.Warning(warning);
            if (lookup.Login is not null)
            {
                text.Line("  Your local credentials were removed. The session may still exist on the server; revoke it");
                text.Line($"  under {text.SessionsPage}.");
            }

            text.Done("Signed out locally.");
        }

        session.Write(new LogoutDocument(AuthJson.SchemaVersion, result, cloudUrl, revoked, warning));
        return AuthExitCodes.Success;
    }

    private async Task<(bool Revoked, string? Warning)> RevokeAsync(string cloudUrl, string refreshToken, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(RevokeTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        string warning;
        try
        {
            DiscoveryDocument document = await discovery.GetAsync(cloudUrl, useCache: true, linked.Token).ConfigureAwait(false);
            await oauth.RevokeAsync(document, refreshToken, linked.Token).ConfigureAwait(false);
            return (true, null);
        }
        catch (AuthFailureException ex) when (ex.Reason is { } reason)
        {
            warning = $"Could not reach {cloudUrl} to revoke the session ({reason}).";
        }
        catch (AuthFailureException ex)
        {
            // Reached, but unusable: a version mismatch or a discovery document that fails validation.
            warning = $"Could not revoke the session at {cloudUrl}: {ex.Message}";
        }
        catch (OAuthException ex)
        {
            // Revocation answers 200 even for an unknown token (contract §4.4); any refusal means the
            // server was reached and did not revoke.
            warning = $"Xping Cloud at {cloudUrl} refused to revoke the session ({ex.Error.Error}).";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            warning = $"Could not reach {cloudUrl} to revoke the session (timeout).";
        }

        logger.LogInformation("Revocation failed: {Warning}", warning);
        return (false, warning);
    }

    private async Task<bool> DeleteEverywhereAsync(CredentialStores stores, string cloudUrl)
    {
        bool deleted;
        try
        {
            deleted = await stores.DeleteAllAsync(cloudUrl, CancellationToken.None).ConfigureAwait(false);
        }
        catch (CredentialStoreException ex)
        {
            string where = string.Join(", ", stores.ReadOrder.Select(s => s.DisplayName));
            throw new AuthFailureException(
                AuthExitCodes.CredentialStoreError,
                AuthErrorCodes.CredentialStore,
                $"Could not remove stored credentials: {ex.Message} Remove them by hand: {where}.",
                ex);
        }

        // Caches are not credentials: a failure to remove them is logged, never reported as one.
        string projects = home.ProjectCacheDirectory(cloudUrl);
        try
        {
            if (Directory.Exists(projects))
                Directory.Delete(projects, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Project cache {Path} could not be deleted: {Reason}", projects, ex.GetType().Name);
        }

        discoveryCache.Delete(cloudUrl);
        return deleted;
    }

    private static int NotSignedIn(AuthSession session, ConfiguredValue? apiKey)
    {
        session.Text.Line($"You are not signed in to {session.CloudUrl}.");

        if (apiKey is not null)
        {
            session.Text.Line(apiKey.Source == ConfigurationSource.Environment
                ? $"An API key is set in {apiKey.Origin}; logout does not remove it. Unset {apiKey.Origin} to stop using it."
                : $"An API key is set in {apiKey.Origin}; logout does not remove it.");
        }

        session.Write(new LogoutDocument(AuthJson.SchemaVersion, "not-signed-in", session.CloudUrl, Revoked: false, Warning: null));
        return AuthExitCodes.Success;
    }
}
