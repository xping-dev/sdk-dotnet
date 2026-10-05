/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.PullRequest.Internals;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

public sealed class JenkinsPullRequestDetectorTests
{
    private const string Workspace = "/var/jenkins/workspace/api_PR-17";
    private const string HeadSha = "1111111111111111111111111111111111111111";
    private const string MergeSha = "2222222222222222222222222222222222222222";
    private const string OtherSha = "3333333333333333333333333333333333333333";

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Variables of a GitHub Branch Source PR build using the head strategy (GIT_COMMIT is the PR head).
    /// Tests override or remove entries as needed.
    /// </summary>
    private static Dictionary<string, string?> ValidVariables() => new()
    {
        ["CHANGE_ID"] = "17",
        ["CHANGE_URL"] = "https://github.com/acme/api-service/pull/17",
        ["CHANGE_BRANCH"] = "feature/login",
        ["CHANGE_TARGET"] = "main",
        ["CHANGE_AUTHOR"] = "janedev",
        ["BRANCH_NAME"] = "PR-17",
        ["WORKSPACE"] = Workspace,
        ["GIT_COMMIT"] = HeadSha,
    };

    /// <summary>A repository whose <c>refs/remotes/origin/PR-17</c> points at <see cref="HeadSha"/>.</summary>
    private static FakeGitRepositoryReader Repository() => new()
    {
        Refs = { [(Workspace, "refs/remotes/origin/PR-17")] = HeadSha },
    };

