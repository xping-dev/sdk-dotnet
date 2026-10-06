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
    ConsoleIO io,
    GlobalOptions options,
    CliConfigurationLoader configurationLoader,
    IEnvironmentVariableProvider environment,
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

    public async Task<int> RunAsync(bool json, CancellationToken cancellationToken)
    {
        var text = new AuthText(io, environment);
        string cloudUrl = options.CloudUrl ?? CloudUrl.Default;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            CliConfiguration configuration;
            try
            {
                configuration = configurationLoader.Load();
            }
            catch (CliConfigurationException ex)
            {
                await io.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return 2;
            }

            cloudUrl = configuration.CloudUrl.Value;
            CredentialStores stores = selector.Select();
            StoredLoginLookup lookup = await stores.ReadAsync(cloudUrl, cancellationToken).ConfigureAwait(false);

            string? warning = null;
            bool revoked = false;

            if (lookup.Login is { } login)
                (revoked, warning) = await RevokeAsync(cloudUrl, login.Record.RefreshToken, cancellationToken).ConfigureAwait(false);

            // Past this point the sign-out completes: a Ctrl+C that arrives during deletion must not
            // leave half the entries behind.
            bool deleted = await DeleteEverywhereAsync(stores, cloudUrl).ConfigureAwait(false);

            if (lookup.Login is null && !deleted)
                return NotSignedIn(text, json, cloudUrl, configuration.ApiKey);

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
                warning ??= "Removed stored credentials that could not be read; no session was revoked.";
                text.Warning(warning);
                if (lookup.Login is not null)
                {
                    text.Line("  Your local credentials were removed. The session may still exist on the server; revoke it");
                    text.Line($"  under {text.SessionsPage}.");
                }

                text.Done("Signed out locally.");
            }

            if (json)
                AuthJson.Write(io.Output, new LogoutDocument(AuthJson.SchemaVersion, result, cloudUrl, revoked, warning));

            return AuthExitCodes.Success;
        }
        catch (AuthFailureException ex)
        {
            Fail(text, json, cloudUrl, ex.ErrorCode, ex.Message);
            return ex.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(text, json, cloudUrl, AuthErrorCodes.Cancelled, "Cancelled.");
            return AuthExitCodes.Cancelled;
        }
    }

    private async Task<(bool Revoked, string? Warning)> RevokeAsync(string cloudUrl, string refreshToken, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(RevokeTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        string reason;
        try
        {
            DiscoveryDocument document = await discovery.GetAsync(cloudUrl, useCache: true, linked.Token).ConfigureAwait(false);
            await oauth.RevokeAsync(document, refreshToken, linked.Token).ConfigureAwait(false);
            return (true, null);
        }
        catch (AuthFailureException ex)
        {
            reason = ex.Reason ?? ex.Message.TrimEnd('.');
        }
        catch (OAuthException ex)
        {
            // Revocation answers 200 even for an unknown token (contract §4.4); any refusal means the
            // server did not revoke, and the reason is its error code.
            reason = ex.Error.Error;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            reason = "timeout";
        }

        logger.LogInformation("Revocation failed: {Reason}", reason);
        return (false, $"Could not reach {cloudUrl} to revoke the session ({reason}).");
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

    private int NotSignedIn(AuthText text, bool json, string cloudUrl, ConfiguredValue? apiKey)
    {
        text.Line($"You are not signed in to {cloudUrl}.");

        if (apiKey is not null)
        {
            text.Line(apiKey.Source == ConfigurationSource.Environment
                ? $"An API key is set in {apiKey.Origin}; logout does not remove it. Unset {apiKey.Origin} to stop using it."
                : $"An API key is set in {apiKey.Origin}; logout does not remove it.");
        }

        if (json)
            AuthJson.Write(io.Output, new LogoutDocument(AuthJson.SchemaVersion, "not-signed-in", cloudUrl, Revoked: false, Warning: null));

        return AuthExitCodes.Success;
    }

    private void Fail(AuthText text, bool json, string cloudUrl, string error, string message)
    {
        text.Line(message);

        if (json)
            AuthJson.Write(io.Output, new AuthFailedDocument(AuthJson.SchemaVersion, "failed", cloudUrl, error, message, OAuthError: null));
    }
}
