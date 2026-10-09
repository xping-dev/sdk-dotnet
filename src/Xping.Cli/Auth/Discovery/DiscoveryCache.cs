/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Cli.Configuration;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Discovery;

/// <summary>
/// Keeps each Cloud URL's discovery document for up to 24 hours (contract §4.1), in
/// <c>~/.xping/cache/discovery/{key}.json</c>.
/// </summary>
/// <remarks>
/// Best effort in both directions: a missing, unreadable, corrupt or foreign entry is a miss, and a
/// failed write is logged and forgotten. The cache only saves a request; it never decides anything.
/// </remarks>
internal sealed class DiscoveryCache(
    XpingHome home,
    IXpingSerializer serializer,
    TimeProvider timeProvider,
    ILogger<DiscoveryCache> logger)
{
    /// <summary>
    /// The longest a document is used without asking the server again.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private const int SchemaVersion = 1;

    /// <summary>
    /// Returns the cached document for <paramref name="cloudUrl"/>, or <see langword="null"/> when
    /// there is none younger than <see cref="MaxAge"/>.
    /// </summary>
    public DiscoveryResponse? Read(string cloudUrl)
    {
        string path = PathFor(cloudUrl);

        try
        {
            if (!File.Exists(path))
                return null;

            CacheEntry? entry = serializer.Deserialize<CacheEntry>(File.ReadAllText(path));
            TimeSpan age = timeProvider.GetUtcNow() - (entry?.FetchedAt ?? DateTimeOffset.MinValue);

            // A fetch time in the future means the clock moved back; the age is unknown, so the
            // entry is not trusted. The URL check guards against a hash collision.
            if (entry is not { SchemaVersion: SchemaVersion, Document: not null }
                || !string.Equals(entry.CloudUrl, cloudUrl, StringComparison.Ordinal)
                || age < TimeSpan.Zero
                || age >= MaxAge)
            {
                return null;
            }

            return entry.Document;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            logger.LogInformation("Discovery cache entry {Path} could not be read: {Reason}", path, ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Stores <paramref name="document"/> as fetched now.
    /// </summary>
    public void Write(string cloudUrl, DiscoveryResponse document)
    {
        string path = PathFor(cloudUrl);
        var entry = new CacheEntry(SchemaVersion, cloudUrl, timeProvider.GetUtcNow(), document);

        try
        {
            home.EnsurePrivate();
            PrivateFiles.WriteAtomically(path, serializer.SerializeToUtf8Bytes(entry));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Discovery cache entry {Path} could not be written: {Reason}", path, ex.GetType().Name);
        }
    }

    /// <summary>
    /// Removes the cached document for <paramref name="cloudUrl"/>, if there is one.
    /// </summary>
    /// <remarks>Best effort, like the rest of the cache: <c>logout</c> must not fail on it.</remarks>
    public void Delete(string cloudUrl)
    {
        string path = PathFor(cloudUrl);

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Discovery cache entry {Path} could not be deleted: {Reason}", path, ex.GetType().Name);
        }
    }

    private string PathFor(string cloudUrl) =>
        Path.Combine(home.DiscoveryCacheDirectory, CloudUrl.StorageKey(cloudUrl) + ".json");

    private sealed record CacheEntry(int SchemaVersion, string CloudUrl, DateTimeOffset FetchedAt, DiscoveryResponse? Document);
}
