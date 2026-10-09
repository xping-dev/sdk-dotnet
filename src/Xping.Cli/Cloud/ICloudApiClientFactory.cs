/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;

namespace Xping.Cli.Cloud;

/// <summary>
/// Builds a DataGateway client for one resolved credential (cli-auth-cli-spec §2.2).
/// </summary>
internal interface ICloudApiClientFactory
{
    /// <summary>
    /// Returns a client that reads with <paramref name="credential"/>.
    /// </summary>
    /// <param name="credential">A sign-in or an API key; the caller has already handled none.</param>
    /// <param name="cloudUrl">The normalized Cloud URL the credential belongs to.</param>
    /// <param name="cancellationToken">Cancels discovery for an API key.</param>
    /// <exception cref="ArgumentException"><paramref name="credential"/> is none.</exception>
    /// <exception cref="AuthFailureException">
    /// An API key's DataGateway could not be discovered: Cloud unreachable or incompatible.
    /// </exception>
    Task<ICloudApiClient> CreateAsync(ResolvedCredential credential, string cloudUrl, CancellationToken cancellationToken);
}
