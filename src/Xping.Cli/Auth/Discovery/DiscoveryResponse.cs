/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json.Serialization;

namespace Xping.Cli.Auth.Discovery;

/// <summary>
/// The discovery document as the server sent it, before validation (contract §4.1).
/// </summary>
/// <remarks>
/// Lists only the members the CLI reads; every other member is ignored, so the server can add
/// fields without breaking a released CLI (contract §11). Also the shape of the cache entry, so a
/// cached document is validated exactly as a fresh one.
/// </remarks>
internal sealed record DiscoveryResponse
{
    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; init; }

    [JsonPropertyName("device_authorization_endpoint")]
    public string? DeviceAuthorizationEndpoint { get; init; }

    [JsonPropertyName("xping_data_gateway_uri")]
    public string? DataGatewayUri { get; init; }

    [JsonPropertyName("xping_contract_version")]
    public int? ContractVersion { get; init; }

    [JsonPropertyName("xping_cli_min_version")]
    public string? CliMinVersion { get; init; }
}
