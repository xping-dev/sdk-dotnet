/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Configuration;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// The output of one auth command run: its text on stderr and, under <c>--json</c>, its one document
/// on stdout.
/// </summary>
internal sealed class AuthSession(ConsoleIO io, AuthText text, bool json, string cloudUrl)
{
    /// <summary>
    /// Gets the human text writer.
    /// </summary>
    public AuthText Text { get; } = text;

    /// <summary>
    /// Gets whether <c>--json</c> was given.
    /// </summary>
    public bool Json { get; } = json;

    /// <summary>
    /// Gets the normalized Cloud URL the command works on; the configured one once it is loaded.
    /// </summary>
    public string CloudUrl { get; internal set; } = cloudUrl;

    /// <summary>
    /// Writes <paramref name="document"/> to stdout when <c>--json</c> was given.
    /// </summary>
    public void Write<T>(T document)
    {
        if (Json)
            AuthJson.Write(io.Output, document);
    }

    /// <summary>
    /// Reports a failure on stderr and, under <c>--json</c>, as the failure document.
    /// </summary>
    /// <remarks>
    /// The message is scrubbed here, once for both streams: it can quote a server's
    /// <c>error_description</c> or a redirect's, and either may hold a token (§15.2).
    /// </remarks>
    /// <returns><paramref name="exitCode"/>.</returns>
    public int Fail(int exitCode, string error, string message, string? oauthError = null)
    {
        string safe = Redaction.Scrub(message);
        Text.Line(safe);
        Write(new AuthFailedDocument(AuthJson.SchemaVersion, "failed", CloudUrl, error, safe, oauthError));
        return exitCode;
    }
}

/// <summary>
/// What the three auth commands share: the configuration load, Ctrl+C, and the reporting of every
/// failure in both output modes.
/// </summary>
/// <remarks>
/// One place, so a failure can never leave stdout empty under <c>--json</c> or reach the user
/// unscrubbed in one command and not the others.
/// </remarks>
internal sealed class AuthCommandRunner(
    ConsoleIO io,
    GlobalOptions options,
    CliConfigurationLoader configurationLoader,
    IEnvironmentVariableProvider environment)
{
    /// <summary>
    /// Loads the configuration and runs <paramref name="body"/>, turning its failures into exit codes.
    /// </summary>
    public async Task<int> RunAsync(
        bool json,
        Func<AuthSession, CliConfiguration, Task<int>> body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        // Until the configuration loads, the flag or the default is the best name for the Cloud; a
        // configuration that fails to load has no valid URL to offer instead.
        var session = new AuthSession(io, new AuthText(io, environment), json, options.CloudUrl ?? CloudUrl.Default);

        try
        {
            CliConfiguration configuration;
            try
            {
                configuration = configurationLoader.Load();
            }
            catch (CliConfigurationException ex)
            {
                return session.Fail(2, AuthErrorCodes.Configuration, ex.Message);
            }

            session.CloudUrl = configuration.CloudUrl.Value;
            cancellationToken.ThrowIfCancellationRequested();

            return await body(session, configuration).ConfigureAwait(false);
        }
        catch (AuthFailureException ex)
        {
            return session.Fail(ex.ExitCode, ex.ErrorCode, ex.Message, ex.OAuthErrorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return session.Fail(AuthExitCodes.Cancelled, AuthErrorCodes.Cancelled, "Cancelled.");
        }
    }
}
