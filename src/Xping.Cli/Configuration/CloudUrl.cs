/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Xping.Cli.Configuration;

/// <summary>
/// Validates and normalizes an Xping Cloud (Portal) URL.
/// </summary>
/// <remarks>
/// The normalized string is the key for the credential store and every cache under
/// <c>~/.xping</c>, and discovery compares it to the server's <c>issuer</c> ordinally. Two spellings
/// of one server must therefore normalize to one string, or a user ends up signed in to
/// <c>https://App.Xping.io/</c> but not to <c>https://app.xping.io</c>.
/// </remarks>
internal static class CloudUrl
{
    /// <summary>
    /// The URL used when nothing else is configured.
    /// </summary>
    public const string Default = "https://app.xping.io";

    /// <summary>
    /// Validates <paramref name="value"/> and returns its normalized form.
    /// </summary>
    /// <param name="value">The URL as the user supplied it.</param>
    /// <param name="sourceName">What supplied it, for the error message: <c>--cloud-url</c>, a variable name.</param>
    /// <param name="normalized">Lowercase scheme and host, no default port, no trailing slash.</param>
    /// <param name="error">Why the value was refused.</param>
    /// <returns><see langword="true"/> when the value is a usable Cloud URL.</returns>
    public static bool TryNormalize(
        string value,
        string sourceName,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out string? error)
    {
        normalized = null;
        string trimmed = value.Trim();

        // Messages never quote the value as given: it can carry a password or a query string, and
        // an env var or settings file is no less likely to hold one than the command line. They
        // quote the parts that cannot be secret instead.
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            error = $"{sourceName} must be an absolute https URL.";
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            error = $"{sourceName} must not contain a user name or password.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            error = $"{sourceName} must be an absolute https URL, got scheme '{uri.Scheme}'.";
            return false;
        }

        // Uri drops an empty "?" or "#", so the raw text is checked as well: the issuer comparison
        // happens on the normalized string, and a URL the user typed with a query is not the issuer
        // whatever Uri makes of it.
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0 || trimmed.Contains('?', StringComparison.Ordinal)
            || trimmed.Contains('#', StringComparison.Ordinal))
        {
            error = $"{sourceName} must not contain a query or fragment.";
            return false;
        }

        // [::1] is deliberately absent: the contract lists only these two hosts for plain http.
        if (uri.Scheme == Uri.UriSchemeHttp && uri.Host is not ("localhost" or "127.0.0.1"))
        {
            error = $"{sourceName} must use https; http is allowed only for localhost and 127.0.0.1, got '{Origin(uri)}'.";
            return false;
        }

        // The Portal's issuer has no path, and the issuer must equal this URL exactly.
        if (uri.AbsolutePath != "/")
        {
            error = $"{sourceName} must not contain a path, got '{Origin(uri)}{uri.AbsolutePath}'.";
            return false;
        }

        normalized = Origin(uri);
        error = null;
        return true;
    }

    /// <summary>
    /// Returns the file-name-safe key of a normalized Cloud URL: the first 16 hex digits of its
    /// SHA-256.
    /// </summary>
    /// <remarks>
    /// A URL holds characters no file system accepts (<c>:</c> on Windows), and a hash keeps the
    /// names of the caches under <c>~/.xping</c> the same length for every server.
    /// </remarks>
    /// <param name="normalized">A value returned by <see cref="TryNormalize"/>.</param>
    /// <returns>Sixteen lowercase hex digits.</returns>
    public static string StorageKey(string normalized)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash, 0, 8);
    }

    // Uri already lowercases scheme and host and keeps the brackets of an IPv6 host.
    private static string Origin(Uri uri) =>
        uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : string.Create(CultureInfo.InvariantCulture, $"{uri.Scheme}://{uri.Host}:{uri.Port}");
}
