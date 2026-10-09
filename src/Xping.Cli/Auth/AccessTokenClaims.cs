/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Buffers.Text;
using System.Text.Json;

namespace Xping.Cli.Auth;

/// <summary>
/// The claims of an access token the CLI shows to the user.
/// </summary>
/// <remarks>
/// Read from the payload without verifying the signature: the DataGateway verifies the token, and
/// the CLI uses these values for display only (contract §6.1). A token that is not a readable JWT
/// gives empty claims rather than an error, because nothing depends on them.
/// </remarks>
/// <param name="Sub">The user id.</param>
/// <param name="Sid">The session id, as listed on the Portal's CLI sessions page.</param>
/// <param name="Email">The user's e-mail address.</param>
/// <param name="WorkspaceId">The workspace the session is bound to.</param>
internal sealed record AccessTokenClaims(string? Sub, string? Sid, string? Email, string? WorkspaceId)
{
    /// <summary>
    /// No claims could be read.
    /// </summary>
    public static AccessTokenClaims None { get; } = new(null, null, null, null);

    /// <summary>
    /// Reads the claims of <paramref name="accessToken"/>.
    /// </summary>
    public static AccessTokenClaims Read(string? accessToken)
    {
        string[] parts = (accessToken ?? string.Empty).Split('.');
        if (parts.Length != 3)
            return None;

        try
        {
            byte[] payload = Base64Url.DecodeFromChars(parts[1]);
            using JsonDocument document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return None;

            return new AccessTokenClaims(
                StringClaim(document.RootElement, "sub"),
                StringClaim(document.RootElement, "sid"),
                StringClaim(document.RootElement, "email"),
                StringClaim(document.RootElement, "workspace_id"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return None;
        }
    }

    private static string? StringClaim(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
