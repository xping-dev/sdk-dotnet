/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.NetworkInformation;

namespace Xping.Sdk.Actions.Configurations;

/// <summary>
/// Represents the configuration settings for a network ping operation.
/// </summary>
public class PingConfiguration
{
    /// <summary>
    /// Gets or sets the number of routing nodes that can forward the <see cref="Ping"/> data before it is discarded.
    /// </summary>
    /// <returns>
    /// An integer value that specifies the number of times the <see cref="Ping"/> data packets can be forwarded. 
    /// The default is 128.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value specified for a set operation is less than or equal to zero.
    /// </exception>
    public int Ttl { get; set; } = 128;

    /// <summary>
    /// Gets or sets the maximum amount of time to wait for the ICMP echo reply message after sending the echo message.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that specifies the time to wait for an ICMP echo reply message.
    /// The default value is 5 seconds.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value specified for a set operation is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
