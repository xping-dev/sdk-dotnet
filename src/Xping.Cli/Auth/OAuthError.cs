/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// An RFC 6749 §5.2 error the Portal answered with.
/// </summary>
/// <param name="Error">The <c>error</c> code, such as <c>invalid_grant</c>.</param>
/// <param name="ErrorDescription">The server's <c>error_description</c>, when it sent one.</param>
/// <param name="StatusCode">The HTTP status of the response.</param>
internal sealed record OAuthError(string Error, string? ErrorDescription, int StatusCode);

/// <summary>
/// The Portal refused an OAuth request with an error that retrying does not fix.
/// </summary>
/// <remarks>
/// Commands map <see cref="Error"/> to their own message and exit code (cli-auth-cli-spec §4.8,
/// §6.3); the message here is the fallback for an error code this CLI does not know.
/// </remarks>
internal sealed class OAuthException : Exception
{
    public OAuthException()
        : this(new OAuthError(OAuthProtocol.InvalidRequest, null, 0))
    {
    }

    public OAuthException(string message)
        : base(message)
    {
        Error = new OAuthError(OAuthProtocol.InvalidRequest, null, 0);
    }

    public OAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = new OAuthError(OAuthProtocol.InvalidRequest, null, 0);
    }

    public OAuthException(OAuthError error)
        : base(Describe(error))
    {
        Error = error;
    }

    /// <summary>
    /// Gets what the server answered.
    /// </summary>
    public OAuthError Error { get; }

    private static string Describe(OAuthError error) =>
        error.ErrorDescription is { Length: > 0 } description
            ? $"Xping Cloud returned {error.Error}: {description}"
            : $"Xping Cloud returned {error.Error}.";
}
