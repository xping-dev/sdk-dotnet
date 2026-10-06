/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

namespace Xping.Cli.Auth.Discovery;

/// <summary>
/// A validated discovery document: the endpoints this CLI may use for one Cloud URL.
/// </summary>
/// <param name="CloudUrl">The normalized Cloud URL the document was fetched from.</param>
/// <param name="AuthorizationEndpoint">Where the browser is sent to sign in.</param>
/// <param name="TokenEndpoint">Where codes and refresh tokens are exchanged.</param>
/// <param name="RevocationEndpoint">Where <c>logout</c> revokes the session.</param>
/// <param name="DeviceAuthorizationEndpoint">Where the device flow starts.</param>
/// <param name="DataGatewayUri">The only base URL an access token is ever sent to; no trailing slash.</param>
internal sealed record DiscoveryDocument(
    string CloudUrl,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri RevocationEndpoint,
    Uri DeviceAuthorizationEndpoint,
    string DataGatewayUri)
{
    /// <summary>
    /// The contract version this CLI implements (contract §11).
    /// </summary>
    public const int ContractVersion = 1;

    /// <summary>
    /// Checks <paramref name="response"/> in the order of cli-auth-cli-spec §2.3 and fails closed on
    /// the first problem.
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL the document came from.</param>
    /// <param name="response">The document as sent.</param>
    /// <param name="cliVersion">This CLI's version.</param>
    /// <returns>The usable document.</returns>
    /// <exception cref="AuthFailureException">The document cannot be trusted or used.</exception>
    public static DiscoveryDocument Validate(string cloudUrl, DiscoveryResponse response, CliVersion cliVersion)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(cliVersion);

        // OpenIddict publishes the issuer as Uri.AbsoluteUri, which gives a URL without a path one
        // trailing slash (contract OQ-17). Compared as text: a document claiming another server is
        // not ours, however the URLs would normalize.
        string expectedIssuer = cloudUrl + "/";
        if (!string.Equals(response.Issuer, expectedIssuer, StringComparison.Ordinal))
            throw Unusable(cloudUrl, $"names an issuer other than {expectedIssuer}");

        // A query would be dropped by every v1/ path appended to the base, so it cannot be meant.
        if (!ServerUri.TryParse(response.DataGatewayUri, out Uri? gateway) || gateway.Query.Length > 0)
            throw Unusable(cloudUrl, "has no usable xping_data_gateway_uri");

        CheckContractVersion(response.ContractVersion);
        CheckMinimumCliVersion(cloudUrl, response.CliMinVersion, cliVersion);

        return new DiscoveryDocument(
            cloudUrl,
            Endpoint(cloudUrl, response.AuthorizationEndpoint, "authorization_endpoint"),
            Endpoint(cloudUrl, response.TokenEndpoint, "token_endpoint"),
            Endpoint(cloudUrl, response.RevocationEndpoint, "revocation_endpoint"),
            Endpoint(cloudUrl, response.DeviceAuthorizationEndpoint, "device_authorization_endpoint"),
            NormalizeBase(gateway));
    }

    private static void CheckContractVersion(int? version)
    {
        if (version == ContractVersion)
            return;

        string reported = version?.ToString(CultureInfo.InvariantCulture) ?? "none";
        string newer = version > ContractVersion ? "Upgrade the CLI." : "The server is older than this CLI.";

        throw AuthFailureException.VersionMismatch(
            $"This CLI implements Cloud contract version {ContractVersion}; the server reports {reported}. {newer}");
    }

    private static void CheckMinimumCliVersion(string cloudUrl, string? minimum, CliVersion cliVersion)
    {
        if (!SemanticVersion.TryParse(minimum, out SemanticVersion? required))
            throw Unusable(cloudUrl, "has an xping_cli_min_version that is not a SemVer version");

        // An unparseable own version cannot be shown to satisfy the minimum, so it does not.
        if (SemanticVersion.TryParse(cliVersion.Value, out SemanticVersion? current) && current >= required)
            return;

        throw AuthFailureException.VersionMismatch(
            $"Please upgrade the xping CLI: this server requires {required} or newer, you have {cliVersion.Value}.");
    }

    // Used as given, never rebuilt from the paths in the contract: the server may move them.
    private static Uri Endpoint(string cloudUrl, string? value, string name) =>
        ServerUri.TryParse(value, out Uri? uri) ? uri : throw Unusable(cloudUrl, $"has no usable {name}");

    // Lowercase scheme and host, no default port, no trailing slash: the form the host guard of
    // §9.1 compares request URIs against.
    private static string NormalizeBase(Uri uri) =>
        uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped) + uri.AbsolutePath.TrimEnd('/');

    private static AuthFailureException Unusable(string cloudUrl, string reason) =>
        AuthFailureException.Unreachable(cloudUrl, $"its discovery document {reason}");
}
