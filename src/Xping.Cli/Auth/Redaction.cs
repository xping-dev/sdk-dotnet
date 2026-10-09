/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Xping.Cli.Auth;

/// <summary>
/// Scrubs secrets from text before it is shown or logged.
/// </summary>
/// <remarks>
/// <para>
/// One place, applied to every log message, every error message and every <c>--verbose</c> line,
/// so a new code path cannot forget it. It is a backstop, not a licence: code still must not put a
/// secret into a message in the first place.
/// </para>
/// <para>
/// Process-wide rather than per invocation because a secret known to one part of the process is a
/// secret everywhere in it. Tests that register a value use a unique one, so sharing is harmless.
/// </para>
/// </remarks>
internal static partial class Redaction
{
    /// <summary>
    /// The text that replaces a secret.
    /// </summary>
    public const string Placeholder = "[redacted]";

    // Shorter values are refused: a short "secret" would be scrubbed from ordinary words and make
    // every message unreadable, and no token or key this CLI handles is that short.
    private const int MinimumSecretLength = 8;

    private static readonly ConcurrentDictionary<string, byte> KnownSecrets = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a value this process holds, so it is scrubbed wherever it appears.
    /// </summary>
    /// <param name="secret">A token or API key.</param>
    public static void AddSecret(string? secret)
    {
        if (secret is { Length: >= MinimumSecretLength })
            KnownSecrets.TryAdd(secret, 0);
    }

    /// <summary>
    /// Returns <paramref name="text"/> with every secret replaced by <see cref="Placeholder"/>.
    /// </summary>
    /// <param name="text">The text to scrub.</param>
    /// <returns>The scrubbed text.</returns>
    public static string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Known values first: the patterns below would otherwise cut a known value in half and
        // leave the rest of it behind.
        foreach (string secret in KnownSecrets.Keys.OrderByDescending(s => s.Length))
            text = text.Replace(secret, Placeholder, StringComparison.Ordinal);

        text = HeaderValue().Replace(text, "${name}" + Placeholder);
        text = FormField().Replace(text, "${name}" + Placeholder);
        text = JsonMember().Replace(text, "${name}" + Placeholder);
        text = Jwt().Replace(text, Placeholder);

        return text;
    }

    // The value runs to the end of the line: "Bearer <token>" carries a space.
    [GeneratedRegex(
        @"(?<name>\b(?:Authorization|X-API-Key)\s*:\s*)[^\r\n]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderValue();

    // The look-behind keeps "code=" from matching the tail of "error_code=": the underscore is a
    // word character, so that is not a field named "code". Names that are secrets in their own
    // right ("access_token") are listed in full for the same reason.
    [GeneratedRegex(
        @"(?<name>(?<![A-Za-z0-9_])(?:code|refresh_token|access_token|id_token|device_code|code_verifier|client_secret|token|state)=)[^&\s]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex FormField();

    // A token response body, or the stored credential record, quoted in a message. The camelCase
    // names are the record's (IXpingSerializer writes camelCase).
    [GeneratedRegex(
        @"(?<name>""(?:code|refresh_token|access_token|id_token|device_code|code_verifier|client_secret|token|state|refreshToken|accessToken|idToken|deviceCode|codeVerifier|clientSecret)""\s*:\s*"")(?:[^""\\]|\\.)*",
        RegexOptions.CultureInvariant)]
    private static partial Regex JsonMember();

    // Header and payload are Base64URL JSON objects, so both start "eyJ". Requiring that keeps
    // dotted .NET type names in a --verbose stack trace intact.
    [GeneratedRegex(
        @"eyJ[A-Za-z0-9_-]*\.eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();
}
