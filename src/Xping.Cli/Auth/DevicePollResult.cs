/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// What one poll of the token endpoint answered during a device flow, short of a failure.
/// </summary>
internal enum DevicePollStatus
{
    /// <summary>The user approved; <see cref="DevicePollResult.Token"/> holds the tokens.</summary>
    Completed,

    /// <summary><c>authorization_pending</c>: poll again after the interval.</summary>
    Pending,

    /// <summary><c>slow_down</c>: add 5 seconds to the interval, then poll again.</summary>
    SlowDown,

    /// <summary>
    /// The network failed or the server had a transient error. Poll again after the interval, never
    /// sooner (contract §3.2).
    /// </summary>
    Transient
}

/// <summary>
/// The outcome of one device poll that does not end the flow with an error.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Token">The tokens, for <see cref="DevicePollStatus.Completed"/> only.</param>
/// <param name="Reason">Why the poll failed, for <see cref="DevicePollStatus.Transient"/> only.</param>
internal sealed record DevicePollResult(DevicePollStatus Status, TokenResponse? Token = null, string? Reason = null)
{
    public static readonly DevicePollResult Pending = new(DevicePollStatus.Pending);

    public static readonly DevicePollResult SlowDown = new(DevicePollStatus.SlowDown);

    public static DevicePollResult Completed(TokenResponse token) => new(DevicePollStatus.Completed, token);

    public static DevicePollResult Transient(string reason) => new(DevicePollStatus.Transient, Reason: reason);
}
