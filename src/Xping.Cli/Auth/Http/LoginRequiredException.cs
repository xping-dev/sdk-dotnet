/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Http;

/// <summary>
/// The stored sign-in is no longer valid, and only <c>xping login</c> fixes it (cli-auth-cli-spec §9.6).
/// </summary>
/// <remarks>
/// By the time this is thrown the tokens have been deleted, except for a 401
/// <c>Error.Authentication.MissingCredentials</c>, where nothing was sent that could be wrong.
/// </remarks>
internal sealed class LoginRequiredException : Exception
{
    /// <summary>
    /// The message every command shows for it.
    /// </summary>
    public const string DefaultMessage = "Your Xping Cloud sign-in is no longer valid. Run `xping login` to sign in again.";

    public LoginRequiredException()
        : base(DefaultMessage)
    {
    }

    public LoginRequiredException(string message)
        : base(message)
    {
    }

    public LoginRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
