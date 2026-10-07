/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;

namespace Xping.Cli.Cloud;

/// <summary>
/// Typed reads from the DataGateway with one credential (cli-auth-cli-spec §10.1).
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
    /// Reads one test, or <see langword="null"/> when Xping Cloud has no data for it.
    /// </summary>
    Task<CloudTest?> GetTestAsync(string projectKey, string testFingerprint, CancellationToken cancellationToken);
}
