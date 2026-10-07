/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Configuration;

namespace Xping.Cli.Cloud.Projects;

/// <summary>
/// The Cloud project an assembly was bound to, and how.
/// </summary>
/// <param name="Key">The project key the DataGateway reads by.</param>
/// <param name="Source"><c>flag</c>, <c>env</c>, <c>session</c> or <c>name-match</c>.</param>
internal sealed record ProjectBinding(string Key, string Source)
{
    /// <summary>The binding came from matching the assembly name against the project list.</summary>
    public bool IsNameMatch => Source == ProjectResolver.NameMatch;
}

/// <summary>
/// What one report knows that can bind an assembly to a Cloud project.
/// </summary>
/// <param name="CloudUrl">The normalized Cloud URL, part of the cache key.</param>
/// <param name="WorkspaceId">The signed-in workspace, or null for an API key, which is never cached.</param>
/// <param name="Configured"><c>--project</c>, <c>XPING_PROJECTID</c> or <c>Xping:ProjectId</c>.</param>
/// <param name="SessionPins">The project pin each assembly's newest session was recorded with.</param>
internal sealed record ProjectLookup(
    string CloudUrl,
    string? WorkspaceId,
    ConfiguredValue? Configured,
    IReadOnlyDictionary<string, string> SessionPins);

/// <summary>
/// Binds a test assembly to the Cloud project key the DataGateway reads by
/// (cli-auth-cli-spec §11.2).
/// </summary>
/// <remarks>
/// The Cloud derives a key from the assembly name unless the SDK was pinned, and that rule is the
/// Cloud's to change, so it is never reproduced here. What is left is, in order: what the user
/// configured, what the session recorded, and the project whose display name is the assembly name.
/// </remarks>
internal sealed class ProjectResolver(ProjectCache cache)
{
    /// <summary>The <see cref="ProjectBinding.Source"/> of a match by display name.</summary>
    public const string NameMatch = "name-match";

    private const int PageSize = 50;

    // A workspace with more projects than this is not searched to the end: the enrichment budget
    // would be spent listing, and --project is the documented way out.
    private const int MaxPages = 20;

    /// <summary>
    /// Returns the binding for <paramref name="assembly"/>, or <see langword="null"/> when nothing
    /// binds it.
    /// </summary>
    public async Task<ProjectBinding?> ResolveAsync(
        ICloudApiClient client, string assembly, ProjectLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        if (lookup.Configured is { } configured)
            return new ProjectBinding(configured.Value, configured.Source == ConfigurationSource.Flag ? "flag" : "env");

        if (lookup.SessionPins.TryGetValue(assembly, out string? pin))
            return new ProjectBinding(pin, "session");

        if (lookup.WorkspaceId is { } workspaceId && cache.Read(lookup.CloudUrl, workspaceId, assembly) is { } cached)
            return new ProjectBinding(cached.Id, NameMatch);

        return await MatchByNameAsync(client, assembly, lookup, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Re-resolves a name match whose project answered 404: the cached match is dropped and the
    /// project list read again, once. Any other binding is final and returns <see langword="null"/>.
    /// </summary>
    public async Task<ProjectBinding?> RebindAsync(
        ICloudApiClient client, string assembly, ProjectBinding stale, ProjectLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stale);
        ArgumentNullException.ThrowIfNull(lookup);

        if (!stale.IsNameMatch)
            return null;

        if (lookup.WorkspaceId is { } workspaceId)
            cache.Evict(lookup.CloudUrl, workspaceId, assembly);

        ProjectBinding? fresh = await MatchByNameAsync(client, assembly, lookup, cancellationToken).ConfigureAwait(false);
        return fresh is not null && fresh.Key != stale.Key ? fresh : null;
    }

    private async Task<ProjectBinding?> MatchByNameAsync(
        ICloudApiClient client, string assembly, ProjectLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        for (int page = 1; page <= MaxPages; page++)
        {
            PagedResult<ProjectSummary> projects =
                await client.ListProjectsAsync(page, PageSize, cancellationToken).ConfigureAwait(false);

            if (projects.Items.FirstOrDefault(p => string.Equals(p.DisplayName, assembly, StringComparison.Ordinal)) is { } match)
            {
                if (lookup.WorkspaceId is { } workspaceId)
                    cache.Write(lookup.CloudUrl, workspaceId, assembly, match);

                return new ProjectBinding(match.Id, NameMatch);
            }

            if (!projects.HasNextPage)
                break;
        }

        return null;
    }
}
