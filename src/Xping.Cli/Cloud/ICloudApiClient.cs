/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud.Digest;

namespace Xping.Cli.Cloud;

/// <summary>
/// Typed reads from the DataGateway with one credential (cloud-data-in-cli-cli-spec §2.2).
/// </summary>
/// <remarks>
/// Every method can throw <see cref="LoginRequiredException"/> (the sign-in is over),
/// <see cref="AuthFailureException"/> (Cloud unreachable or incompatible) and
/// <see cref="CloudApiException"/> (the read was refused).
/// </remarks>
internal interface ICloudApiClient : IDisposable
{
    /// <summary>
    /// Gets problems the user should hear about that did not stop the read, worded for the user.
    /// </summary>
    IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Lists one page of the workspace's projects.
    /// </summary>
    Task<PagedResult<ProjectSummary>> ListProjectsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one project, or <see langword="null"/> when the workspace has none with that key.
    /// </summary>
    Task<ProjectSummary?> GetProjectAsync(string projectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the digest of a batch of tests (contract §6.1), or <see langword="null"/> when the
    /// workspace has no project with that key.
    /// </summary>
    /// <exception cref="CloudResponseException">The answer could not be read.</exception>
    Task<DigestResponse?> GetDigestAsync(string projectKey, DigestRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the newest <paramref name="limit"/> runs of one test (contract §6.3), or
    /// <see langword="null"/> when the workspace has no such project or test.
    /// </summary>
    /// <exception cref="CloudResponseException">The answer could not be read.</exception>
    Task<RunsResponse?> GetRunsAsync(string projectKey, string fingerprint, int limit, CancellationToken cancellationToken);
}
