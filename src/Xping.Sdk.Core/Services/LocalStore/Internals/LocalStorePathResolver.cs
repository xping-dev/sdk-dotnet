/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Xping.Sdk.Core.Services.Diagnostics;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Resolves the on-disk location of the local session store.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
/// <item><description>The nearest ancestor of the test assembly containing <c>.git</c>, <c>*.sln</c>
/// or <c>*.slnx</c>, plus <c>/.xping</c>.</description></item>
/// <item><description>A folder under the user's local application data, keyed as below.</description></item>
/// </list>
/// <para>
/// The SDK and the CLI must land on the same fallback folder, although the SDK starts from the test
/// assembly's output directory and the CLI from the current directory. So the folder is keyed by
/// something both walks find, never by the start directory itself: the repository root when there
/// is one (it was found but is not writable), else the nearest directory holding a project file
/// (the test project, for both the SDK's <c>bin/</c> folder and a CLI run from the project).
/// Only a start directory with neither above it keys the folder by itself.
/// </para>
/// <para>
/// An explicit path (<c>XPING_LOCAL_STORE</c> or <see cref="Configuration.XpingConfiguration.LocalStorePath"/>)
/// is read by the callers, never by this class: they read it once and hand it to the store, which
/// passes it to <see cref="ResolveExplicit"/>. A test suite that sets the variable for the tool it
/// tests would otherwise redirect where its own run is recorded.
/// </para>
/// <para>
/// The repository root is preferred over the user profile because flakiness history is only
/// meaningful per repository — a shared profile-wide store would blend unrelated projects and would
/// break running the CLI inside a repo and getting that repo's answer.
/// </para>
/// <para>
/// The walk starts from the assembly location rather than <see cref="Directory.GetCurrentDirectory"/>
/// because the working directory during <c>dotnet test</c> varies between the CLI, IDE runners and CI
/// agents, while the assembly always sits inside the repository.
/// </para>
/// </remarks>
internal static class LocalStorePathResolver
{
    internal const string StoreDirectoryName = ".xping";
    internal const string SessionsDirectoryName = "sessions";

    /// <summary>
    /// Resolves the store root directory, or <see langword="null"/> when no writable location exists.
    /// </summary>
    /// <param name="startDirectory">
    /// Directory to start the upward walk from. Defaults to the folder of the SDK assembly, which sits in the test project's output.
    /// </param>
    /// <returns>The absolute path of the store root, or <see langword="null"/>.</returns>
    public static string? Resolve(string? startDirectory = null)
    {
        string? origin = startDirectory ?? GetAssemblyDirectory();

        string? repoRoot = RepositoryRoot.Find(origin);
        if (repoRoot != null)
        {
            string candidate = Path.Combine(repoRoot, StoreDirectoryName);

            // A repository root is not automatically writable: read-only checkouts and containers
            // that mount the source tree read-only are common. Without this check the store would
            // silently fail on every write instead of reaching the documented profile fallback.
            if (IsWritable(candidate))
                return candidate;
        }

        return GetProfileFallback(repoRoot ?? FindProjectDirectory(origin) ?? origin);
    }

    /// <summary>
    /// Makes an explicit store path (<c>XPING_LOCAL_STORE</c> or <c>LocalStorePath</c>) absolute.
    /// </summary>
    /// <param name="storePath">The path as configured.</param>
    /// <param name="startDirectory">
    /// Directory the walk starts from. Defaults to the folder of the SDK assembly, which sits in the test project's output.
    /// </param>
    /// <returns>The absolute path of the store root.</returns>
    /// <remarks>
    /// <para>
    /// A relative path is read from the repository root, else from the nearest project folder, the
    /// same anchors the profile fallback is keyed by. Read from the current directory instead, the
    /// SDK would put it under the test project's output folder and the CLI under wherever it was
    /// run, so one exported variable would name two stores.
    /// </para>
    /// <para>
    /// With neither above the start directory, the path is read from the current directory, not
    /// from the start directory: that can be a test runner's install folder, which is outside the
    /// repository and often read-only.
    /// </para>
    /// </remarks>
    public static string ResolveExplicit(string storePath, string? startDirectory = null)
    {
        if (IsFullyQualified(storePath))
            return Path.GetFullPath(storePath);

        string? origin = startDirectory ?? GetAssemblyDirectory();
        string anchor = RepositoryRoot.Find(origin)
            ?? FindProjectDirectory(origin)
            ?? Directory.GetCurrentDirectory();

        return Path.GetFullPath(Path.Combine(anchor, storePath));
    }

    /// <summary>
    /// Returns whether <paramref name="path"/> names one location whatever the current directory
    /// and drive are.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.IsPathRooted"/> is not enough on Windows: <c>\store</c> is on the current
    /// drive and <c>C:store</c> is in that drive's current directory, so the testhost and the CLI
    /// would read either as two different stores. netstandard2.0 has no
    /// <c>Path.IsPathFullyQualified</c>, hence the check here.
    /// </remarks>
    internal static bool IsFullyQualified(string path) =>
        IsFullyQualified(path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

    /// <summary>
    /// <see cref="IsFullyQualified(string)"/> for a given platform, so both rules can be tested anywhere.
    /// </summary>
    internal static bool IsFullyQualified(string path, bool windows)
    {
        if (!windows)
            return path.StartsWith("/", StringComparison.Ordinal);

        if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
            return true; // UNC (\\server\share) or a device path (\\?\C:\)

        return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]);

