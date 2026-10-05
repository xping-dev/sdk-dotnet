/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Xping.Sdk.Core.Services.PullRequest.Internals;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

/// <summary>
/// Runs against throwaway repositories built with the real <c>git</c> CLI, so the reader is checked
/// against the on-disk format git actually writes, not one we assumed.
/// </summary>
public sealed class GitRepositoryReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-git-" + Guid.NewGuid().ToString("N"));
    private readonly GitRepositoryReader _reader = new();

    public GitRepositoryReaderTests()
    {
        Directory.CreateDirectory(_root);
        Git("init -q -b main");
        Commit("base");
    }

    public void Dispose()
    {
        // Git marks object files read-only, which Directory.Delete refuses on Windows.
        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private string Git(string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Isolate from the developer's config: no signing, hooks or templates, a fixed identity.
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "no-global-config");
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_AUTHOR_NAME"] = startInfo.Environment["GIT_COMMITTER_NAME"] = "Test";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";

        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {arguments} failed: {error}");
        return output.Trim();
    }

    private string Commit(string name)
    {
        File.WriteAllText(Path.Combine(_root, name + ".txt"), name);
        Git($"add {name}.txt");
        Git($"commit -q -m {name}");
        return Git("rev-parse HEAD");
    }

    /// <summary>
    /// Reproduces a Jenkins merge strategy checkout: the PR head is checked out, fetched into
    /// <c>refs/remotes/origin/PR-17</c>, and the target branch is merged into it.
    /// </summary>
    private (string Head, string Merge) PullRequestMergedWithTarget()
    {
        Git("checkout -q -b pr");
        string head = Commit("pr");
        Git($"update-ref refs/remotes/origin/PR-17 {head}");
        Git("checkout -q main");
        Commit("target");
        Git("checkout -q pr");
        Git("merge -q --no-ff --no-edit main");
        return (head, Git("rev-parse HEAD"));
    }

    private const string UnknownSha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>Writes a loose object file with arbitrary (possibly malformed) content.</summary>
    private void WriteLooseObject(string sha, string content)
    {
        string directory = Path.Combine(_root, ".git", "objects", sha.Substring(0, 2));
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, sha.Substring(2)));
        using var zlib = new ZLibStream(file, CompressionLevel.Fastest);
        zlib.Write(Encoding.UTF8.GetBytes(content));
    }

    private void WriteRef(string refName, string content)
    {
        string path = Path.Combine(_root, ".git", refName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>A stand-in for git: a shell script with the given body. Unix only, like CI.</summary>
    private string FakeGit(string body)
    {
        string path = Path.Combine(_root, "fake-git");
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    // ---------------------------------------------------------------------------
    // ResolveRef
    // ---------------------------------------------------------------------------

    [Fact]
    public void ResolveRef_LooseRef_ReturnsItsCommit()
    {
        string head = Git("rev-parse HEAD");
        Git($"update-ref refs/remotes/origin/PR-17 {head}");

        Assert.Equal(head, _reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_RefOnlyInPackedRefs_ReturnsItsCommit()
    {
        string head = Git("rev-parse HEAD");
        Git($"update-ref refs/remotes/origin/PR-17 {head}");
        Git("pack-refs --all");
        Assert.False(File.Exists(Path.Combine(_root, ".git", "refs", "remotes", "origin", "PR-17")));

        Assert.Equal(head, _reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_LooseRefDeletedAfterTheExistenceCheck_ReadsPackedRefs()
    {
        // gc --auto packs the ref and deletes the loose file while we read it. A dangling symlink
        // reproduces that deterministically: File.Exists says yes, the read finds nothing.
        string head = Git("rev-parse HEAD");
        Git($"update-ref refs/remotes/origin/PR-17 {head}");
        Git("pack-refs --all");
        string looseRef = Path.Combine(_root, ".git", "refs", "remotes", "origin", "PR-17");
        Directory.CreateDirectory(Path.GetDirectoryName(looseRef)!);
        File.CreateSymbolicLink(looseRef, Path.Combine(_root, "deleted-by-gc"));
        Assert.True(File.Exists(looseRef));

        Assert.Equal(head, _reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_SymbolicLooseRef_ReturnsNull()
    {
        WriteRef("refs/remotes/origin/PR-17", "ref: refs/heads/main\n");

        Assert.Null(_reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_RefNotInPackedRefs_ReturnsNull()
    {
        Git("pack-refs --all");

        Assert.Null(_reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_PackedRefsSkipsHeaderAndPeeledLines()
    {
        string head = Git("rev-parse HEAD");
        File.WriteAllText(
            Path.Combine(_root, ".git", "packed-refs"),
            $"# pack-refs with: peeled fully-peeled sorted\n\n{UnknownSha} refs/tags/v1\n^{head}\n{head} refs/remotes/origin/PR-17\n");

        Assert.Equal(head, _reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_PackedRefWithMalformedSha_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_root, ".git", "packed-refs"), "not-a-sha refs/remotes/origin/PR-17\n");

        Assert.Null(_reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Fact]
    public void ResolveRef_RefMissing_ReturnsNull()
    {
        Assert.Null(_reader.ResolveRef(_root, "refs/remotes/origin/PR-17"));
    }

    [Theory]
    [InlineData("refs/remotes/origin/../../HEAD")]
    [InlineData("HEAD")]
    [InlineData("/etc/passwd")]
    [InlineData("refs\\remotes\\origin\\PR-17")]
    public void ResolveRef_UnsafeRefName_ReturnsNull(string refName)
    {
        Assert.Null(_reader.ResolveRef(_root, refName));
    }

    [Fact]
    public void ResolveRef_GitIsAFile_ReturnsNull()
    {
        string worktree = Path.Combine(_root, "worktree");
        Git($"worktree add -q {worktree}");
        Git($"update-ref refs/remotes/origin/PR-17 {Git("rev-parse HEAD")}");

        Assert.Null(_reader.ResolveRef(worktree, "refs/remotes/origin/PR-17"));
    }

    // ---------------------------------------------------------------------------
    // GetFirstParent
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetFirstParent_MergeOfTheTargetIntoThePullRequest_ReturnsThePullRequestHead()
    {
        var (head, merge) = PullRequestMergedWithTarget();

        Assert.Equal(head, _reader.GetFirstParent(_root, merge));
        Assert.Equal(Git("rev-parse HEAD^1"), head);
    }

    [Fact]
    public void GetFirstParent_OrdinaryCommit_ReturnsItsParent()
    {
        string parent = Git("rev-parse HEAD");
        string child = Commit("child");

        Assert.Equal(parent, _reader.GetFirstParent(_root, child));
    }

    [Fact]
    public void GetFirstParent_RootCommit_ReturnsNull()
    {
        Assert.Null(_reader.GetFirstParent(_root, Git("rev-parse HEAD")));
    }

    [Fact]
    public void GetFirstParent_MergeCommitPackedByGc_FallsBackToGit()
    {
        // A long-lived Jenkins workspace crosses gc.auto's threshold and the merge commit gets packed.
        var (head, merge) = PullRequestMergedWithTarget();
        Git("gc -q");
        Assert.False(File.Exists(Path.Combine(_root, ".git", "objects", merge.Substring(0, 2), merge.Substring(2))));

        Assert.Equal(head, _reader.GetFirstParent(_root, merge));
    }

    [Fact]
    public void GetFirstParent_PackedObjectAndNoGitOnPath_ReturnsNull()
    {
        var (_, merge) = PullRequestMergedWithTarget();
        Git("gc -q");
        var reader = new GitRepositoryReader("xping-no-such-git", TimeSpan.FromSeconds(5));

        Assert.Null(reader.GetFirstParent(_root, merge));
    }

    [Fact]
    public void GetFirstParent_PackedRootCommit_ReturnsNull()
    {
        string root = Git("rev-parse HEAD");
        Git("gc -q");

        Assert.Null(_reader.GetFirstParent(_root, root));
    }

    [Fact]
    public void GetFirstParent_LooseObjectDeletedMidRead_FallsBackToGit()
    {
        // gc packs and prunes the merge commit while we open it. A dangling symlink reproduces that:
        // the path is there, opening it finds nothing.
        var (head, merge) = PullRequestMergedWithTarget();
        Git("gc -q");
        string looseObject = Path.Combine(_root, ".git", "objects", merge.Substring(0, 2), merge.Substring(2));
        Directory.CreateDirectory(Path.GetDirectoryName(looseObject)!);
        File.CreateSymbolicLink(looseObject, Path.Combine(_root, "pruned-by-gc"));

        Assert.Equal(head, _reader.GetFirstParent(_root, merge));
    }

    [Fact]
    public void GetFirstParent_EmptyObjectFile_ReturnsNull()
    {
        string directory = Path.Combine(_root, ".git", "objects", UnknownSha.Substring(0, 2));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, UnknownSha.Substring(2)), []);

        Assert.Null(_reader.GetFirstParent(_root, UnknownSha));
    }

    [Theory]
    [InlineData("commit 12 without a nul")]
    [InlineData("blob 4\0data")]
    [InlineData("commit 40\0tree abc\nparent not-a-sha\n\nmessage")]
    public void GetFirstParent_MalformedLooseObject_ReturnsNull(string content)
    {
        WriteLooseObject(UnknownSha, content);

        Assert.Null(_reader.GetFirstParent(_root, UnknownSha));
    }

    [Fact]
    public void GetFirstParent_GitPrintsSomethingOtherThanASha_ReturnsNull()
    {
        var reader = new GitRepositoryReader(FakeGit("echo not-a-sha"), TimeSpan.FromSeconds(5));

        Assert.Null(reader.GetFirstParent(_root, UnknownSha));
    }

    [Fact]
    public void GetFirstParent_GitHangs_GivesUpAfterTheTimeout()
    {
        var reader = new GitRepositoryReader(FakeGit("exec sleep 30"), TimeSpan.FromMilliseconds(200));
        var stopwatch = Stopwatch.StartNew();

        Assert.Null(reader.GetFirstParent(_root, UnknownSha));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public void GetFirstParent_CommitNotInTheRepository_ReturnsNull()
    {
        Assert.Null(_reader.GetFirstParent(_root, "0123456789abcdef0123456789abcdef01234567"));
    }

    [Theory]
    [InlineData("not-a-sha")]
    [InlineData("../../../../../../etc/passwd/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("4b825dc642cb6eb9a060e54bf8d69288fbee490")]
    public void GetFirstParent_NotASha_ReturnsNull(string commitSha)
    {
        Assert.Null(_reader.GetFirstParent(_root, commitSha));
    }

    // ---------------------------------------------------------------------------
    // IsSha
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456g", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456G", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456/", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456:", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456`", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456@", false)]
    public void IsSha_AcceptsOnlyFullHexObjectNames(string value, bool expected)
    {
        Assert.Equal(expected, GitRepositoryReader.IsSha(value));
    }

    [Fact]
    public void GetFirstParent_ObjectIsATree_ReturnsNull()
    {
        Assert.Null(_reader.GetFirstParent(_root, Git("rev-parse HEAD^{tree}")));
    }
}
