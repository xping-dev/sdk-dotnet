/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// The JSON documents of <c>login</c>, <c>logout</c> and <c>auth status</c> (cli-auth-cli-spec §3).
/// </summary>
/// <remarks>
/// <para>
/// Written with options of their own rather than the SDK serializer's, for the reason
/// <c>ReportJsonOptions</c> gives: this is a documented output contract that agents parse, not a wire
/// format that moves with the Cloud. Nulls are written, so a consumer can tell "no value" from "field
/// removed".
/// </para>
/// <para>
/// No document holds a token, a code, the verifier or <c>state</c> (§15); the types below have no
/// member that could carry one.
/// </para>
/// </remarks>
internal static class AuthJson
{
    /// <summary>The auth JSON schema, independent of the report envelope's.</summary>
    public const string SchemaVersion = "1";

    /// <summary>Gets the options every auth document is written with.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.Default
    };

    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="output"/> as the only thing on it.
    /// </summary>
    public static void Write<T>(TextWriter output, T document)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteLine(JsonSerializer.Serialize(document, Options));
    }

    /// <summary>
    /// Formats a timestamp as UTC to the second, <c>2026-09-29T10:15:00Z</c>.
    /// </summary>
    public static string? Timestamp(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Names a store kind as the documents do: <c>keychain</c> or <c>file</c>.
    /// </summary>
    public static string StoreName(CredentialStoreKind kind) => kind switch
    {
        CredentialStoreKind.Keychain => "keychain",
        CredentialStoreKind.File => "file",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

/// <summary>
/// <c>login --json</c> after a sign-in.
/// </summary>
internal sealed record LoginSucceededDocument(
    string SchemaVersion,
    string Result,
    string CloudUrl,
    string Flow,
    string? Email,
    string? Sub,
    string? WorkspaceId,
    string? SessionId,
    string Store,
    string? AccessTokenExpiresAt);

/// <summary>
/// Any auth command's <c>--json</c> document when it failed.
/// </summary>
/// <remarks>
/// <c>error</c> is one of <c>AuthErrorCodes</c>; <c>oauthError</c> is the server's own code when
/// <c>error</c> is <c>oauth_error</c>.
/// </remarks>
internal sealed record AuthFailedDocument(
    string SchemaVersion,
    string Result,
    string CloudUrl,
    string Error,
    string Message,

    // The naming policy would write "oAuthError".
    [property: JsonPropertyName("oauthError")] string? OAuthError);

/// <summary>
/// <c>logout --json</c>.
/// </summary>
/// <remarks><c>result</c> is <c>signed-out</c>, <c>signed-out-locally</c> or <c>not-signed-in</c>.</remarks>
internal sealed record LogoutDocument(
    string SchemaVersion,
    string Result,
    string CloudUrl,
    bool Revoked,
    string? Warning);

/// <summary>
/// <c>auth status --json</c>.
/// </summary>
internal sealed record AuthStatusDocument(
    string SchemaVersion,
    string CloudUrl,
    string Credential,
    string? CredentialSource,
    bool LoggedIn,
    string? Email,
    string? Sub,
    string? WorkspaceId,
    string? SessionId,
    string? AccessTokenExpiresAt,
    string? StoredAt,
    string? FallbackApiKey,
    bool ShadowedLogin,
    IReadOnlyList<string> Warnings);
