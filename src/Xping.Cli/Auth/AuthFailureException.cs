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
    /// Gets the server's OAuth <c>error</c> when <see cref="ErrorCode"/> is
    /// <see cref="AuthErrorCodes.OAuthError"/>; the JSON documents carry it as <c>oauthError</c>.
    /// </summary>
    public string? OAuthErrorCode { get; init; }

    /// <summary>
    /// Gets the short cause on its own ("connection failed", "timeout"), for a message that wraps it
    /// in words of its own, such as <c>logout</c>'s warning; <see langword="null"/> when there is none
    /// beyond <see cref="Exception.Message"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the URL that could not be reached, when <see cref="ErrorCode"/> is
    /// <see cref="AuthErrorCodes.CloudUnreachable"/>; <c>report</c>'s hint line names it on its own.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Xping Cloud could not be reached, or answered with something this CLI cannot use.
    /// </summary>
    public static AuthFailureException Unreachable(string cloudUrl, string reason, Exception? innerException = null) =>
        new(
            AuthExitCodes.CloudUnreachable,
            AuthErrorCodes.CloudUnreachable,
            $"Could not reach Xping Cloud at {cloudUrl}: {reason}.",
            innerException)
        {
            Reason = reason,
            Target = cloudUrl
        };

    /// <summary>
    /// The server and this CLI implement versions of the contract that do not work together.
    /// </summary>
    public static AuthFailureException VersionMismatch(string message) =>
        new(AuthExitCodes.CloudVersionMismatch, AuthErrorCodes.VersionMismatch, message);
}
