/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Flows;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// The options of <c>xping login</c> (cli-auth-cli-spec §3.2).
/// </summary>
/// <param name="Json">Write the result to stdout as JSON.</param>
/// <param name="Device">Use the device flow instead of the loopback flow.</param>
/// <param name="NoBrowser">Do not try to open a browser.</param>
/// <param name="WorkspaceId">The workspace to preselect, already validated as a ULID.</param>
internal sealed record LoginOptions(bool Json, bool Device = false, bool NoBrowser = false, string? WorkspaceId = null);

/// <summary>
/// Signs in to Xping Cloud from a browser (cli-auth-cli-spec §3.2).
/// </summary>
/// <remarks>
/// Every failure is reported here, with its exit code and, under <c>--json</c>, its document on
/// stdout, so nothing but that document ever reaches stdout. The command never asks a question in
/// the terminal: every choice happens in the browser (§3.5).
/// </remarks>
internal sealed class LoginCommand(
    ConsoleIO io,
    AuthCommandRunner runner,
    IEnvironmentVariableProvider environment,
    XpingHome home,
    CredentialStoreSelector selector,
    LoopbackFlow loopback,
    DeviceFlow device,
    ILogger<LoginCommand> logger)
{
    public Task<int> RunAsync(LoginOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return runner.RunAsync(
            options.Json,
            (session, configuration) => SignInAsync(options, session, configuration, cancellationToken),
            cancellationToken);
    }

    private async Task<int> SignInAsync(
        LoginOptions options,
        AuthSession session,
        CliConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // Before anything touches the network or the disk: a coding agent or a CI job that reaches
        // this command must learn at once that it cannot sign in, not after a browser opened.
        if (!io.IsInputTerminal || !io.IsErrorTerminal || IsTruthy(environment.GetVariable("CI")))
        {
            throw new AuthFailureException(
                AuthExitCodes.InteractiveRequired,
                AuthErrorCodes.InteractiveRequired,
                "xping login needs an interactive terminal. Run it in your own shell. Coding agents and " +
                "CI must use an API key or a login stored earlier; see `xping auth status`.");
        }

        string cloudUrl = session.CloudUrl;
        CredentialStores stores = selector.Select();
        EnsureStoreAvailable(stores);

        if (configuration.ApiKey is { } apiKey)
            logger.LogInformation("An API key is also set ({Origin}); it is used only when no sign-in is available", apiKey.Origin);

        CredentialRecord record = options.Device
            ? await device.RunAsync(
                cloudUrl,
                options.WorkspaceId,
                options.NoBrowser,
                authorization => ShowCode(session.Text, authorization),
                cancellationToken).ConfigureAwait(false)
            : await loopback.RunAsync(
                cloudUrl,
                options.WorkspaceId,
                options.NoBrowser,
                (url, mode, timeout) => ShowLink(session.Text, cloudUrl, url, mode, timeout),
                cancellationToken).ConfigureAwait(false);

        // The last point a Ctrl+C is honoured. Once stored, the sign-in is complete.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await stores.WriteAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (CredentialStoreException ex)
        {
            throw new AuthFailureException(
                AuthExitCodes.CredentialStoreError,
                AuthErrorCodes.CredentialStore,
                $"Signed in, but the sign-in could not be stored: {ex.Message}",
                ex);
        }

        session.Text.SignedIn(record, stores.Selected);
        session.Write(new LoginSucceededDocument(
            AuthJson.SchemaVersion,
            "signed-in",
            cloudUrl,
            options.Device ? "device" : "loopback",
            record.Email,
            record.Sub,
            record.WorkspaceId,
            record.Sid,
            AuthJson.StoreName(stores.Selected.Kind),
            AuthJson.Timestamp(record.AccessTokenExpiresAt)));

        return AuthExitCodes.Success;
    }

    /// <summary>
    /// Returns whether a <c>CI</c> value means "this is CI".
    /// </summary>
    internal static bool IsTruthy(string? value) =>
        value is not null
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private void EnsureStoreAvailable(CredentialStores stores)
    {
        // Only the file store exists until the keychains arrive; it is available when ~/.xping can be
        // created private to the user.
        if (stores.Selected.Kind != CredentialStoreKind.File)
            return;

        try
        {
            home.EnsurePrivate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AuthFailureException(
                AuthExitCodes.CredentialStoreError,
                AuthErrorCodes.CredentialStore,
                $"Could not prepare {XpingHome.Display(home.Root)} to store the sign-in: {ex.Message}",
                ex);
        }
    }

    private static void ShowLink(AuthText text, string cloudUrl, Uri url, LinkMode mode, TimeSpan timeout)
    {
        switch (mode)
        {
            case LinkMode.Headless:
                text.Line($"No browser was found on this machine. Open this link in a browser {text.Emphasis("on this machine")}:");
                break;
            case LinkMode.NoBrowser:
                text.Line($"Open this link in a browser {text.Emphasis("on this machine")}:");
                break;
            default:
                text.Line($"Opening your browser to sign in to Xping Cloud ({cloudUrl}).");
                text.Line("If it does not open, use this link:");
                break;
        }

        text.Line();
        text.Line($"  {url.AbsoluteUri}");
        text.Line();

        if (mode != LinkMode.Browser)
            text.Line("If your browser is on another machine, press Ctrl+C and run `xping login --device`.");

        text.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"Waiting for you to finish in the browser (up to {timeout.TotalMinutes:0} minutes)..."));
    }

    private static void ShowCode(AuthText text, DeviceAuthorization authorization)
    {
        text.Line($"To sign in, open  {authorization.VerificationUri.AbsoluteUri}");
        text.Line($"and enter the code  {text.Emphasis(authorization.UserCode)}");
        text.Line();

        if (authorization.VerificationUriComplete is { } complete)
        {
            text.Line($"(or open {complete.AbsoluteUri})");
            text.Line();
        }

        text.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"Waiting for you to approve in the browser (up to {authorization.ExpiresIn.TotalMinutes:0} minutes)..."));
    }
}
