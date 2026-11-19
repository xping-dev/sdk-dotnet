/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.InteropServices;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Sets XPING_LOCAL_STORE, which is process-wide state, to prove the resolver ignores it.
[Collection("Sequential")]
public sealed class LocalStorePathResolverTests : IDisposable
{
    private const string StoreVariable = "XPING_LOCAL_STORE";

    private readonly string _temp;

    public LocalStorePathResolverTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "xping-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        System.Environment.SetEnvironmentVariable(StoreVariable, null);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(StoreVariable, null);

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
    public void TheEnvironmentVariableDoesNotMoveTheResolvedStore()
    {
        // The store resolves lazily, at the end of the run. A suite that sets the variable for the
        // tool it tests must not redirect where its own run is recorded, so the variable is read
        // once, into the configuration, and never here.
        string repo = Path.Combine(_temp, "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        System.Environment.SetEnvironmentVariable(StoreVariable, Path.Combine(_temp, "fixture-store"));

        Assert.Equal(
            Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName),
            LocalStorePathResolver.Resolve(repo));
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

    // The SDK starts from the test assembly's output directory and the CLI from the current
    // directory. Keyed by the start directory, the two resolved different fallback folders, so
    // `xping report` and `xping where` never found what the SDK wrote.
    [Fact]
    public void WithoutARepositoryTheSdkAndTheCliShareTheProjectsFallback()
    {
        string project = Path.Combine(_temp, "Shop.Tests");
        string output = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(project, "Shop.Tests.csproj"), "<Project />");

        string? sdk = LocalStorePathResolver.Resolve(output);
        string? cli = LocalStorePathResolver.Resolve(project);

        Assert.NotNull(sdk);
        Assert.Equal(sdk, cli);
    }

    [Theory]
    [InlineData("Shop.Tests.fsproj")]
    [InlineData("Shop.Tests.vbproj")]
    public void AnyProjectFileKeysTheFallback(string projectFile)
    {
        string project = Path.Combine(_temp, "Shop.Tests");
        string output = Path.Combine(project, "bin", "Release", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(project, projectFile), "<Project />");

        Assert.Equal(LocalStorePathResolver.Resolve(project), LocalStorePathResolver.Resolve(output));
    }

    [Fact]
    public void TwoProjectsWithoutARepositoryKeepSeparateFallbacks()
    {
        foreach (string name in new[] { "A.Tests", "B.Tests" })
        {
            Directory.CreateDirectory(Path.Combine(_temp, name, "bin"));
            File.WriteAllText(Path.Combine(_temp, name, name + ".csproj"), "<Project />");
        }

        Assert.NotEqual(
            LocalStorePathResolver.Resolve(Path.Combine(_temp, "A.Tests", "bin")),
            LocalStorePathResolver.Resolve(Path.Combine(_temp, "B.Tests", "bin")));
    }

    // A read-only checkout: the root is found from both sides but cannot hold the store, so the
    // fallback is keyed by the root, which the CLI finds from anywhere inside it.
    [Fact]
    public void AnUnwritableRepositoryKeysTheFallbackByItsRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // chmod semantics differ; the behaviour is verified on Unix CI.

        string repo = Path.Combine(_temp, "readonly-repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string project = Path.Combine(repo, "tests", "Shop.Tests");
        string output = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(project, "Shop.Tests.csproj"), "<Project />");

        File.SetUnixFileMode(
            repo,
            UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            string? sdk = LocalStorePathResolver.Resolve(output);

            Assert.NotNull(sdk);
            Assert.NotEqual(Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName), sdk);
            Assert.Equal(sdk, LocalStorePathResolver.Resolve(repo));
            Assert.Equal(sdk, LocalStorePathResolver.Resolve(Path.Combine(repo, "tests")));
        }
        finally
        {
            File.SetUnixFileMode(repo, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // Test projects run in parallel and resolve together at the end of their runs. With one shared
    // probe file, a run that lost the race took the profile fallback while its siblings and the CLI
    // used the repository root.
    [Fact]
    public void AProbeHeldByAnotherRunDoesNotSendThisOneToTheFallback()
    {
        string repo = Path.Combine(_temp, "busy-repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string store = Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName);
        Directory.CreateDirectory(store);

        using (new FileStream(Path.Combine(store, ".write-probe"), FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Assert.Equal(store, LocalStorePathResolver.Resolve(repo));
        }
    }

    // `--directory .` reaches the resolver as typed; the SDK always passes an absolute path.
    [Fact]
    public void ARelativeKeyIsTheDirectoryItNames()
    {
        Assert.Equal(
            LocalStorePathResolver.NormalizeKey(Directory.GetCurrentDirectory()),
            LocalStorePathResolver.NormalizeKey("."));
    }

    // Only when the assembly reports no location at all; every such run shares one folder.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoKeyIsTheDefaultKey(string? key)
    {
        Assert.Equal("default", LocalStorePathResolver.NormalizeKey(key));
    }

    // A path the platform cannot expand is still a key; it is hashed as it came.
    [Fact]
    public void APathThatCannotBeExpandedIsKeptAsItCame()
    {
        const string key = "bad\0path";
        bool folds = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        Assert.Equal(folds ? key.ToUpperInvariant() : key, LocalStorePathResolver.NormalizeKey(key));
    }

    [Fact]
    public void ATrailingSeparatorDoesNotMakeASecondKey()
    {
        string project = Path.Combine(_temp, "Shop.Tests");

        Assert.Equal(
            LocalStorePathResolver.NormalizeKey(project),
            LocalStorePathResolver.NormalizeKey(project + Path.DirectorySeparatorChar));
    }

    // An IDE and a shell can spell one directory in different case, and on Windows and macOS that is
    // still one directory. On Linux it is two.
    [Fact]
    public void KeyCaseIsFoldedOnlyWhereTheFileSystemIgnoresIt()
    {
        string project = Path.Combine(_temp, "Shop.Tests");
        bool folds = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        bool same = LocalStorePathResolver.NormalizeKey(project) ==
            LocalStorePathResolver.NormalizeKey(project.ToUpperInvariant());

        Assert.Equal(folds, same);
    }

    [Fact]
    public void EnsureCreatedMakesSessionsDirectoryAndGitIgnore()
    {
        // Arrange
        string root = Path.Combine(_temp, "store");

        // Act
        LocalStorePathResolver.EnsureCreated(root);

        // Assert
        Assert.True(Directory.Exists(LocalStorePathResolver.GetSessionsDirectory(root)));

        // The store is one tier now; nothing should re-create the retired runs directory.
        Assert.False(Directory.Exists(Path.Combine(root, "runs")));
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
