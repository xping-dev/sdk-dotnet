/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;

namespace Xping.Cli.Auth.Store;

/// <summary>
/// One stored sign-in for one Cloud URL (cli-auth-cli-spec §7.2).
/// </summary>
/// <remarks>
/// The identity members are copied from the unverified access token payload when the record is
/// stored, for display only. The PKCE verifier, <c>state</c>, codes and the device code are never
/// part of it (contract §10.4).
/// </remarks>
/// <param name="SchemaVersion">The record format; only <see cref="CurrentSchemaVersion"/> is read.</param>
/// <param name="CloudUrl">The normalized Cloud URL the record belongs to.</param>
/// <param name="RefreshToken">The opaque token that buys the next pair.</param>
/// <param name="AccessToken">The last access token, or <see langword="null"/> when it was dropped.</param>
/// <param name="AccessTokenExpiresAt">
/// When <paramref name="AccessToken"/> expires, computed by the CLI as receipt time plus
/// <c>expires_in</c>, not read from <c>exp</c>, so the machine's clock skew applies consistently.
/// </param>
/// <param name="WorkspaceId">The workspace the session is bound to.</param>
/// <param name="Sid">The session id.</param>
/// <param name="Sub">The user id.</param>
/// <param name="Email">The user's e-mail address.</param>
/// <param name="DataGatewayUri">The DataGateway base URI from discovery.</param>
/// <param name="StoredAt">When the record was written.</param>
internal sealed record CredentialRecord(
    int SchemaVersion,
    string CloudUrl,
    string RefreshToken,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAt,
    string? WorkspaceId,
    string? Sid,
    string? Sub,
    string? Email,
    string DataGatewayUri,
    DateTimeOffset StoredAt)
{
    /// <summary>
    /// The only record format this CLI reads or writes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Returns whether this record is usable as the sign-in for <paramref name="cloudUrl"/>.
    /// </summary>
    /// <remarks>
    /// Deserialization leaves a missing member <see langword="null"/> whatever its declared type,
    /// so the required ones are checked here. A record stored under one Cloud URL but naming another
    /// is corrupt too: its tokens were issued by a different server (cli-auth-cli-spec §7.7).
    /// </remarks>
    public bool IsValidFor(string cloudUrl) =>
        SchemaVersion == CurrentSchemaVersion
        && string.Equals(CloudUrl, cloudUrl, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(RefreshToken)
        && !string.IsNullOrWhiteSpace(DataGatewayUri);

    // The generated ToString would print both tokens wherever the record is logged or interpolated.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture,
            $"SchemaVersion = {SchemaVersion}, CloudUrl = {CloudUrl}, AccessTokenExpiresAt = {AccessTokenExpiresAt:O}, ")
            .Append(CultureInfo.InvariantCulture,
            $"WorkspaceId = {WorkspaceId}, Sid = {Sid}, Sub = {Sub}, Email = {Email}, DataGatewayUri = {DataGatewayUri}, StoredAt = {StoredAt:O}");
        return true;
    }
}
