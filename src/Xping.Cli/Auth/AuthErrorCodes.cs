/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// The <c>error</c> values of the auth commands' JSON documents (cli-auth-cli-spec §3.2).
/// </summary>
internal static class AuthErrorCodes
{
    public const string AccessDenied = "access_denied";
    public const string Timeout = "timeout";
    public const string StateMismatch = "state_mismatch";
    public const string InteractiveRequired = "interactive_required";
    public const string CloudUnreachable = "cloud_unreachable";
    public const string VersionMismatch = "version_mismatch";
    public const string CredentialStore = "credential_store";
    public const string OAuthError = "oauth_error";
    public const string Cancelled = "cancelled";
}
