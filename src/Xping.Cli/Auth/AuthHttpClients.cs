/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// The names of the CLI's HTTP clients (cli-auth-cli-spec §2.2).
/// </summary>
internal static class AuthHttpClients
{
    /// <summary>
    /// The back channel to the Portal: discovery, token, device authorization and revocation.
    /// </summary>
    public const string OAuth = "xping-oauth";

    /// <summary>
    /// How long one request on <see cref="OAuth"/> may take.
    /// </summary>
    public static readonly TimeSpan OAuthTimeout = TimeSpan.FromSeconds(15);
}
