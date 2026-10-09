/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

namespace Xping.Cli.Auth;

/// <summary>
/// The start of a device flow (contract §4.5).
/// </summary>
/// <param name="DeviceCode">The secret the CLI polls with; never shown.</param>
/// <param name="UserCode">The code the user types, exactly as the server formatted it.</param>
/// <param name="VerificationUri">The page the user opens.</param>
/// <param name="VerificationUriComplete">The same page with the code filled in, when sent.</param>
/// <param name="ExpiresIn">How long the codes are valid.</param>
/// <param name="Interval">The wait between two polls; 5 seconds when the server sent none.</param>
internal sealed record DeviceAuthorization(
    string DeviceCode,
    string UserCode,
    Uri VerificationUri,
    Uri? VerificationUriComplete,
    TimeSpan ExpiresIn,
    TimeSpan Interval)
{
    /// <summary>
    /// The interval RFC 8628 §3.2 prescribes when the server names none.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    // The generated ToString would print the device code.
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"DeviceAuthorization {{ UserCode = {UserCode}, VerificationUri = {VerificationUri}, ExpiresIn = {ExpiresIn}, Interval = {Interval} }}");
}
