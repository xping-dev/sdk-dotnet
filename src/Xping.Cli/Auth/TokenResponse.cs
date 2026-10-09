/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

namespace Xping.Cli.Auth;

/// <summary>
/// A successful answer of the token endpoint, for any of the three grants (contract §4.3).
/// </summary>
/// <param name="AccessToken">The JWT sent to the DataGateway.</param>
/// <param name="ExpiresIn">How long the access token is valid from the moment it was received.</param>
/// <param name="RefreshToken">The opaque token that buys the next pair; present on every success.</param>
/// <param name="Scope">The granted scope, when the server stated it.</param>
internal sealed record TokenResponse(string AccessToken, TimeSpan ExpiresIn, string RefreshToken, string? Scope)
{
    // The generated ToString would print both tokens wherever the record is logged or interpolated.
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"TokenResponse {{ ExpiresIn = {ExpiresIn}, Scope = {Scope} }}");
}
