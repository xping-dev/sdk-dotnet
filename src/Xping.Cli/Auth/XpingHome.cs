/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Configuration;

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
    /// Returns where the projects resolved for <paramref name="cloudUrl"/> are cached, one file per
    /// workspace (cli-auth-cli-spec §11.2).
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL.</param>
    public string ProjectCacheDirectory(string cloudUrl) =>
        Path.Combine(Root, "cache", "projects", CloudUrl.StorageKey(cloudUrl));

    /// <summary>
    /// Gets the file that holds sign-ins when no OS credential store is used (cli-auth-cli-spec §7.5).
    /// </summary>
    public string CredentialsFile => Path.Combine(Root, "credentials.json");

    /// <summary>
    /// Returns <paramref name="path"/> as a user would type it: below the home directory it starts
    /// with <c>~/</c>, so <c>~/.xping/credentials.json</c> rather than an absolute path.
    /// </summary>
    public static string Display(string path) =>
        Display(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// Returns <paramref name="path"/> relative to <paramref name="profile"/> as <c>~/…</c>, or
    /// unchanged when it is not below it.
    /// </summary>
    /// <remarks>
    /// The profile is empty when the process has no home (a container user with no passwd entry and
    /// no <c>HOME</c>); the path is then shown as it is.
    /// </remarks>
    internal static string Display(string path, string profile)
    {
        if (string.IsNullOrEmpty(profile))
            return path;

        string relative = Path.GetRelativePath(profile, path);

        return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : "~/" + relative.Replace(Path.DirectorySeparatorChar, '/');
    }

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
