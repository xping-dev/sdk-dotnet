/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Reads refs and commit parents straight from a repository's <c>.git</c> directory, for CI platforms
/// that don't expose the PR head commit in the environment.
/// </summary>
internal interface IGitRepositoryReader
{
    /// <summary>
    /// Resolves a full ref name (e.g. <c>refs/remotes/origin/PR-12</c>) to its commit SHA, from the loose
    /// ref file or <c>packed-refs</c>. Returns <c>null</c> when the ref can't be found or read.
    /// </summary>
    string? ResolveRef(string repositoryRoot, string refName);

    /// <summary>
    /// Returns the first parent of a commit stored as a loose object. Returns <c>null</c> for a root
    /// commit, a packed object, or anything that can't be read.
    /// </summary>
    string? GetFirstParent(string repositoryRoot, string commitSha);
}
