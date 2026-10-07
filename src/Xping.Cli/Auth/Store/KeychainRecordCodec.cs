/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Json;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Turns a <see cref="CredentialRecord"/> into the secret a keychain entry holds, and back.
/// </summary>
/// <remarks>
/// Shared by the OS backends so the size rule and the corruption rule are the same everywhere
/// (cli-auth-cli-spec §7.3, §7.7).
/// </remarks>
internal static class KeychainRecordCodec
{
    /// <summary>
    /// Returns the UTF-8 JSON of <paramref name="record"/>, without its access token when the whole
    /// record exceeds <paramref name="maxBytes"/>.
    /// </summary>
    /// <remarks>
    /// The access token is optional (contract §10.4): without it the next command refreshes first.
    /// A maximum-length refresh token plus an access token does not fit a Windows credential.
    /// </remarks>
    /// <exception cref="CredentialStoreException">Even without the access token it does not fit.</exception>
    public static byte[] Encode(CredentialRecord record, int? maxBytes, IXpingSerializer serializer, string storeName)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(serializer);

        byte[] secret = serializer.SerializeToUtf8Bytes(record);
        if (maxBytes is not { } limit || secret.Length <= limit)
            return secret;

        Array.Clear(secret);
        secret = serializer.SerializeToUtf8Bytes(record with { AccessToken = null, AccessTokenExpiresAt = null });
        if (secret.Length <= limit)
            return secret;

        int length = secret.Length;
        Array.Clear(secret);
        throw new CredentialStoreException(string.Create(
            CultureInfo.InvariantCulture,
            $"The sign-in is too large for {storeName} ({length} bytes, at most {limit})."));
    }

    /// <summary>
    /// Reads the record stored under <paramref name="cloudUrl"/> from <paramref name="secret"/>.
    /// </summary>
    /// <returns>The record, or a corrupt-entry warning.</returns>
    public static CredentialReadResult Decode(ReadOnlySpan<byte> secret, string cloudUrl, IXpingSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        CredentialRecord? record;
        try
        {
            record = serializer.Deserialize<CredentialRecord>(secret);
        }
        catch (JsonException)
        {
            return CredentialReadResult.Corrupt(cloudUrl);
        }

        if (record is null || !record.IsValidFor(cloudUrl))
            return CredentialReadResult.Corrupt(cloudUrl);

        Redaction.AddSecret(record.RefreshToken);
        Redaction.AddSecret(record.AccessToken);
        return new CredentialReadResult(record, null);
    }
}
