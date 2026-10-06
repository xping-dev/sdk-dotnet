/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Xping.Cli.Auth;

/// <summary>
/// Creates files under <c>~/.xping</c> that only the current user can read.
/// </summary>
/// <remarks>
/// Every file is created with its final mode before the first byte is written (cli-auth-cli-spec
/// §15.3). Creating it open and tightening it afterwards leaves a window in which another user can
/// open it, and an open handle survives the later chmod.
/// </remarks>
internal static class PrivateFiles
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Creates <paramref name="path"/> and any missing parents as <c>0700</c> on Unix.
    /// </summary>
    /// <remarks>Directories that already exist are left as they are.</remarks>
    public static void EnsureDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        // CreateDirectory applies the mode to the last directory only and creates the missing
        // parents with the default, so ~/.xping itself would end up 0755 when a cache directory
        // below it is the first thing written. Each missing level is created on its own.
        var missing = new Stack<string>();
        for (string? current = Path.GetFullPath(path); current is not null && !Directory.Exists(current); current = Path.GetDirectoryName(current))
            missing.Push(current);

        while (missing.TryPop(out string? directory))
            Directory.CreateDirectory(directory, DirectoryMode);
    }

    /// <summary>
    /// Replaces <paramref name="path"/> with <paramref name="content"/> in one step.
    /// </summary>
    /// <remarks>
    /// Written to a temporary file in the same directory and moved over the target, so a reader never
    /// sees half a file and a crash leaves the previous content in place.
    /// </remarks>
    public static void WriteAtomically(string path, ReadOnlySpan<byte> content)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new ArgumentException("The path has no directory.", nameof(path));

        EnsureDirectory(directory);

        // Unique per write, not per process: two writers of the same file, in one process or two,
        // must never open or delete each other's temporary file. The last move wins whole.
        string temporary = $"{path}.tmp-{Environment.ProcessId}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6))}";

        try
        {
            var options = new FileStreamOptions
            {
                Mode = System.IO.FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };

            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = FileMode;

            using (var stream = new FileStream(temporary, options))
            {
                if (OperatingSystem.IsWindows())
                    RestrictToCurrentUser(temporary);

                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    /// Makes <paramref name="root"/> a <c>0700</c> directory on Unix, creating it or tightening it.
    /// </summary>
    /// <remarks>
    /// <c>~/.xping</c> can exist before the CLI writes to it: the SDK puts its local store at
    /// <c>&lt;repo&gt;/.xping</c>, and a home directory that is a repository (a dotfiles repo) makes
    /// that <c>~/.xping</c>, with the default mode. The store works the same in a <c>0700</c>
    /// directory, so the CLI tightens it rather than write credentials into a directory others can
    /// list (cli-auth-cli-spec §15.3).
    /// </remarks>
    public static void EnsurePrivateRoot(string root)
    {
        EnsureDirectory(root);

        if (OperatingSystem.IsWindows())
            return;

        UnixFileMode mode = File.GetUnixFileMode(root);
        if ((mode & ~DirectoryMode) != 0)
            File.SetUnixFileMode(root, mode & DirectoryMode);
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictToCurrentUser(string path)
    {
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User
            ?? throw new UnreachableException("A Windows process always runs as a user.");

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The write already failed; that is the error worth reporting.
        }
    }
}
