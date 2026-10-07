/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// Reports which Xping Cloud credential the CLI would use, without a network call
/// (cli-auth-cli-spec §3.4).
/// </summary>
/// <remarks>
/// Agents call this to learn whether Cloud data is possible (§16), so it answers from stored state
/// only: it never refreshes, never verifies a key, and never makes a request.
/// </remarks>
internal sealed class AuthStatusCommand(
    AuthCommandRunner runner,
    GlobalOptions options,
    XpingHome home,
    CredentialStoreSelector selector,
    CredentialResolver resolver,
    TimeProvider timeProvider)
{
    private const int LabelWidth = 14;

    public Task<int> RunAsync(bool json, CancellationToken cancellationToken) =>
        runner.RunAsync(json, (session, configuration) => ReportAsync(session, configuration, cancellationToken), cancellationToken);

    private async Task<int> ReportAsync(AuthSession session, CliConfiguration configuration, CancellationToken cancellationToken)
    {
        string cloudUrl = session.CloudUrl;
        CredentialStores stores = selector.Select(cloudUrl);
        ResolvedCredential credential = await resolver.ResolveAsync(configuration, cancellationToken).ConfigureAwait(false);

        // Store messages can quote an exception from the file system; scrubbed like every message.
        List<string> warnings = [.. credential.Warnings.Concat(credential.StoreFailures).Select(w => Redaction.Scrub(w))];
        if (stores.FallbackReason is { } fallback)
            warnings.Add($"No OS credential store is available ({fallback}); sign-ins are kept in {stores.Selected.DisplayName}.");

        WriteText(session.Text, cloudUrl, credential, warnings);

        if (options.Verbose)
            WritePaths(session.Text, cloudUrl);

        session.Write(Document(cloudUrl, credential, warnings));

        if (credential.Source != CredentialSource.None)
            return AuthExitCodes.Success;

        return credential.StoreFailures.Count > 0 ? AuthExitCodes.CredentialStoreError : AuthExitCodes.AuthRequired;
    }

    private void WriteText(AuthText text, string cloudUrl, ResolvedCredential credential, IReadOnlyList<string> warnings)
    {
        Row(text, "Cloud URL", cloudUrl);

        switch (credential.Source)
        {
            case CredentialSource.StoredLogin when credential.Login is { } login:
                CredentialRecord record = login.Record;
                Row(text, "Credential", $"stored login ({login.Store.DisplayName})");
                Row(text, "Signed in", record.Email ?? record.Sub ?? "unknown user");

                if (record.WorkspaceId is { } workspace)
                    Row(text, "Workspace", workspace);

                if (AuthText.ShortSession(record.Sid) is { } session)
                    Row(text, "Session", session);

                Row(text, "Access token", AccessTokenState(record));

                if (credential.FallbackApiKey is { } fallback)
                    text.Line($"API key ({fallback.Origin}) also set; used only when no login is available.");

                break;

            case CredentialSource.None:
                Row(text, "Credential", "none");
                text.Line("Run `xping login` to sign in.");
                break;

            default:
                Row(text, "Credential", $"API key ({credential.ApiKey?.Origin})");

                if (credential.ShadowedLogin)
                    text.Line("A stored login also exists and is not used while --api-key is given.");

                break;
        }

        foreach (string warning in warnings)
            text.Warning(warning);
    }

    private string AccessTokenState(CredentialRecord record)
    {
        if (record.AccessToken is null || record.AccessTokenExpiresAt is not { } expiresAt)
            return "none stored (refreshed automatically on next use)";

        TimeSpan left = expiresAt - timeProvider.GetUtcNow();
        if (left <= TimeSpan.Zero)
            return "expired (refreshed automatically on next use)";

        int minutes = (int)Math.Floor(left.TotalMinutes);
        string when = minutes switch
        {
            0 => "in less than a minute",
            1 => "in 1 minute",
            _ => string.Create(CultureInfo.InvariantCulture, $"in {minutes} minutes")
        };

        return $"expires {when} (refreshed automatically)";
    }

    private void WritePaths(AuthText text, string cloudUrl)
    {
        Row(text, "Home", XpingHome.Display(home.Root));
        Row(text, "Credentials", XpingHome.Display(home.CredentialsFile));
        Row(text, "Discovery", XpingHome.Display(home.DiscoveryCacheDirectory));
        Row(text, "Projects", XpingHome.Display(home.ProjectCacheDirectory(cloudUrl)));
    }

    private static AuthStatusDocument Document(string cloudUrl, ResolvedCredential credential, IReadOnlyList<string> warnings)
    {
        CredentialRecord? record = credential.Login?.Record;

        return new AuthStatusDocument(
            AuthJson.SchemaVersion,
            cloudUrl,
            credential.Source switch
            {
                CredentialSource.None => "none",
                CredentialSource.StoredLogin => "stored-login",
                _ => "api-key"
            },
            credential.Source switch
            {
                CredentialSource.StoredLogin => AuthJson.StoreName(credential.Login!.Store.Kind),
                CredentialSource.ApiKeyFlag => "flag",
                CredentialSource.ApiKeyEnv => "env",
                CredentialSource.ApiKeyConfig => "config",
                _ => null
            },
            LoggedIn: record is not null,
            record?.Email,
            record?.Sub,
            record?.WorkspaceId,
            record?.Sid,
            record?.AccessToken is null ? null : AuthJson.Timestamp(record.AccessTokenExpiresAt),
            AuthJson.Timestamp(record?.StoredAt),
            credential.FallbackApiKey?.Source switch
            {
                ConfigurationSource.Environment => "env",
                ConfigurationSource.AppSettings => "config",
                _ => null
            },
            credential.ShadowedLogin,
            warnings);
    }

    private static void Row(AuthText text, string label, string value) =>
        text.Line(label.PadRight(LabelWidth) + value);
}
