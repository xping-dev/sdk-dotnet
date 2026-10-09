/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Cloud.Projects;

/// <summary>
/// Remembers which Cloud project an assembly's name matched, for 24 hours, in
/// <c>~/.xping/cache/projects/{key}/{workspaceId}.json</c> (cli-auth-cli-spec §11.2).
/// </summary>
/// <remarks>
/// Only name matches are cached: every other source costs nothing to ask again. Best effort in both
/// directions, like <see cref="Auth.Discovery.DiscoveryCache"/>: a missing, corrupt or stale entry
/// is a miss, and a failed write is logged and forgotten. The file holds no secret, but it does
/// reveal project keys, so it is written owner-only beside the credentials.
/// </remarks>
internal sealed class ProjectCache(
    XpingHome home,
    IXpingSerializer serializer,
    TimeProvider timeProvider,
    ILogger<ProjectCache> logger)
{
    /// <summary>
    /// The longest a match is used without listing the projects again.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private const int SchemaVersion = 1;
    private const string NameMatch = "name-match";

    /// <summary>
    /// Returns the cached match for <paramref name="assembly"/>, or <see langword="null"/>.
    /// </summary>
    public ProjectSummary? Read(string cloudUrl, string workspaceId, string assembly)
    {
        if (PathFor(cloudUrl, workspaceId) is not { } path)
            return null;

        Entry? entry = ReadFile(path)?.Entries?.GetValueOrDefault(assembly);
        if (entry?.ProjectKey is not { Length: > 0 } key)
            return null;

        // A resolution time in the future means the clock moved back; the age is unknown.
        TimeSpan age = timeProvider.GetUtcNow() - entry.ResolvedAt;
        return age < TimeSpan.Zero || age >= MaxAge ? null : new ProjectSummary(key, assembly, entry.Slug);
    }

    /// <summary>
    /// Records that <paramref name="assembly"/> matched <paramref name="project"/> by name.
    /// </summary>
    public void Write(string cloudUrl, string workspaceId, string assembly, ProjectSummary project)
    {
        Update(cloudUrl, workspaceId, entries =>
            entries[assembly] = new Entry(project.Id, project.Slug, NameMatch, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Forgets the match for <paramref name="assembly"/>: its project no longer answered.
    /// </summary>
    public void Evict(string cloudUrl, string workspaceId, string assembly) =>
        Update(cloudUrl, workspaceId, entries => entries.Remove(assembly));

    private void Update(string cloudUrl, string workspaceId, Action<Dictionary<string, Entry>> change)
    {
        if (PathFor(cloudUrl, workspaceId) is not { } path)
            return;

        var entries = new Dictionary<string, Entry>(ReadFile(path)?.Entries ?? [], StringComparer.Ordinal);
        change(entries);

        try
        {
            home.EnsurePrivate();
            PrivateFiles.WriteAtomically(path, serializer.SerializeToUtf8Bytes(new CacheFile(SchemaVersion, entries)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Project cache {Path} could not be written: {Reason}", path, ex.GetType().Name);
        }
    }

    private CacheFile? ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            CacheFile? file = serializer.Deserialize<CacheFile>(File.ReadAllText(path));
            return file is { SchemaVersion: SchemaVersion } ? file : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            logger.LogInformation("Project cache {Path} could not be read: {Reason}", path, ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// The cache file for one workspace, or <see langword="null"/> when the id cannot be a file name.
    /// </summary>
    /// <remarks>
    /// The id comes from a token claim. A workspace id is a ULID, so anything else is refused here
    /// rather than trusted to name a path.
    /// </remarks>
    private string? PathFor(string cloudUrl, string workspaceId) =>
        workspaceId.Length is > 0 and <= 64 && workspaceId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? Path.Combine(home.ProjectCacheDirectory(cloudUrl), workspaceId + ".json")
            : null;

    private sealed record CacheFile(int SchemaVersion, Dictionary<string, Entry>? Entries);

    private sealed record Entry(string? ProjectKey, string? Slug, string? Source, DateTimeOffset ResolvedAt);
}
