/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// The per-user directory, <c>~/.xping</c>, that holds credentials, caches and locks.
/// </summary>
/// <remarks>
/// Unrelated to the local run store (<c>&lt;repo&gt;/.xping/</c>). Injected so tests point it at a
/// scratch directory and never touch a developer's real sign-in.
/// </remarks>
/// <param name="root">The directory itself.</param>
internal sealed class XpingHome(string root)
{
    /// <summary>
    /// Gets the directory itself.
    /// </summary>
    public string Root { get; } = root;

    /// <summary>
    /// Gets where the discovery documents are cached (cli-auth-cli-spec §14.4).
    /// </summary>
    public string DiscoveryCacheDirectory => Path.Combine(Root, "cache", "discovery");

    /// <summary>
    /// Creates <see cref="Root"/> readable only by the user, or tightens it if it exists.
    /// </summary>
    /// <remarks>Called before anything is written below it.</remarks>
    public void EnsurePrivate() => PrivateFiles.EnsurePrivateRoot(Root);

    /// <summary>
    /// Returns the home of the user running the process.
    /// </summary>
    public static XpingHome ForCurrentUser() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xping"));
}
