/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class LocalStorePathResolverTests : IDisposable
{
    private readonly string _temp;

    public LocalStorePathResolverTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "xping-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_temp))
                Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void EnvironmentVariableOverridesEverythingElse()
    {
        // Arrange
        string expected = Path.Combine(_temp, "explicit-store");
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, expected);

        // Act & Assert
        Assert.Equal(expected, LocalStorePathResolver.Resolve(_temp));
    }

    [Fact]
    public void FindsRepositoryRootByGitDirectory()
    {
        // Arrange — nest the start directory several levels below the marker, mirroring the real
        // bin/Debug/net10.0 layout a test assembly lives in.
        string repo = Path.Combine(_temp, "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string start = Path.Combine(repo, "tests", "MyTests", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(start);

        // Act
        string? resolved = LocalStorePathResolver.Resolve(start);

        // Assert
        Assert.Equal(Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName), resolved);
    }

    [Fact]
    public void FindsRepositoryRootBySolutionFile()
    {
        // Arrange
        string repo = Path.Combine(_temp, "repo-sln");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "My.sln"), string.Empty);
        string start = Path.Combine(repo, "src", "bin");
        Directory.CreateDirectory(start);

        // Act
        string? resolved = LocalStorePathResolver.Resolve(start);

        // Assert
        Assert.Equal(Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName), resolved);
    }

    [Fact]
    public void HandlesGitWorktreeFileMarker()
    {
        // Arrange — in a git worktree, .git is a file rather than a directory.
        string repo = Path.Combine(_temp, "worktree");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, ".git"), "gitdir: /elsewhere/.git/worktrees/wt");
        string start = Path.Combine(repo, "tests", "bin");
        Directory.CreateDirectory(start);

        // Act
        string? resolved = LocalStorePathResolver.Resolve(start);

        // Assert
        Assert.Equal(Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName), resolved);
    }

    [Fact]
    public void PicksNearestRootWhenRepositoriesAreNested()
    {
        // Arrange
        string outer = Path.Combine(_temp, "outer");
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        string inner = Path.Combine(outer, "vendor", "inner");
        Directory.CreateDirectory(Path.Combine(inner, ".git"));
        string start = Path.Combine(inner, "bin");
        Directory.CreateDirectory(start);

        // Act
        string? resolved = LocalStorePathResolver.Resolve(start);

        // Assert
        Assert.Equal(Path.Combine(inner, LocalStorePathResolver.StoreDirectoryName), resolved);
    }

    [Fact]
    public void FallsBackToProfileWhenNoRepositoryMarkerExists()
    {
        // Arrange — a directory with no repository markers anywhere above it inside temp.
        string orphan = Path.Combine(_temp, "orphan", "deep");
        Directory.CreateDirectory(orphan);

        // Act
        string? resolved = LocalStorePathResolver.Resolve(orphan);

        // Assert — resolution must still produce something writable rather than giving up.
        Assert.NotNull(resolved);
        Assert.DoesNotContain(orphan, resolved!, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileFallbackIsStableForTheSameOriginAndDistinctForOthers()
    {
        // Arrange
        string a = Path.Combine(_temp, "orphan-a");
        string b = Path.Combine(_temp, "orphan-b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        // Act
        string? firstA = LocalStorePathResolver.Resolve(a);
        string? secondA = LocalStorePathResolver.Resolve(a);
        string? forB = LocalStorePathResolver.Resolve(b);

        // Assert — unrelated projects must not pool their history into one meaningless store.
        Assert.Equal(firstA, secondA);
        Assert.NotEqual(firstA, forB);
    }

    [Fact]
    public void EnsureCreatedMakesRunsDirectoryAndGitIgnore()
    {
        // Arrange
        string root = Path.Combine(_temp, "store");

        // Act
        LocalStorePathResolver.EnsureCreated(root);

        // Assert
        Assert.True(Directory.Exists(LocalStorePathResolver.GetRunsDirectory(root)));
        Assert.Equal("*", File.ReadAllLines(Path.Combine(root, ".gitignore"))[1]);
    }

    [Fact]
    public void EnsureCreatedIsIdempotentAndPreservesAnEditedGitIgnore()
    {
        // Arrange
        string root = Path.Combine(_temp, "store-idempotent");
        LocalStorePathResolver.EnsureCreated(root);
        string path = Path.Combine(root, ".gitignore");
        File.WriteAllText(path, "custom");

        // Act
        LocalStorePathResolver.EnsureCreated(root);

        // Assert — never clobber a file the developer may have edited.
        Assert.Equal("custom", File.ReadAllText(path));
    }
}
