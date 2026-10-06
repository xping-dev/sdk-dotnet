/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// The fixed values of the CLI's OAuth client registration and the error codes it switches on
/// (contract §5, §8.1).
/// </summary>
internal static class OAuthProtocol
{
    /// <summary>The one registered client. Public: there is no secret.</summary>
    public const string ClientId = "xping-cli";

    /// <summary>Both scopes, always together; the server refuses either alone.</summary>
    public const string Scope = "user:read offline_access";

    public const string AuthorizationCodeGrant = "authorization_code";
    public const string RefreshTokenGrant = "refresh_token";
    public const string DeviceCodeGrant = "urn:ietf:params:oauth:grant-type:device_code";

    public const string InvalidRequest = "invalid_request";
    public const string InvalidClient = "invalid_client";
    public const string InvalidGrant = "invalid_grant";
    public const string UnauthorizedClient = "unauthorized_client";
    public const string UnsupportedGrantType = "unsupported_grant_type";
    public const string InvalidScope = "invalid_scope";
    public const string AccessDenied = "access_denied";
    public const string AuthorizationPending = "authorization_pending";
    public const string SlowDown = "slow_down";
    public const string ExpiredToken = "expired_token";
    public const string ServerError = "server_error";
    public const string TemporarilyUnavailable = "temporarily_unavailable";
}