        static bool IsSeparator(char c) => c == '\\' || c == '/';
    }

    private static readonly string[] ProjectFileExtensions = [".csproj", ".fsproj", ".vbproj"];

    /// <summary>
    /// Returns the nearest ancestor of <paramref name="startDirectory"/> (or the directory itself)
    /// that holds a project file, or <see langword="null"/> when there is none.
    /// </summary>
    private static string? FindProjectDirectory(string? startDirectory) =>
        RepositoryRoot.FindNearest(
            startDirectory,
            directory => directory.EnumerateFiles("*proj").Any(file =>
                ProjectFileExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)));

    /// <summary>
    /// Returns whether a store can actually be created and written at the given path.
    /// </summary>
    private static bool IsWritable(string storeRoot)
    {
        try
        {
            Directory.CreateDirectory(storeRoot);

            // Creating the directory can succeed on a path that still rejects file writes, so probe
            // with a real file rather than trusting the directory alone. The name is unique because
            // test projects run in parallel and resolve together at the end of their runs: on a
            // shared name, the loser of the race would see a sharing violation, take the profile
            // fallback and record its run where the CLI never looks.
            string probe = Path.Combine(storeRoot, ".write-probe-" + Guid.NewGuid().ToString("N"));
            using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0);
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or NotSupportedException
                or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the directory holding session files for the given store root.
    /// </summary>
    /// <param name="storeRoot">The store root directory.</param>
    /// <returns>The sessions directory path.</returns>
    /// <remarks>
    /// Sessions sit in their own directory rather than at the store root so the store can grow
    /// sibling state — the CLI's <c>state.json</c> already lives there — without retention having to
    /// tell its own files apart from everything else in the folder.
    /// </remarks>
    public static string GetSessionsDirectory(string storeRoot) =>
        Path.Combine(storeRoot, SessionsDirectoryName);

    /// <summary>
    /// Creates the store directory structure and ensures it is invisible to git.
    /// </summary>
    /// <param name="storeRoot">The store root directory.</param>
    /// <remarks>
    /// Writes a <c>.gitignore</c> containing <c>*</c> inside the store itself. Git honours nested
    /// ignore files, so the store disappears from <c>git status</c> with no action from the developer
    /// and without modifying the repository's own <c>.gitignore</c> — which would show up as an
    /// unexplained diff in their next commit.
    /// </remarks>
    public static void EnsureCreated(string storeRoot)
    {
        Directory.CreateDirectory(GetSessionsDirectory(storeRoot));

        string gitIgnorePath = Path.Combine(storeRoot, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            File.WriteAllText(
                gitIgnorePath,
                "# Created by the Xping SDK. Local test history; not intended for version control." +
                System.Environment.NewLine + "*" + System.Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string? GetAssemblyDirectory()
    {
        try
        {
            // The SDK's own assembly, not the entry assembly: under an external runner
            // (nunit3-console from a tools folder, Visual Studio's testhost.net48.exe) the entry
            // assembly sits in the runner's install folder, outside the repository. The SDK is
            // always copied into the test project's output. The CLI always passes a start directory.
            string? location = typeof(LocalStorePathResolver).Assembly.Location;

            // Single-file and some dynamic hosts report an empty location.
            if (string.IsNullOrEmpty(location))
                return Directory.GetCurrentDirectory();

            return Path.GetDirectoryName(location);
        }
        catch (NotSupportedException)
        {
            return Directory.GetCurrentDirectory();
        }
    }

    private static string? GetProfileFallback(string? key)
    {
        try
        {
            string baseDirectory = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrEmpty(baseDirectory))
                return null;

            // Keyed so unrelated projects falling back to the profile do not pool their history into
            // one meaningless store.
            return Path.Combine(baseDirectory, "Xping", "stores", ComputeShortHash(NormalizeKey(key)));
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Spells one directory one way, so the SDK and the CLI hash the same string for it.
    /// </summary>
    /// <remarks>
    /// The two sides reach the directory differently: the SDK from an absolute assembly path, the CLI
    /// from <c>--directory</c> as typed, which can be relative, end in a separator, or differ in case
    /// from the path an IDE used. Windows and macOS file systems ignore case by default, so case is
    /// folded there; Linux keeps it, because two spellings there are two directories.
    /// </remarks>
    internal static string NormalizeKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "default";

        string normalized;
        try
        {
            normalized = Path.GetFullPath(key);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException
                                       or System.Security.SecurityException)
        {
            normalized = key!;
        }

        normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        bool caseInsensitive =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        return caseInsensitive ? normalized.ToUpperInvariant() : normalized;
    }

    private static string ComputeShortHash(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));

        var sb = new StringBuilder(16);
        for (int i = 0; i < 8; i++)
            sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

        return sb.ToString();
    }
}
