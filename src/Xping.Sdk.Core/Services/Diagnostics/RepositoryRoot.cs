/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System;
using System.IO;
using System.Linq;

namespace Xping.Sdk.Core.Services.Diagnostics;

/// <summary>
/// Finds the root of the repository a path belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Two unrelated features need the same answer for different reasons. The local store puts its
/// session files at the repository root, because flakiness history is only meaningful per repository.
/// <see cref="SourceLocationLookup"/> needs it to turn the absolute path a PDB records into something
/// that reads the same on every machine. Both want "the nearest ancestor that looks like a checkout",
/// so the walk lives in one place.
/// </para>
/// <para>
/// A solution file counts alongside <c>.git</c> so a source tree exported without its history — a
/// vendored copy, a release archive — still resolves rather than falling through to a fallback.
/// </para>
/// </remarks>
internal static class RepositoryRoot
{
    // Bounds the upward walk. A repository nested deeper than this is pathological, and an unbounded
    // walk on a detached or virtualised path would climb to the filesystem root.
    private const int MaxWalkDepth = 32;

    /// <summary>
    /// Returns the nearest ancestor of <paramref name="startDirectory"/> that looks like a repository
    /// root, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="startDirectory">The directory to walk up from.</param>
    /// <returns>The absolute path of the root, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Probes the filesystem, so it only answers for a tree that exists on this machine. A caller
    /// holding a path recorded elsewhere — the build agent's copy of a source file, say — gets
    /// <see langword="null"/> and must have somewhere else to go.
    /// </remarks>
    public static string? Find(string? startDirectory) =>
        FindNearest(startDirectory, directory =>
            // ".git" is a directory in an ordinary clone and a file in a worktree or submodule,
            // where it holds "gitdir: <path>". Both mark a root.
            Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
            File.Exists(Path.Combine(directory.FullName, ".git")) ||
            directory.EnumerateFiles("*.sln").Any() ||
            directory.EnumerateFiles("*.slnx").Any());

    /// <summary>
    /// Returns the nearest existing ancestor of <paramref name="startDirectory"/>, the directory
    /// itself included, that <paramref name="isMatch"/> accepts, or <see langword="null"/>.
    /// </summary>
    /// <param name="startDirectory">The directory to walk up from.</param>
    /// <param name="isMatch">Tests one directory; it may enumerate it.</param>
    /// <returns>The absolute path of the match, or <see langword="null"/>.</returns>
    public static string? FindNearest(string? startDirectory, Func<DirectoryInfo, bool> isMatch)
    {
        if (string.IsNullOrEmpty(startDirectory))
            return null;

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(startDirectory!);
        }
        catch (ArgumentException)
        {
            return null;
        }

        for (int depth = 0; current != null && depth < MaxWalkDepth; depth++, current = current.Parent)
        {
            try
            {
                if (current.Exists && isMatch(current))
                    return current.FullName;
            }
            catch (UnauthorizedAccessException)
            {
                // A directory we cannot enumerate is not one we can use; keep walking.
            }
            catch (IOException)
            {
                // Same reasoning: transient or virtualised paths must not abort resolution.
            }
        }

        return null;
    }
}
