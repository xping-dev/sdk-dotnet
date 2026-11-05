/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// The DataGateway answered a digest or run history read with a body the CLI cannot read
/// (<c>invalid-response</c>, cloud-data-in-cli-cli-spec §3.4).
/// </summary>
/// <remarks>
/// Not an unreachable Cloud: the request arrived and was answered, so only that read is lost.
/// </remarks>
internal sealed class CloudResponseException : Exception
{
    public CloudResponseException()
    {
    }

    public CloudResponseException(string message)
        : base(message)
    {
    }

    public CloudResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
