/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
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
        var reader = new GitRepositoryReader("xping-no-such-git");

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

    [Fact]
    public void GetFirstParent_ObjectIsATree_ReturnsNull()
    {
        Assert.Null(_reader.GetFirstParent(_root, Git("rev-parse HEAD^{tree}")));
    }
}
