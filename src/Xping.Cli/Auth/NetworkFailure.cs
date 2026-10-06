/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net.Http;
using System.Security.Authentication;

namespace Xping.Cli.Auth;

/// <summary>
/// Names the kind of network failure for a message, without quoting the exception.
/// </summary>
/// <remarks>
/// Exception messages can carry a URL with a query, a header or a host the user did not type; the
/// category is all a user needs to act on (cli-auth-cli-spec §4.8).
/// </remarks>
internal static class NetworkFailure
{
    /// <summary>
    /// Returns whether <paramref name="exception"/> is a failure to talk to the server, as opposed
    /// to the caller cancelling.
    /// </summary>
    public static bool IsNetworkFailure(Exception exception, CancellationToken cancellationToken) =>
        exception switch
        {
            HttpRequestException => true,

            // A connection that drops while the body is read can surface unwrapped.
            IOException => true,

            // HttpClient.Timeout surfaces as a cancellation that nobody asked for.
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false
        };

    /// <summary>
    /// Describes a failure that <see cref="IsNetworkFailure"/> accepted.
    /// </summary>
    public static string Describe(Exception exception) =>
        exception switch
        {
            OperationCanceledException => "timeout",
            HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError } => "name not resolved",
            HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => "TLS error",
            HttpRequestException { InnerException: AuthenticationException } => "TLS error",

            // Refused, reset and unreachable all land here; naming one of them would send the user
            // to check the wrong thing.
            HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError } => "connection failed",
            _ => "network error"
        };
}
