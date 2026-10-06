/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Auth.Flows;

/// <summary>
/// What the device flow tells the user while it runs.
/// </summary>
internal interface IDeviceFlowOutput
{
    /// <summary>
    /// Shows where to go and which code to enter, before polling starts.
    /// </summary>
    /// <param name="authorization">The codes and pages; the device code on it is never shown.</param>
    void ShowCode(DeviceAuthorization authorization);

    /// <summary>
    /// Says that a poll could not reach Xping Cloud and that the flow keeps trying. Called once per
    /// run, on the first such poll.
    /// </summary>
    /// <param name="reason">The failure category, such as "connection failed".</param>
    void PollingFailed(string reason);
}

/// <summary>
/// One sign-in through the device flow: the user approves in a browser on any machine while the
/// CLI polls (cli-auth-cli-spec §6).
/// </summary>
/// <remarks>
/// Discovery is fetched fresh for the same reason as in <see cref="LoopbackFlow"/>. The device code
/// lives in memory for the length of this call only.
/// </remarks>
internal sealed class DeviceFlow(
    DiscoveryClient discovery,
    OAuthClient oauth,
    IBrowserLauncher browser,
    IHeadlessDetector headless,
    TimeProvider timeProvider,
    ILogger<DeviceFlow> logger)
{
    /// <summary>
    /// How much <c>slow_down</c> adds to the polling interval (RFC 8628 §3.5).
    /// </summary>
    public static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    private const string ExpiredMessage = "The sign-in code expired. Run `xping login --device` again.";

    /// <summary>
    /// Runs the flow and returns the record to store.
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL.</param>
    /// <param name="workspaceId">A workspace to preselect on the approval page, or <see langword="null"/>.</param>
    /// <param name="noBrowser">Whether <c>--no-browser</c> was given.</param>
    /// <param name="output">Prints the page, the code and the waiting line, and polling trouble.</param>
    /// <param name="cancellationToken">Ctrl+C.</param>
    /// <exception cref="AuthFailureException">The sign-in failed; the exit code and message are on it.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> fired.</exception>
    public async Task<CredentialRecord> RunAsync(
        string cloudUrl,
        string? workspaceId,
        bool noBrowser,
        IDeviceFlowOutput output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        DiscoveryDocument document = await discovery.GetAsync(cloudUrl, useCache: false, cancellationToken).ConfigureAwait(false);

        DeviceAuthorization authorization;
        try
        {
            authorization = await oauth.StartDeviceAsync(document, workspaceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OAuthException ex)
        {
            throw FlowFailures.ServerRefused(cloudUrl, ex.Error);
        }

        DateTimeOffset deadline = timeProvider.GetUtcNow() + authorization.ExpiresIn;
        output.ShowCode(authorization);
        BrowserChoice.Make(headless, noBrowser, logger)
            .Open(browser, authorization.VerificationUriComplete ?? authorization.VerificationUri);

        TimeSpan interval = authorization.Interval;
        bool warned = false;
        while (true)
        {
            // Waited before every poll, including the first and the one after a network error: the
            // CLI never polls faster than the interval (contract §3.2). The last wait stops at the
            // deadline, so an expiry after several slow_down answers is reported when it happens.
            TimeSpan remaining = deadline - timeProvider.GetUtcNow();
            await Task.Delay(remaining < interval ? remaining : interval, timeProvider, cancellationToken).ConfigureAwait(false);

            if (timeProvider.GetUtcNow() >= deadline)
                throw Expired();

            DevicePollResult result;
            try
            {
                result = await oauth.PollDeviceAsync(document, authorization.DeviceCode, cancellationToken).ConfigureAwait(false);
            }
            catch (OAuthException ex)
            {
                throw PollFailed(cloudUrl, ex.Error);
            }

            switch (result.Status)
            {
                case DevicePollStatus.Completed:
                    return SignedInRecord.Create(cloudUrl, result.Token!, document, timeProvider.GetUtcNow());

                case DevicePollStatus.SlowDown:
                    interval += SlowDownStep;
                    logger.LogInformation("The server asked to poll more slowly; polling every {Interval} s", interval.TotalSeconds);
                    break;

                case DevicePollStatus.Transient:
                    logger.LogInformation("Polling failed ({Reason}); trying again in {Interval} s", result.Reason, interval.TotalSeconds);

                    // Without a word the user would wait out the whole code lifetime, approve, and
                    // see nothing happen.
                    if (!warned)
                    {
                        output.PollingFailed(result.Reason ?? "unknown failure");
                        warned = true;
                    }

                    break;

                case DevicePollStatus.Pending:
                default:
                    break;
            }
        }
    }

    private static AuthFailureException Expired() =>
        new(AuthExitCodes.LoginTimedOut, AuthErrorCodes.Timeout, ExpiredMessage);

    private static AuthFailureException PollFailed(string cloudUrl, OAuthError error) =>
        error.Error switch
        {
            OAuthProtocol.AccessDenied => FlowFailures.Declined(),
            OAuthProtocol.ExpiredToken => Expired(),
            OAuthProtocol.InvalidGrant => new AuthFailureException(
                AuthExitCodes.LoginFailed,
                AuthErrorCodes.OAuthError,
                "The device code was rejected. Run `xping login --device` again.")
            {
                OAuthErrorCode = error.Error
            },
            _ => FlowFailures.ServerRefused(cloudUrl, error)
        };
}
