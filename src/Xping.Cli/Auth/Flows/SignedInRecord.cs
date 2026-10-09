/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Auth.Flows;

/// <summary>
/// Builds the record a sign-in stores from the token endpoint's answer (cli-auth-cli-spec §7.2).
/// </summary>
internal static class SignedInRecord
{
    /// <summary>
    /// Builds the record for tokens received at <paramref name="receivedAt"/>.
    /// </summary>
    public static CredentialRecord Create(string cloudUrl, TokenResponse tokens, DiscoveryDocument document, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(document);

        AccessTokenClaims claims = AccessTokenClaims.Read(tokens.AccessToken);

        return new CredentialRecord(
            CredentialRecord.CurrentSchemaVersion,
            cloudUrl,
            tokens.RefreshToken,
            tokens.AccessToken,

            // Receipt time plus expires_in, not the token's exp: the machine's clock skew then applies
            // to both ends of the comparison (§7.2).
            receivedAt + tokens.ExpiresIn,
            claims.WorkspaceId,
            claims.Sid,
            claims.Sub,
            claims.Email,
            document.DataGatewayUri,
            receivedAt);
    }
}
