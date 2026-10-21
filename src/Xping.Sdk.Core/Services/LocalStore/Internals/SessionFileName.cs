/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Builds and parses session file names: <c>session-{ticks}-{id}[-{tags}].json.gz</c>.
/// </summary>
/// <remarks>
/// <para>
/// The ticks come first so ordinal name order is chronological order. The tags are a short hash of
/// each assembly the session covers, so retention can count runs per assembly and a scoped read can
/// skip other assemblies' files, both from a directory listing alone. Opening every file on every
/// write is what the tags exist to avoid.
/// </para>
/// <para>
/// A session covering more than <see cref="MaxTags"/> assemblies, or none, is written untagged. Its
/// name says nothing about what it covers, so callers read the file's head to find out.
/// </para>
/// </remarks>
internal static class SessionFileName
{
    /// <summary>The prefix every session file name starts with.</summary>
    public const string Prefix = "session-";

    /// <summary>The suffix every session file name ends with.</summary>
    public const string Suffix = ".json.gz";

    /// <summary>The pattern selecting session files in a directory.</summary>
    public const string SearchPattern = Prefix + "*" + Suffix;

    // Keeps a name well inside path limits on Windows. A host running more test projects than this
    // at once is rare, and its sessions stay correct, just read rather than listed.
    private const int MaxTags = 8;

    private const int TicksLength = 19;
    private const int IdLength = 8;
    private const int TagLength = 8;
    private const char TagSeparator = '.';

    /// <summary>
    /// Builds the file name for a session.
    /// </summary>
    /// <param name="startedAt">When the session started.</param>
    /// <param name="sessionId">The session's id.</param>
    /// <param name="assemblies">The assemblies the session covers.</param>
    /// <returns>The file name, without a directory.</returns>
    public static string Build(DateTime startedAt, Guid sessionId, IReadOnlyList<string> assemblies)
    {
        string ticks = startedAt.Ticks.ToString("D19", CultureInfo.InvariantCulture);
        string id = sessionId.ToString("N").Substring(0, IdLength);

        string tags = assemblies.Count is > 0 and <= MaxTags
            ? "-" + string.Join(TagSeparator.ToString(), assemblies.Select(Tag))
            : string.Empty;

        return Prefix + ticks + "-" + id + tags + Suffix;
    }

    /// <summary>
    /// Returns the tag an assembly is listed under in file names.
    /// </summary>
    /// <param name="assembly">The assembly name.</param>
    /// <returns>Eight lowercase hex digits.</returns>
    /// <remarks>
    /// FNV-1a over the UTF-8 name: stable across processes and platforms, which
    /// <see cref="string.GetHashCode()"/> is not. Two assemblies sharing a tag would only make
    /// retention count them together, keeping fewer of each, never lose a scoped read: the read still
    /// projects the session onto the assembly it asked for.
    /// </remarks>
    public static string Tag(string assembly)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        uint hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(assembly))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the start time from a session file name.
    /// </summary>
    /// <param name="fileName">The file name, without a directory.</param>
    /// <param name="startedAt">The session's start time, in UTC.</param>
    /// <returns><see langword="true"/> when the name carries one.</returns>
    public static bool TryParseStartedAt(string fileName, out DateTime startedAt)
    {
        startedAt = default;

        if (!fileName.StartsWith(Prefix, StringComparison.Ordinal)
            || fileName.Length < Prefix.Length + TicksLength
            || !long.TryParse(
                fileName.Substring(Prefix.Length, TicksLength),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long ticks)
            || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        startedAt = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    /// <summary>
    /// Reads the assembly tags from a session file name.
    /// </summary>
    /// <param name="fileName">The file name, without a directory.</param>
    /// <returns>The tags, or <see langword="null"/> when the name carries none.</returns>
    public static IReadOnlyList<string>? ParseTags(string fileName)
    {
        // session-{ticks}-{id}-{tags}.json.gz: the tags start after the second dash past the prefix.
        int tagsStart = Prefix.Length + TicksLength + 1 + IdLength + 1;

        if (!fileName.EndsWith(Suffix, StringComparison.Ordinal)
            || fileName.Length <= tagsStart + Suffix.Length
            || fileName[tagsStart - 1] != '-')
        {
            return null;
        }

        string[] tags = fileName
            .Substring(tagsStart, fileName.Length - tagsStart - Suffix.Length)
            .Split(TagSeparator);

        foreach (string tag in tags)
        {
            if (tag.Length != TagLength || !tag.All(Uri.IsHexDigit))
                return null;
        }

        return tags;
    }
}
