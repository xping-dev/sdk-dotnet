/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Diagnostics;

namespace Xping.Sdk.Core.Tests.Diagnostics;

/// <summary>
/// Covers the upward walk that decides which checkout a path belongs to.
/// </summary>
/// <remarks>
/// Shared by two features that fail in different ways when it is wrong: the local store puts its
/// session files at the root it returns, and <see cref="SourceLocationLookup"/> shortens reported
/// paths against it. Each case below builds a real directory tree rather than a stand-in, because
/// what is being tested is what the walk sees on a filesystem.
/// </remarks>
public sealed class RepositoryRootTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "xping-reporoot-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_scratch))
                Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
            // A scratch directory the OS is still holding is not worth failing a test over.
        }
    }

    // ---------------------------------------------------------------------------
    // Nothing to find
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoStartingPointFindsNothing(string? start)
    {
        Assert.Null(RepositoryRoot.Find(start));
    }

    /// <summary>
    /// A tree with no marker anywhere must terminate rather than climbing to the filesystem root and
    /// returning whatever happens to sit there.
    /// </summary>
    [Fact]
    public void ATreeWithNoMarkerFindsNothing()
    {
        string leaf = Directory.CreateDirectory(Path.Combine(_scratch, "a", "b", "c")).FullName;

        Assert.Null(RepositoryRoot.Find(leaf));
    }

    /// <summary>
    /// A path spelled for another platform, or otherwise unusable, is not a walk that can start.
    /// </summary>
    [Fact]
    public void AnUnusablePathFindsNothingRatherThanThrowing()
    {
        Assert.Null(RepositoryRoot.Find("\0invalid"));
    }

    // ---------------------------------------------------------------------------
    // Markers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The ordinary clone: <c>.git</c> is a directory.
    /// </summary>
    [Fact]
    public void AGitDirectoryMarksTheRoot()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        string leaf = Directory.CreateDirectory(Path.Combine(root, "tests", "Unit")).FullName;

        Assert.Equal(root, RepositoryRoot.Find(leaf));
    }

    /// <summary>
    /// In a worktree or a submodule <c>.git</c> is a <i>file</i> holding "gitdir: …". Missing this
    /// would send both callers to the wrong root for every developer using either.
    /// </summary>
    [Fact]
    public void AGitFileMarksTheRootToo()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "worktree")).FullName;
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: /elsewhere/.git/worktrees/wt");
        string leaf = Directory.CreateDirectory(Path.Combine(root, "src")).FullName;

        Assert.Equal(root, RepositoryRoot.Find(leaf));
    }

    /// <summary>
    /// A source tree exported without its history — a vendored copy, a release archive — still
    /// resolves, which is the reason a solution file counts as a marker at all.
    /// </summary>
    [Theory]
    [InlineData("Product.sln")]
    [InlineData("Product.slnx")]
    public void ASolutionFileMarksTheRootWhenThereIsNoGit(string solution)
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "export")).FullName;
        File.WriteAllText(Path.Combine(root, solution), string.Empty);
        string leaf = Directory.CreateDirectory(Path.Combine(root, "tests")).FullName;

        Assert.Equal(root, RepositoryRoot.Find(leaf));
    }

    [Fact]
    public void ADirectoryThatIsItselfTheRootNeedsNoWalk()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        Assert.Equal(root, RepositoryRoot.Find(root));
    }

    /// <summary>
    /// The <i>nearest</i> marker wins. A submodule checked out inside a parent repository must
    /// resolve to itself, or its sessions land in the parent's store and its paths are shortened
    /// against the wrong tree.
    /// </summary>
    [Fact]
    public void TheNearestMarkerWinsOverAnEnclosingOne()
    {
        string outer = Directory.CreateDirectory(Path.Combine(_scratch, "outer")).FullName;
        Directory.CreateDirectory(Path.Combine(outer, ".git"));

        string inner = Directory.CreateDirectory(Path.Combine(outer, "vendor", "inner")).FullName;
        Directory.CreateDirectory(Path.Combine(inner, ".git"));

        string leaf = Directory.CreateDirectory(Path.Combine(inner, "src")).FullName;

        Assert.Equal(inner, RepositoryRoot.Find(leaf));
    }

    /// <summary>
    /// A directory that does not exist is walked through rather than treated as the end of the road:
    /// the parent of a path that was deleted, or never existed on this machine, may still be inside
    /// a checkout.
    /// </summary>
    [Fact]
    public void AMissingLeafStillFindsTheRootAboveIt()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        string missing = Path.Combine(root, "tests", "never", "existed");

        Assert.Equal(root, RepositoryRoot.Find(missing));
    }

    // ---------------------------------------------------------------------------
    // The depth bound
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The walk is bounded, so a marker further up than the bound is not found. Asserted rather than
    /// left implicit: the bound is what stops a detached or virtualised path climbing to the
    /// filesystem root, and it is only correct because a repository that deep is pathological.
    /// </summary>
    [Fact]
    public void AMarkerBeyondTheWalkDepthIsNotFound()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "deep")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        // One level past the 32 the walk allows.
        string leaf = root;
        for (int i = 0; i < 33; i++)
            leaf = Directory.CreateDirectory(Path.Combine(leaf, "d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))).FullName;

        Assert.Null(RepositoryRoot.Find(leaf));
    }

    [Fact]
    public void AMarkerInsideTheWalkDepthIsFound()
    {
        string root = Directory.CreateDirectory(Path.Combine(_scratch, "shallow")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        string leaf = root;
        for (int i = 0; i < 20; i++)
            leaf = Directory.CreateDirectory(Path.Combine(leaf, "d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))).FullName;

        Assert.Equal(root, RepositoryRoot.Find(leaf));
    }
}