    private static JenkinsPullRequestDetector CreateDetector(
        Dictionary<string, string?> variables, IGitRepositoryReader? git = null)
    {
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>()))
           .Returns((string name) => variables.TryGetValue(name, out string? value) ? value : null);
        return new JenkinsPullRequestDetector(
            env.Object, git ?? Repository(), NullLogger<JenkinsPullRequestDetector>.Instance);
    }

    private static PullRequestContext? Detect(Dictionary<string, string?> variables, IGitRepositoryReader? git = null) =>
        CreateDetector(variables, git).Detect();

    private sealed class FakeGitRepositoryReader : IGitRepositoryReader
    {
        public Dictionary<(string Root, string Ref), string> Refs { get; } = [];
        public Dictionary<(string Root, string Sha), string> FirstParents { get; } = [];
        public bool Throws { get; init; }

        public string? ResolveRef(string repositoryRoot, string refName) =>
            Throws ? throw new IOException("boom")
                   : Refs.TryGetValue((repositoryRoot, refName), out string? sha) ? sha : null;

        public string? GetFirstParent(string repositoryRoot, string commitSha) =>
            FirstParents.TryGetValue((repositoryRoot, commitSha), out string? sha) ? sha : null;
    }

    // ---------------------------------------------------------------------------
    // Detect — field mapping per host
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_GitHubPullRequest_MapsEveryField()
    {
        // Act
        var result = Detect(ValidVariables());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.GitHub, result.Platform);
        Assert.Equal("acme", result.RepositoryOwner);
        Assert.Equal("api-service", result.RepositoryName);
        Assert.Equal(17, result.PullRequestNumber);
        Assert.Equal(HeadSha, result.CommitSha);
        Assert.Equal("main", result.BaseBranch);
        Assert.Equal("feature/login", result.HeadBranch);
        Assert.Equal("janedev", result.Author);
    }

    [Fact]
    public void Detect_GitLabNestedGroup_KeepsTheGroupsInTheOwner()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_URL"] = "https://gitlab.com/group/sub/project/-/merge_requests/17";

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.GitLab, result.Platform);
        Assert.Equal("group/sub", result.RepositoryOwner);
        Assert.Equal("project", result.RepositoryName);
    }

    [Theory]
    [InlineData("https://dev.azure.com/fabrikam/Payments/_git/api-service/pullrequest/17")]
    [InlineData("https://fabrikam.visualstudio.com/Payments/_git/api-service/pullrequest/17")]
    [InlineData("https://fabrikam.visualstudio.com/DefaultCollection/Payments/_git/api-service/pullrequest/17")]
    public void Detect_AzureReposPullRequest_UsesOrganizationAndProjectAsOwner(string changeValue)
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_URL"] = changeValue;

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.AzureDevOps, result.Platform);
        Assert.Equal("fabrikam/Payments", result.RepositoryOwner);
        Assert.Equal("api-service", result.RepositoryName);
    }

    [Fact]
    public void Detect_AzureProjectNameWithSpaces_IsDecoded()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_URL"] = "https://dev.azure.com/fabrikam/Online%20Payments/_git/api-service/pullrequest/17";

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fabrikam/Online Payments", result.RepositoryOwner);
    }

    [Fact]
    public void Detect_AuthorMissing_StillDetects()
    {
        // Arrange
        var variables = ValidVariables();
        variables.Remove("CHANGE_AUTHOR");

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Author);
    }

    // ---------------------------------------------------------------------------
    // Detect — not a detectable PR
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_ChangeIdNotSet_ReturnsNull()
    {
        // Arrange
        var variables = ValidVariables();
        variables.Remove("CHANGE_ID");

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    public void Detect_ChangeIdNotAPullRequestNumber_ReturnsNull(string changeId)
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_ID"] = changeId;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("https://github.example.com/acme/api-service/pull/17")]
    [InlineData("https://gitlab.example.com/acme/api-service/-/merge_requests/17")]
    [InlineData("https://tfs.contoso.local/DefaultCollection/Payments/_git/api-service/pullrequest/17")]
    [InlineData("https://bitbucket.org/acme/api-service/pull-requests/17")]
    [InlineData("https://github.com/acme/api-service/issues/17")]
    [InlineData("https://github.com/acme/pull/17")]
    [InlineData("https://gitlab.com/project/-/merge_requests/17")]
    [InlineData("https://dev.azure.com/fabrikam/Payments/api-service/pullrequest/17")]
    [InlineData("https://github.com/acme//pull/17")]
    [InlineData("https://github.com/acme/api-service/pull/abc")]
    [InlineData("https://gitlab.com/group//-/merge_requests/17")]
    [InlineData("https://gitlab.com/acme/api-service/-/issues/17")]
    [InlineData("https://dev.azure.com/fabrikam/Payments/_git/api-service/pulls/17")]
    [InlineData("https://fabrikam.visualstudio.com/Payments/_git/api-service/pullrequest")]
    [InlineData("https://eu.fabrikam.visualstudio.com/Payments/_git/api-service/pullrequest/17")]
    [InlineData("not a url")]
    public void Detect_ChangeUrlNotASupportedPullRequestUrl_ReturnsNull(string changeValue)
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_URL"] = changeValue;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_ChangeUrlNamesAnotherPullRequest_ReturnsNull()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CHANGE_URL"] = "https://github.com/acme/api-service/pull/18";

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("CHANGE_URL")]
    [InlineData("CHANGE_BRANCH")]
    [InlineData("CHANGE_TARGET")]
    [InlineData("BRANCH_NAME")]
    [InlineData("WORKSPACE")]
    [InlineData("GIT_COMMIT")]
    public void Detect_RequiredVariableMissing_ReturnsNull(string variable)
    {
        // Arrange
        var variables = ValidVariables();
        variables.Remove(variable);

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    // ---------------------------------------------------------------------------
    // Detect — head commit
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_MergeStrategy_UsesThePullRequestHeadNotTheMergeCommit()
    {
        // Arrange — GIT_COMMIT is the local merge of the target into the PR head
        var variables = ValidVariables();
        variables["GIT_COMMIT"] = MergeSha;
        var git = Repository();
        git.FirstParents[(Workspace, MergeSha)] = HeadSha;

        // Act
        var result = Detect(variables, git);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HeadSha, result.CommitSha);
    }

    [Fact]
    public void Detect_RefMovedPastTheBuiltCommit_ReturnsNull()
    {
        // Arrange — a push after Jenkins pinned the revision: the ref is newer than what was built
        var variables = ValidVariables();
        variables["GIT_COMMIT"] = MergeSha;
        var git = Repository();
        git.FirstParents[(Workspace, MergeSha)] = OtherSha;

        // Act & Assert
        Assert.Null(Detect(variables, git));
    }

    [Fact]
    public void Detect_BuiltCommitNotReadable_ReturnsNull()
    {
        // Arrange — GIT_COMMIT differs from the ref and has no readable first parent (packed, root)
        var variables = ValidVariables();
        variables["GIT_COMMIT"] = MergeSha;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_PullRequestRefMissing_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(Detect(ValidVariables(), new FakeGitRepositoryReader()));
    }

    [Fact]
    public void Detect_ReadsTheRefNamedAfterTheJob()
    {
        // Arrange — GitLab Branch Source names MR jobs MR-{n}
        var variables = ValidVariables();
        variables["BRANCH_NAME"] = "MR-17";
        variables["CHANGE_URL"] = "https://gitlab.com/acme/api-service/-/merge_requests/17";
        var git = new FakeGitRepositoryReader { Refs = { [(Workspace, "refs/remotes/origin/MR-17")] = HeadSha } };

        // Act
        var result = Detect(variables, git);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HeadSha, result.CommitSha);
    }

    [Fact]
    public void Detect_GitReaderThrows_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(Detect(ValidVariables(), new FakeGitRepositoryReader { Throws = true }));
    }

    // ---------------------------------------------------------------------------
    // Parity with the other detectors
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("https://dev.azure.com/fabrikam/Online%20Payments/_git/api-service/pullrequest/17", "https://dev.azure.com/fabrikam/")]
    [InlineData("https://fabrikam.visualstudio.com/Online%20Payments/_git/api-service/pullrequest/17", "https://fabrikam.visualstudio.com/")]
    public void Detect_AzureReposRepository_GivesTheSameOwnerAndNameAsAzurePipelines(string changeValue, string collectionValue)
    {
        // Arrange
        var jenkinsVariables = ValidVariables();
        jenkinsVariables["CHANGE_URL"] = changeValue;

        var azureVariables = new Dictionary<string, string?>
        {
            ["BUILD_REASON"] = "PullRequest",
            ["BUILD_REPOSITORY_PROVIDER"] = "TfsGit",
            ["BUILD_REPOSITORY_NAME"] = "api-service",
            ["SYSTEM_COLLECTIONURI"] = collectionValue,
            ["SYSTEM_TEAMPROJECT"] = "Online Payments",
            ["SYSTEM_PULLREQUEST_PULLREQUESTID"] = "17",
            ["SYSTEM_PULLREQUEST_SOURCEBRANCH"] = "refs/heads/feature/login",
            ["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "refs/heads/main",
            ["SYSTEM_PULLREQUEST_SOURCECOMMITID"] = HeadSha,
        };
        var azureEnv = new Mock<IEnvironmentVariableProvider>();
        azureEnv.Setup(e => e.GetVariable(It.IsAny<string>()))
                .Returns((string name) => azureVariables.TryGetValue(name, out string? value) ? value : null);

        // Act
        var jenkins = Detect(jenkinsVariables);
        var azure = new AzureDevOpsPullRequestDetector(
            azureEnv.Object, NullLogger<AzureDevOpsPullRequestDetector>.Instance).Detect();

        // Assert
        Assert.NotNull(jenkins);
        Assert.NotNull(azure);
        Assert.Equal(azure.Platform, jenkins.Platform);
        Assert.Equal(azure.RepositoryOwner, jenkins.RepositoryOwner);
        Assert.Equal(azure.RepositoryName, jenkins.RepositoryName);
    }

    [Fact]
    public void Detect_GitLabNestedGroup_GivesTheSameOwnerAndNameAsGitLabCi()
    {
        // Arrange
        var jenkinsVariables = ValidVariables();
        jenkinsVariables["CHANGE_URL"] = "https://gitlab.com/group/sub/project/-/merge_requests/17";

        var gitLabVariables = new Dictionary<string, string?>
        {
            ["CI_SERVER_HOST"] = "gitlab.com",
            ["CI_MERGE_REQUEST_IID"] = "17",
            ["CI_MERGE_REQUEST_PROJECT_PATH"] = "group/sub/project",
            ["CI_MERGE_REQUEST_SOURCE_BRANCH_NAME"] = "feature/login",
            ["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "main",
            ["CI_COMMIT_SHA"] = HeadSha,
        };
        var gitLabEnv = new Mock<IEnvironmentVariableProvider>();
        gitLabEnv.Setup(e => e.GetVariable(It.IsAny<string>()))
                 .Returns((string name) => gitLabVariables.TryGetValue(name, out string? value) ? value : null);

        // Act
        var jenkins = Detect(jenkinsVariables);
        var gitLab = new GitLabPullRequestDetector(
            gitLabEnv.Object, NullLogger<GitLabPullRequestDetector>.Instance).Detect();

        // Assert
        Assert.NotNull(jenkins);
        Assert.NotNull(gitLab);
        Assert.Equal(gitLab.Platform, jenkins.Platform);
        Assert.Equal(gitLab.RepositoryOwner, jenkins.RepositoryOwner);
        Assert.Equal(gitLab.RepositoryName, jenkins.RepositoryName);
    }
}
