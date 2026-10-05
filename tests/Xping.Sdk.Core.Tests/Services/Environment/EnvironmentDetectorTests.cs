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
    private static readonly HashSet<string> _platformSpecificCommitKeys =
        ["CI.SHA", "CI.SourceVersion", "CI.GitCommit", "CI.CommitSHA", "CI.Commit"];

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
        "GITHUB_SHA",
        "GITHUB_EVENT_NAME",
        "GITHUB_REF",
        "GITHUB_REF_NAME",
        "GITHUB_HEAD_REF",
        "GITHUB_BASE_REF",
        "BUILD_SOURCEVERSION",
        "BUILD_REASON",
        "SYSTEM_PULLREQUEST_SOURCECOMMITID",
        "GIT_COMMIT",
        "CHANGE_ID",
        "ghprbPullId",
        "CI_COMMIT_SHA",
        "CI_MERGE_REQUEST_IID",
        "CI_MERGE_REQUEST_SOURCE_BRANCH_SHA",
        "CIRCLE_SHA1",
        "CIRCLE_PULL_REQUEST",
        "TRAVIS_COMMIT",
        "TRAVIS_PULL_REQUEST",
        "BUILD_VCS_NUMBER",
        "BITBUCKET_COMMIT",
        "BITBUCKET_PR_ID",
        "APPVEYOR_REPO_COMMIT",
        "APPVEYOR_PULL_REQUEST_NUMBER",
        "GITHUB_SERVER_URL",
        "CI_SERVER_URL",
        "BUILD_REPOSITORY_URI",
        "BITBUCKET_GIT_HTTP_ORIGIN",
        "XPING_ENVIRONMENT",
        "ASPNETCORE_ENVIRONMENT",
        "DOTNET_ENVIRONMENT",
    ];

    [Theory]
    [InlineData("DOTNET_ENVIRONMENT")]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    public async Task BuildEnvironmentInfoAsync_WithHostingEnvironmentVariable_IgnoresIt(string variable)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var hostingEnvironment = new EnvRestorer(variable, "Production");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        // These say how the app under test configures itself, not which deployment the suite
        // targeted. A base image that sets one would otherwise stamp every CI run "Production"
        // while developer runs said something else - a CI-vs-local split in a convincing costume.
        Assert.Equal("Default", info.EnvironmentName);
        Assert.False(info.IsCIEnvironment);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithXpingEnvironmentVariableOnly_IgnoresIt()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var xpingEnvironment = new EnvRestorer("XPING_ENVIRONMENT", "Staging");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        // XPING_ENVIRONMENT reaches the detector through configuration, which is bound by
        // AddXping. Reading the process variable here as well would give one value two paths,
        // free to disagree - and they did, whenever a caller passed an instance in code.
        Assert.Equal("Default", info.EnvironmentName);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_InCiWithConfiguredEnvironment_KeepsTheConfiguredName()
    {
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");

        IEnvironmentDetector detector = CreateDetector(new XpingConfiguration
        {
            Environment = "Staging",
        });

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        // The setting a team made deliberately survives the runs they care about most. CI-ness is
        // still recorded, just not by overwriting the name.
        Assert.Equal("Staging", info.EnvironmentName);
        Assert.True(info.IsCIEnvironment);
        Assert.Equal("CI", info.CustomProperties["ExecutionContext"]);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_InCiAndLocally_ReportsTheSameEnvironmentName()
    {
        string ciName;
        using (new EnvRestorer("GITHUB_ACTIONS", "true"))
        {
            IEnvironmentDetector inCi = CreateDetector();
            ciName = (await inCi.BuildEnvironmentInfoAsync()).EnvironmentName;
        }

        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        IEnvironmentDetector onLaptop = CreateDetector();
        EnvironmentInfo local = await onLaptop.BuildEnvironmentInfoAsync();

        // The point of the change: a laptop run and a build-agent run of the same suite are the
        // same environment, so they share a scoring bucket instead of halving each other's evidence.
        Assert.Equal(local.EnvironmentName, ciName);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithLocalExecution_MarksDeveloperMachine()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.False(info.IsCIEnvironment);
        Assert.Equal("Default", info.EnvironmentName);
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
    public async Task BuildEnvironmentInfoAsync_WithGitHubActions_CapturesNormalizedBranchAndCiMetadata()
    {
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubHeadRef = new EnvRestorer("GITHUB_HEAD_REF", "feature/refactor-environment");
        using var githubRefName = new EnvRestorer("GITHUB_REF_NAME", "17/merge");
        using var githubRef = new EnvRestorer("GITHUB_REF", "refs/pull/17/merge");
        using var githubRepository = new EnvRestorer("GITHUB_REPOSITORY", "xping-dev/sdk-dotnet");
        using var githubRunId = new EnvRestorer("GITHUB_RUN_ID", "42");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "abc123");
        using var githubActor = new EnvRestorer("GITHUB_ACTOR", "octocat");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.True(info.IsCIEnvironment);
        Assert.Equal("Default", info.EnvironmentName);
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
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubHeadRef = new EnvRestorer("GITHUB_HEAD_REF", "feature/ci-branch");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "cafebabe");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.True(info.IsCIEnvironment);
        Assert.Equal("feature/ci-branch", info.CustomProperties["CI.Branch"]);
        Assert.Equal("cafebabe", info.CustomProperties["CI.CommitSha"]);
    }

    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_SHA")]
    [InlineData("TF_BUILD", "True", "BUILD_SOURCEVERSION")]
    [InlineData("JENKINS_URL", "https://jenkins.example", "GIT_COMMIT")]
    [InlineData("GITLAB_CI", "true", "CI_COMMIT_SHA")]
    [InlineData("CIRCLECI", "true", "CIRCLE_SHA1")]
    [InlineData("TRAVIS", "true", "TRAVIS_COMMIT")]
    [InlineData("TEAMCITY_VERSION", "2025.07", "BUILD_VCS_NUMBER")]
    [InlineData("BITBUCKET_PIPELINE_UUID", "{uuid}", "BITBUCKET_COMMIT")]
    [InlineData("APPVEYOR", "True", "APPVEYOR_REPO_COMMIT")]
    public async Task CommitShaIsReadFromThePlatformsCommitVariable(
        string platformVariable, string platformValue, string shaVariable)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var platform = new EnvRestorer(platformVariable, platformValue);
        using var sha = new EnvRestorer(shaVariable, "0123abcd");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("0123abcd", info.CustomProperties["CI.CommitSha"]);
        Assert.DoesNotContain(info.CustomProperties.Keys, key => _platformSpecificCommitKeys.Contains(key));
    }

    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_SERVER_URL", "https://GHES.acme.com/", "https://ghes.acme.com")]
    [InlineData("GITLAB_CI", "true", "CI_SERVER_URL", "https://gitlab.acme.com:8443/gitlab", "https://gitlab.acme.com:8443/gitlab")]
    [InlineData("TF_BUILD", "True", "BUILD_REPOSITORY_URI", "https://fabrikam@dev.azure.com/fabrikam/Payments/_git/api", "https://dev.azure.com")]
    [InlineData("BITBUCKET_PIPELINE_UUID", "{uuid}", "BITBUCKET_GIT_HTTP_ORIGIN", "http://bitbucket.org/acme/api", "http://bitbucket.org")]
    public async Task ServerUrlIsReadFromThePlatformsServerVariable(
        string platformVariable, string platformValue, string serverVariable, string serverValue, string expected)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var platform = new EnvRestorer(platformVariable, platformValue);
        using var server = new EnvRestorer(serverVariable, serverValue);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal(expected, info.CustomProperties["CI.ServerUrl"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ghes.acme.com")]
    public async Task ServerUrlIsLeftOutWhenThePlatformNamesNoUsableServer(string? serverValue)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var server = new EnvRestorer("GITHUB_SERVER_URL", serverValue);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.DoesNotContain("CI.ServerUrl", info.CustomProperties.Keys);
    }

    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_EVENT_NAME", "pull_request")]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_EVENT_NAME", "pull_request_target")]
    [InlineData("TF_BUILD", "True", "BUILD_REASON", "PullRequest")]
    [InlineData("JENKINS_URL", "https://jenkins.example", "CHANGE_ID", "17")]
    [InlineData("JENKINS_URL", "https://jenkins.example", "ghprbPullId", "17")]
    [InlineData("GITLAB_CI", "true", "CI_MERGE_REQUEST_IID", "17")]
    [InlineData("CIRCLECI", "true", "CIRCLE_PULL_REQUEST", "https://github.com/o/r/pull/17")]
    [InlineData("TRAVIS", "true", "TRAVIS_PULL_REQUEST", "42")]
    [InlineData("BITBUCKET_PIPELINE_UUID", "{uuid}", "BITBUCKET_PR_ID", "17")]
    [InlineData("APPVEYOR", "True", "APPVEYOR_PULL_REQUEST_NUMBER", "17")]
    public async Task IsPullRequestIsTrueForAPullRequestBuild(
        string platformVariable, string platformValue, string markerVariable, string markerValue)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var platform = new EnvRestorer(platformVariable, platformValue);
        using var marker = new EnvRestorer(markerVariable, markerValue);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["CI.IsPullRequest"]);
    }

    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", null, null)]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_EVENT_NAME", "push")]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_EVENT_NAME", "schedule")]
    [InlineData("TF_BUILD", "True", null, null)]
    [InlineData("TF_BUILD", "True", "BUILD_REASON", "IndividualCI")]
    [InlineData("JENKINS_URL", "https://jenkins.example", null, null)]
    [InlineData("GITLAB_CI", "true", null, null)]
    [InlineData("CIRCLECI", "true", null, null)]
    [InlineData("TRAVIS", "true", null, null)]
    [InlineData("TRAVIS", "true", "TRAVIS_PULL_REQUEST", "false")]
    [InlineData("BITBUCKET_PIPELINE_UUID", "{uuid}", null, null)]
    [InlineData("APPVEYOR", "True", null, null)]
    public async Task IsPullRequestIsFalseForAPushBuild(
        string platformVariable, string platformValue, string? markerVariable, string? markerValue)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var platform = new EnvRestorer(platformVariable, platformValue);
        using var marker = markerVariable is null ? null : new EnvRestorer(markerVariable, markerValue);

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("false", info.CustomProperties["CI.IsPullRequest"]);
    }

    [Theory]
    [InlineData("pull_request_review")]
    [InlineData("pull_request_review_comment")]
    public async Task GitHubEventsRunningOnAPullRequestMergeRefAreFlaggedAsPullRequests(string eventName)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubEventName = new EnvRestorer("GITHUB_EVENT_NAME", eventName);
        using var githubRef = new EnvRestorer("GITHUB_REF", "refs/pull/17/merge");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("true", info.CustomProperties["CI.IsPullRequest"]);
    }

    // CI.CommitSha is the commit the build tested. On these PR builds that is a merge result, and the
    // PR head belongs to PullRequestContext.CommitSha alone. Where the head is an env var, it is set to
    // prove it's ignored (GitHub has the head only in the event payload).
    [Theory]
    [InlineData("GITHUB_ACTIONS", "true", "GITHUB_EVENT_NAME", "pull_request", "GITHUB_SHA", null)]
    [InlineData("TF_BUILD", "True", "BUILD_REASON", "PullRequest", "BUILD_SOURCEVERSION", "SYSTEM_PULLREQUEST_SOURCECOMMITID")]
    [InlineData("GITLAB_CI", "true", "CI_MERGE_REQUEST_IID", "17", "CI_COMMIT_SHA", "CI_MERGE_REQUEST_SOURCE_BRANCH_SHA")]
    public async Task OnAPullRequestBuildCommitShaIsTheBuiltCommitNotThePullRequestHead(
        string platformVariable, string platformValue, string markerVariable, string markerValue,
        string builtShaVariable, string? headVariable)
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var platform = new EnvRestorer(platformVariable, platformValue);
        using var marker = new EnvRestorer(markerVariable, markerValue);
        using var builtSha = new EnvRestorer(builtShaVariable, "merge0123");
        using var head = headVariable is null ? null : new EnvRestorer(headVariable, "head4567");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("merge0123", info.CustomProperties["CI.CommitSha"]);
        Assert.Equal("true", info.CustomProperties["CI.IsPullRequest"]);
    }

    [Fact]
    public async Task GitHubPushSendsThePushedCommit()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubEventName = new EnvRestorer("GITHUB_EVENT_NAME", "push");
        using var githubRef = new EnvRestorer("GITHUB_REF", "refs/heads/main");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "0123abcd");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("0123abcd", info.CustomProperties["CI.CommitSha"]);
        Assert.Equal("false", info.CustomProperties["CI.IsPullRequest"]);
    }

    [Fact]
    public async Task GitHubPullRequestTargetOmitsCommitShaBecauseItIsTheBaseBranchTip()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var githubActions = new EnvRestorer("GITHUB_ACTIONS", "true");
        using var githubEventName = new EnvRestorer("GITHUB_EVENT_NAME", "pull_request_target");
        using var githubRef = new EnvRestorer("GITHUB_REF", "refs/heads/main");
        using var githubSha = new EnvRestorer("GITHUB_SHA", "0123abcd");

        IEnvironmentDetector detector = CreateDetector();
        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.False(info.CustomProperties.ContainsKey("CI.CommitSha"));
        Assert.Equal("true", info.CustomProperties["CI.IsPullRequest"]);
    }

    [Fact]
    public async Task TeamCityOmitsThePullRequestFlag()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var teamCity = new EnvRestorer("TEAMCITY_VERSION", "2025.07");
        using var sha = new EnvRestorer("BUILD_VCS_NUMBER", "0123abcd");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("TeamCity", info.CustomProperties["CIPlatform"]);
        Assert.False(info.CustomProperties.ContainsKey("CI.IsPullRequest"));
    }

    [Fact]
    public async Task GenericCiEmitsNeitherCommitShaNorPullRequestFlag()
    {
        using var clearedCiVariables = ClearEnvironmentVariables(_environmentVariables);
        using var ci = new EnvRestorer("CI", "true");

        IEnvironmentDetector detector = CreateDetector();

        EnvironmentInfo info = await detector.BuildEnvironmentInfoAsync();

        Assert.Equal("Generic", info.CustomProperties["CIPlatform"]);
        Assert.False(info.CustomProperties.ContainsKey("CI.CommitSha"));
        Assert.False(info.CustomProperties.ContainsKey("CI.IsPullRequest"));
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
