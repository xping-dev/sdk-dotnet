/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// Minimal, read-only access to a <c>.git</c> directory: loose and packed refs, and the parents of
/// commits. Callers treat <c>null</c> as "can't prove it".
/// </summary>
/// <remarks>
/// Ref names and SHAs come from CI environment variables and end up in file paths and process
/// arguments, so both are validated first.
/// <para>
/// Commits are read from loose objects. Pack files aren't parsed: a commit found only in a pack
/// (e.g. after <c>git gc --auto</c> ran in a long-lived workspace) is resolved by running
/// <c>git rev-parse</c> instead, once per session at most. Without a usable <c>git</c> the result is
/// <c>null</c>.
/// </para>
/// </remarks>
internal sealed class GitRepositoryReader : IGitRepositoryReader
{
    private const string ParentPrefix = "parent ";

    // A commit header is a few hundred bytes; anything past this is a message we never look at.
    private const int MaxCommitBytes = 64 * 1024;

    // rev-parse answers in milliseconds; this only bounds a hung git (credential prompt, locked repo).
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);

    private readonly string _gitExecutable;

    /// <summary>Creates a reader that falls back to the <c>git</c> on <c>PATH</c>.</summary>
    public GitRepositoryReader()
        : this("git")
    {
    }

    /// <summary>Creates a reader that falls back to the given git executable.</summary>
    internal GitRepositoryReader(string gitExecutable)
    {
        _gitExecutable = gitExecutable;
    }

    /// <inheritdoc/>
    public string? ResolveRef(string repositoryRoot, string refName)
    {
        if (!IsSafeRefName(refName) || !TryGetGitDirectory(repositoryRoot, out string gitDirectory))
            return null;

        string looseRefPath = Path.Combine(gitDirectory, refName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(looseRefPath))
        {
            string sha = File.ReadAllText(looseRefPath).Trim();
            return IsSha(sha) ? sha : null;
        }

        string packedRefsPath = Path.Combine(gitDirectory, "packed-refs");
        if (!File.Exists(packedRefsPath))
            return null;

        foreach (string line in File.ReadAllLines(packedRefsPath))
        {
            // '#' is the header, '^' the peeled target of the annotated tag on the line above.
            if (line.Length == 0 || line[0] == '#' || line[0] == '^')
                continue;

            int space = line.IndexOf(' ');
            if (space > 0 && string.Equals(line.Substring(space + 1), refName, StringComparison.Ordinal))
            {
                string sha = line.Substring(0, space);
                return IsSha(sha) ? sha : null;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public string? GetFirstParent(string repositoryRoot, string commitSha)
    {
        if (!IsSha(commitSha) || !TryGetGitDirectory(repositoryRoot, out string gitDirectory))
            return null;

        // Object files are named in lowercase hex, which is what git (and so GIT_COMMIT) prints.
        string objectPath = Path.Combine(gitDirectory, "objects", commitSha.Substring(0, 2), commitSha.Substring(2));
        string? content;
        try
        {
            content = File.Exists(objectPath) ? ReadCommitObject(objectPath) : null;
        }
        catch (IOException)
        {
            // Packed and pruned between the existence check and the read by a background gc.
            content = null;
        }

        if (content is null)
            return File.Exists(objectPath) ? null : GetFirstParentFromGit(repositoryRoot, commitSha);

        // Header lines are "tree <sha>", then one "parent <sha>" per parent, in order.
        foreach (string line in content.Split('\n'))
        {
            if (line.StartsWith(ParentPrefix, StringComparison.Ordinal))
            {
                string parent = line.Substring(ParentPrefix.Length).Trim();
                return IsSha(parent) ? parent : null;
            }

            if (line.Length == 0)
                break; // End of the header: a root commit.
        }

        return null;
    }

    private string? GetFirstParentFromGit(string repositoryRoot, string commitSha)
    {
        var startInfo = new ProcessStartInfo(_gitExecutable, $"rev-parse --verify --quiet {commitSha}^1^{{commit}}")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Never block on a prompt: rev-parse needs no credentials, but a misconfigured agent could ask.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        var output = new StringBuilder();
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    output.Append(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine(); // Drained so a chatty git can't fill the pipe and stall.

            if (!process.WaitForExit((int)GitTimeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Exited between the timeout and the kill.
                }

                return null;
            }

            process.WaitForExit(); // Flushes the asynchronous output handlers.
            if (process.ExitCode != 0)
                return null;
        }
        catch (Win32Exception)
        {
            return null; // git isn't installed or isn't on PATH.
        }

        string parent = output.ToString().Trim();
        return IsSha(parent) ? parent : null;
    }

    private static string? ReadCommitObject(string objectPath)
    {
        using var file = new FileStream(objectPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Loose objects are zlib streams; DeflateStream wants raw deflate, so skip the 2-byte zlib header.
        if (file.ReadByte() < 0 || file.ReadByte() < 0)
            return null;

        using var inflater = new DeflateStream(file, CompressionMode.Decompress);
        byte[] buffer = new byte[MaxCommitBytes];
        int length = 0;
        int read;
        while (length < buffer.Length && (read = inflater.Read(buffer, length, buffer.Length - length)) > 0)
            length += read;

        int nul = Array.IndexOf(buffer, (byte)0, 0, length);
        if (nul < 0)
            return null;

        string header = Encoding.ASCII.GetString(buffer, 0, nul);
        if (!header.StartsWith("commit ", StringComparison.Ordinal))
            return null;

        return Encoding.UTF8.GetString(buffer, nul + 1, length - nul - 1);
    }

    private static bool TryGetGitDirectory(string repositoryRoot, out string gitDirectory)
    {
        // A '.git' file (worktree, submodule) points elsewhere; not followed.
        gitDirectory = Path.Combine(repositoryRoot, ".git");
        return Directory.Exists(gitDirectory);
    }

    private static bool IsSafeRefName(string refName) =>
        refName.StartsWith("refs/", StringComparison.Ordinal)
        && refName.IndexOf("..", StringComparison.Ordinal) < 0
        && refName.IndexOf('\\') < 0
        && refName.IndexOf(':') < 0
        && refName.IndexOf('\0') < 0;

    /// <summary>A SHA-1 (40) or SHA-256 (64) object name in hex.</summary>
    internal static bool IsSha(string value)
    {
        if (value.Length != 40 && value.Length != 64)
            return false;

        foreach (char c in value)
        {
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                return false;
        }

        return true;
    }
}
