/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Reflection;
using System.Text;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Resolves the on-disk location of the local session store.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
/// <item><description><c>XPING_LOCAL_STORE</c>, when set.</description></item>
/// <item><description>The nearest ancestor of the test assembly containing <c>.git</c>, <c>*.sln</c>
/// or <c>*.slnx</c>, plus <c>/.xping</c>.</description></item>
/// <item><description>A per-repository folder under the user's local application data.</description></item>
/// </list>
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
    internal const string EnvironmentVariableName = "XPING_LOCAL_STORE";
    internal const string StoreDirectoryName = ".xping";
    internal const string SessionsDirectoryName = "sessions";

    // Bounds the upward walk. A repository nested deeper than this is pathological, and an unbounded
    // walk on a detached or virtualised path would climb to the filesystem root.
    private const int MaxWalkDepth = 32;

    /// <summary>
    /// Resolves the store root directory, or <see langword="null"/> when no writable location exists.
    /// </summary>
    /// <param name="startDirectory">
    /// Directory to start the upward walk from. Defaults to the location of the calling assembly.
    /// </param>
    /// <returns>The absolute path of the store root, or <see langword="null"/>.</returns>
    public static string? Resolve(string? startDirectory = null)
    {
        string? explicitPath = System.Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        string? origin = startDirectory ?? GetAssemblyDirectory();

        string? repoRoot = FindRepositoryRoot(origin);
        if (repoRoot != null)
        {
            string candidate = Path.Combine(repoRoot, StoreDirectoryName);

            // A repository root is not automatically writable: read-only checkouts and containers
            // that mount the source tree read-only are common. Without this check the store would
            // silently fail on every write instead of reaching the documented profile fallback.
            if (IsWritable(candidate))
                return candidate;
        }

        return GetProfileFallback(origin);
    }

    /// <summary>
    /// Returns whether a store can actually be created and written at the given path.
    /// </summary>
    private static bool IsWritable(string storeRoot)
    {
        try
        {
            Directory.CreateDirectory(storeRoot);

            // Creating the directory can succeed on a path that still rejects file writes, so probe
            // with a real file rather than trusting the directory alone.
            string probe = Path.Combine(storeRoot, ".write-probe");
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
            string? location = Assembly.GetEntryAssembly()?.Location;

            if (string.IsNullOrEmpty(location))
                location = typeof(LocalStorePathResolver).Assembly.Location;

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

    private static string? FindRepositoryRoot(string? startDirectory)
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
                if (!current.Exists)
                    continue;

                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, ".git")) ||
                    current.EnumerateFiles("*.sln").Any() ||
                    current.EnumerateFiles("*.slnx").Any())
                {
                    return current.FullName;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // A directory we cannot enumerate is not a repository root we can use; keep walking.
            }
            catch (IOException)
            {
                // Same reasoning: transient or virtualised paths must not abort resolution.
            }
        }

        return null;
    }

    private static string? GetProfileFallback(string? origin)
    {
        try
        {
            string baseDirectory = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrEmpty(baseDirectory))
                return null;

            // Key the folder by origin path so unrelated projects falling back to the profile do not
            // pool their history into one meaningless store.
            string key = ComputeShortHash(origin ?? "default");

            return Path.Combine(baseDirectory, "Xping", "stores", key);
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
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
