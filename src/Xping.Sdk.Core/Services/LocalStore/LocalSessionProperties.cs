/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Environment properties the local store adds to the sessions it writes.
/// </summary>
/// <remarks>
/// These describe how a session was recorded rather than what it recorded, so they travel on
/// <see cref="Xping.Sdk.Core.Models.Environments.EnvironmentInfo.CustomProperties"/> beside the Git
/// metadata rather than as fields on <see cref="TestSession"/> itself. The session model is also the
/// upload payload; adding a field there to answer a purely local question would change the wire
/// format for every connected project.
/// </remarks>
public static class LocalSessionProperties
{
    /// <summary>
    /// The property key holding the <see cref="XpingMode"/> the session was recorded under.
    /// </summary>
    public const string Mode = "Xping.Mode";

    /// <summary>
    /// Returns whether the session was recorded by a cloud-connected project.
    /// </summary>
    /// <param name="session">The session to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the session was recorded in connected mode.</returns>
    /// <remarks>
    /// Sessions written before this property existed carry no key and read as not connected. That is
    /// the safe direction: the flag only ever suppresses output, so an old session at worst shows a
    /// developer something they have already seen.
    /// </remarks>
    public static bool IsConnected(TestSession? session)
    {
        if (session?.EnvironmentInfo?.CustomProperties == null)
            return false;

        return session.EnvironmentInfo.CustomProperties.TryGetValue(Mode, out string? mode) &&
               string.Equals(mode, nameof(XpingMode.Connected), StringComparison.Ordinal);
    }
}
