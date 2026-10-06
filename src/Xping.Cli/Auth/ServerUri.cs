/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;

namespace Xping.Cli.Auth;

/// <summary>
/// The rule for every URL the server hands the CLI: absolute, <c>https</c> (or <c>http</c> on the
/// two loopback names the contract allows), no user info, no fragment.
/// </summary>
/// <remarks>
/// The same rule as the Cloud URL itself (cli-auth-cli-spec §14.1): codes and tokens are sent to
/// these URLs, and the user signs in on the pages they name.
/// </remarks>
internal static class ServerUri
{
    public static bool TryParse(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
            || uri.UserInfo.Length > 0
            || uri.Fragment.Length > 0
            || !(uri.Scheme == Uri.UriSchemeHttps
                 || (uri.Scheme == Uri.UriSchemeHttp && uri.Host is "localhost" or "127.0.0.1")))
        {
            uri = null;
            return false;
        }

        return true;
    }
}
