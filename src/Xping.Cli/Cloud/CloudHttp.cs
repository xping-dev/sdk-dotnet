/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// The DataGateway client's name and time limits (cli-auth-cli-spec §2.2, §10.2).
/// </summary>
internal static class CloudHttp
{
    /// <summary>
    /// The named client whose primary handler every DataGateway pipeline ends in.
    /// </summary>
    public const string ClientName = "xping-cloud";

    /// <summary>
    /// How long one attempt may take.
    /// </summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long one request may take, retries included.
    /// </summary>
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(30);
}
