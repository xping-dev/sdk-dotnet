/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// The DataGateway client's name and time limits (cloud-data-in-cli-cli-spec §3.2).
/// </summary>
internal static class CloudHttp
{
    /// <summary>
    /// The named client whose primary handler every DataGateway pipeline ends in.
    /// </summary>
    public const string ClientName = "xping-cloud";

    /// <summary>
    /// How long one attempt may take (contract §7.3 <c>CloudRequestTimeoutMs</c>).
    /// </summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long one request may take, retry included: never longer than the whole budget.
    /// </summary>
    public static readonly TimeSpan TotalTimeout = CloudBudget.Total;
}
