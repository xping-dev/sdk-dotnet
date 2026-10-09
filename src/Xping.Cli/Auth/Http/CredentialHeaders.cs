/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Http;

/// <summary>
/// The two credential headers of the DataGateway; a request carries exactly one (contract §7.1).
/// </summary>
internal static class CredentialHeaders
{
    /// <summary>
    /// The API key header.
    /// </summary>
    public const string ApiKey = "X-API-Key";
}
