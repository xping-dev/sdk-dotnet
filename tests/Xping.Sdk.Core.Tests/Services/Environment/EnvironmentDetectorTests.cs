/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Environment.Internals;

namespace Xping.Sdk.Core.Tests.Services.Environment;

[Collection("Sequential")]
public sealed class EnvironmentDetectorTests
{
    private static readonly string[] _environmentVariables =
    [
        "CI",
        "GITHUB_ACTIONS",
        "TF_BUILD",
        "JENKINS_URL",
        "GITLAB_CI",
        "CIRCLECI",
        "TRAVIS",
        "TEAMCITY_VERSION",
        "BITBUCKET_PIPELINE_UUID",
        "APPVEYOR",
        "XPING_ENVIRONMENT",
        "ASPNETCORE_ENVIRONMENT",
        "DOTNET_ENVIRONMENT",
    ];

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithDotnetEnvironmentAndDefaultConfiguration_UsesDotnetEnvironment()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var dotnetEnvironment = new EnvRestorer("DOTNET_ENVIRONMENT", "Development");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("Development", info.EnvironmentName);
        Assert.False(info.IsCIEnvironment);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithLocalExecution_MarksDeveloperMachine()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.False(info.IsCIEnvironment);
        Assert.Equal("Local", info.EnvironmentName);
        Assert.Equal("Local", info.CustomProperties["ExecutionContext"]);
        Assert.Equal("true", info.CustomProperties["IsDeveloperMachine"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_OnAnyMachine_CapturesTheLocalOffsetAndZone()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        // The pair is all-or-nothing whatever the machine offers, so this half holds everywhere.
        Assert.Equal(info.UtcOffset.HasValue, info.TimeZoneId != null);

        if (LocalTimeZoneOrNull() is not { } local)
        {
            // A machine with no usable zone — the case the detector is written to tolerate, and the
            // one this test would otherwise crash in while asserting that it works.
            Assert.Null(info.UtcOffset);
            Assert.Null(info.TimeZoneId);
            return;
        }

        // Asserted against the running machine's own zone rather than a fixed value: the point is
        // that the detector reads the real clock, and pinning an expected offset would only test
        // whichever agent happened to run the suite.
        Assert.Equal(local.GetUtcOffset(DateTime.UtcNow), info.UtcOffset);
        Assert.Equal(local.Id, info.TimeZoneId);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_OnConsecutiveCalls_ReportsTheOffsetOfEachCall()
    {
        // The zone is cached; the offset must not be. A suite running either side of a
        // daylight-saving transition depends on the second reading differing from the first, and a
        // cached offset would silently report the same figure forever.
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo first = await detector.BuildEnvironmentInfoAsync();
        EnvironmentInfo second = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal(first.TimeZoneId, second.TimeZoneId);

        if (LocalTimeZoneOrNull() is not { } local)
        {
            Assert.Null(second.UtcOffset);
            return;
        }

        Assert.Equal(local.GetUtcOffset(DateTime.UtcNow), second.UtcOffset);
    }

    /// <summary>
    /// Reads the running machine's time zone the same tolerant way the detector does.
    /// </summary>
    /// <returns>The zone, or <see langword="null"/> when the machine has no usable one.</returns>
    /// <remarks>
    /// A test that reached for <see cref="TimeZoneInfo.Local"/> directly would throw on a machine
    /// with no time zone database — a minimal container, a broken <c>TZ</c> — which is precisely the
    /// case <c>EnvironmentDetector.DetectLocalTimeZone</c> exists to survive. Asserting the contract
    /// with an expression that violates it is how a guard gets deleted as flaky later.
    /// </remarks>
    private static TimeZoneInfo? LocalTimeZoneOrNull()
    {
        try
        {
            return TimeZoneInfo.Local;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithGitHubActions_CapturesNormalizedBranchAndConfiguredCiName()
    {
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubHeadRef = new EnvRestorer("GITHUB_HEAD_REF", "feature/refactor-environment");
        using var githubRefName = new EnvRestorer("GITHUB_REF_NAME", "17/merge");
        using var githubRef = new EnvRestorer("GITHUB_REF", "refs/pull/17/merge");
        using var githubRepository = new EnvRestorer("GITHUB_REPOSITORY", "xping-dev/sdk-dotnet");
        using var githubRunId = new EnvRestorer("GITHUB_RUN_ID", "42");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "abc123");
        using var githubActor = new EnvRestorer("GITHUB_ACTOR", "octocat");

        IEnvironmentDetector detector = CreateDetector(new XpingConfiguration
        {
            CiEnvironmentName = "BuildPipeline",
        });

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.True(info.IsCIEnvironment);
        Assert.Equal("BuildPipeline", info.EnvironmentName);
        Assert.Equal("CI", info.CustomProperties["ExecutionContext"]);
        Assert.Equal("GitHubActions", info.CustomProperties["CIPlatform"]);
        Assert.Equal("feature/refactor-environment", info.CustomProperties["CI.Branch"]);
        Assert.Equal("refs/pull/17/merge", info.CustomProperties["CI.Ref"]);
        Assert.DoesNotContain("IsDeveloperMachine", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_InsideGitRepository_SetsIsInsideGitRepositoryTrue()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["IsInsideGitRepository"]);
        Assert.Equal("main", info.CustomProperties["Git.Branch"]);
        Assert.Equal("a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2", info.CustomProperties["Git.SHA"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_OutsideGitRepository_SetsIsInsideGitRepositoryFalse()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempDir = new TempEmptyDirectory();
        using var dirRestorer = new WorkingDirectoryRestorer(tempDir.Path);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("false", info.CustomProperties["IsInsideGitRepository"]);
        Assert.DoesNotContain("Git.Branch", info.CustomProperties.Keys);
        Assert.DoesNotContain("Git.SHA", info.CustomProperties.Keys);
        Assert.DoesNotContain("Git.Actor", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithDetachedHead_SetsIsDetachedHeadTrueAndNoBranch()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        const string detachedSha = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        tempGit.WriteHead(detachedSha);
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["IsInsideGitRepository"]);
        Assert.Equal("true", info.CustomProperties["IsDetachedHead"]);
        Assert.Equal(detachedSha, info.CustomProperties["Git.SHA"]);
        Assert.DoesNotContain("Git.Branch", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithCIEnvironment_CIBranchPopulatedFromEnvVarNotGit()
    {
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubHeadRef = new EnvRestorer("GITHUB_HEAD_REF", "feature/ci-branch");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "cafebabe");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.True(info.IsCIEnvironment);
        Assert.Equal("feature/ci-branch", info.CustomProperties["CI.Branch"]);
        Assert.Equal("cafebabe", info.CustomProperties["CI.SHA"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_InsideGitRepositoryWithUserConfig_SetsActorFromGitConfig()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "0000000000000000000000000000000000000001");
        tempGit.WriteConfig("[user]\n\tname = Jane Doe\n\temail = jane@example.com\n");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector(new XpingConfiguration { CollectLocalGitAuthor = true });
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("Jane Doe", info.CustomProperties["Git.Actor"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_NoLocalUserConfig_FallsBackToGlobalGitConfig()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "0000000000000000000000000000000000000001");
        // No local [user] in .git/config — only write an unrelated section
        tempGit.WriteConfig("[core]\n\trepositoryformatversion = 0\n");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        // Create a temporary HOME directory that contains only a .gitconfig with the user name
        string tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempHome);
        string? originalHome = System.Environment.GetEnvironmentVariable("HOME");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempHome, ".gitconfig"),
                "[user]\n\tname = Global Author\n\temail = global@example.com\n");
            System.Environment.SetEnvironmentVariable("HOME", tempHome);

            IEnvironmentDetector detector = CreateDetector(new XpingConfiguration { CollectLocalGitAuthor = true });
            EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

            Assert.Equal("Global Author", info.CustomProperties["Git.Actor"]);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("HOME", originalHome);
            try { Directory.Delete(tempHome, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithUserConfigButAuthorCollectionDisabled_OmitsActor()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "0000000000000000000000000000000000000001");
        tempGit.WriteConfig("[user]\n\tname = Jane Doe\n\temail = jane@example.com\n");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector(); // CollectLocalGitAuthor defaults to false
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.DoesNotContain("Git.Actor", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithPackedRefsOnly_ResolvesShaFromPackedRefs()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/release");
        tempGit.WritePackedRefs("# pack-refs with: peeled fully-peeled sorted\naaaa1111bbbb2222cccc3333dddd4444eeee5555 refs/heads/release\n");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("release", info.CustomProperties["Git.Branch"]);
        Assert.Equal("aaaa1111bbbb2222cccc3333dddd4444eeee5555", info.CustomProperties["Git.SHA"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithGitWorktree_DetectsRepositoryViaGitFile()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var mainGit = new TempGitDirectory();
        mainGit.WriteHead("ref: refs/heads/main");
        mainGit.WriteRef("main", "1234567890abcdef1234567890abcdef12345678");

        // Simulate a worktree: create a separate directory with a .git FILE pointing to the main gitdir
        string worktreeRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(worktreeRoot);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(worktreeRoot, ".git"),
                $"gitdir: {mainGit.GitDir}\n");

            using var dirRestorer = new WorkingDirectoryRestorer(worktreeRoot);
            IEnvironmentDetector detector = CreateDetector();
            EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

            Assert.Equal("true", info.CustomProperties["IsInsideGitRepository"]);
            Assert.Equal("main", info.CustomProperties["Git.Branch"]);
        }
        finally
        {
            try { Directory.Delete(worktreeRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithIndexNewerThanRef_SetsStagedChangesTrue()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        tempGit.WriteIndex();
        var past = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        tempGit.SetFileTime(Path.Combine("refs", "heads", "main"), past);
        tempGit.SetFileTime("index", recent); // index newer than ref
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["HasStagedChanges"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithIndexOlderThanRef_SetsStagedChangesFalse()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        tempGit.WriteIndex();
        var past = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        tempGit.SetFileTime("index", past); // index older than ref
        tempGit.SetFileTime(Path.Combine("refs", "heads", "main"), recent);
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("false", info.CustomProperties["HasStagedChanges"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithNoIndexFile_OmitsStagedChanges()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/heads/main");
        tempGit.WriteRef("main", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        // no index file written
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.DoesNotContain("HasStagedChanges", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithNonBranchSymbolicRef_DoesNotSetDetachedHead()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var tempGit = new TempGitDirectory();
        tempGit.WriteHead("ref: refs/tags/v1.0");
        using var dirRestorer = new WorkingDirectoryRestorer(tempGit.WorkingDirectory);

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["IsInsideGitRepository"]);
        Assert.DoesNotContain("IsDetachedHead", info.CustomProperties.Keys);
        Assert.DoesNotContain("Git.Branch", info.CustomProperties.Keys);
        Assert.DoesNotContain("Git.SHA", info.CustomProperties.Keys);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_OnConcurrentCalls_ReturnsIndependentInstances()
    {
        // The detector is a DI singleton so its detection lazies are paid for once. A builder held
        // alongside them would be shared by every caller, and concurrent calls could interleave
        // their Reset()/With...() calls into one another's output.
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo[] built = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => detector.BuildEnvironmentInfoAsync())));

        EnvironmentInfo expected = built[0];

        Assert.All(built, info =>
        {
            Assert.Equal(expected.MachineName, info.MachineName);
            Assert.Equal(expected.OperatingSystem, info.OperatingSystem);
            Assert.Equal(expected.RuntimeVersion, info.RuntimeVersion);
            Assert.Equal(expected.Framework, info.Framework);
            Assert.Equal(expected.EnvironmentName, info.EnvironmentName);
            Assert.Equal(expected.CustomProperties, info.CustomProperties);
        });

        // Every call must own its properties: one instance's dictionary cannot be another's.
        Assert.Equal(built.Length, built.Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private static EnvironmentDetector CreateDetector(XpingConfiguration? configuration = null)
    {
        return new EnvironmentDetector(Options.Create(configuration ?? new XpingConfiguration()));
    }

    private static CompositeDisposable ClearEnvironmentVariables(IEnumerable<string> variableNames)
    {
        List<EnvRestorer> restorers = [];
        foreach (string variableName in variableNames)
        {
            restorers.Add(new EnvRestorer(variableName, null));
        }

        return new CompositeDisposable(restorers);
    }

    private sealed class EnvRestorer : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvRestorer(string name, string? value)
        {
            _name = name;
            _originalValue = System.Environment.GetEnvironmentVariable(name);
            System.Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }

    private sealed class CompositeDisposable(IEnumerable<IDisposable> disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (IDisposable disposable in disposables.Reverse())
            {
                disposable.Dispose();
            }
        }
    }

    private sealed class WorkingDirectoryRestorer : IDisposable
    {
        private readonly string _original;

        public WorkingDirectoryRestorer(string newDirectory)
        {
            _original = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newDirectory);
        }

        public void Dispose() => Directory.SetCurrentDirectory(_original);
    }

    private sealed class TempEmptyDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

        public TempEmptyDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }

    private sealed class TempGitDirectory : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

        public string WorkingDirectory => _root;
        public string GitDir => System.IO.Path.Combine(_root, ".git");

        public TempGitDirectory()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(GitDir, "refs", "heads"));
        }

        public void WriteHead(string content) =>
            File.WriteAllText(System.IO.Path.Combine(GitDir, "HEAD"), content + "\n");

        public void WriteRef(string branch, string sha) =>
            File.WriteAllText(System.IO.Path.Combine(GitDir, "refs", "heads", branch), sha + "\n");

        public void WritePackedRefs(string content) =>
            File.WriteAllText(System.IO.Path.Combine(GitDir, "packed-refs"), content);

        public void WriteConfig(string content) =>
            File.WriteAllText(System.IO.Path.Combine(GitDir, "config"), content);

        public void WriteIndex(string content = "") =>
            File.WriteAllText(System.IO.Path.Combine(GitDir, "index"), content);

        public void SetFileTime(string relativePathInsideGitDir, DateTime utc) =>
            File.SetLastWriteTimeUtc(System.IO.Path.Combine(GitDir, relativePathInsideGitDir), utc);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        }
    }
}
