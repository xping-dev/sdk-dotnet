/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// A sign-in, sign-out or Cloud step failed in a way the command reports with one of
/// <see cref="AuthExitCodes"/>.
/// </summary>
/// <remarks>
/// The message is written for the user and never holds a secret; <see cref="ErrorCode"/> is the
/// <c>error</c> value of the command's JSON document (cli-auth-cli-spec §3.2).
/// </remarks>
internal sealed class AuthFailureException : Exception
{
    public AuthFailureException()
        : this(AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, "The Xping Cloud request failed.")
    {
    }

    public AuthFailureException(string message)
        : this(AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, message)
    {
    }

    public AuthFailureException(string message, Exception innerException)
        : this(AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, message, innerException)
    {
    }

    public AuthFailureException(int exitCode, string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the exit code the command returns, one of <see cref="AuthExitCodes"/>.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets the machine-readable reason, one of <see cref="AuthErrorCodes"/>.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Xping Cloud could not be reached, or answered with something this CLI cannot use.
    /// </summary>
    public static AuthFailureException Unreachable(string cloudUrl, string reason, Exception? innerException = null) =>
        new(
            AuthExitCodes.CloudUnreachable,
            AuthErrorCodes.CloudUnreachable,
            $"Could not reach Xping Cloud at {cloudUrl}: {reason}.",
            innerException);

    /// <summary>
    /// The server and this CLI implement versions of the contract that do not work together.
    /// </summary>
    public static AuthFailureException VersionMismatch(string message) =>
        new(AuthExitCodes.CloudVersionMismatch, AuthErrorCodes.VersionMismatch, message);
}
