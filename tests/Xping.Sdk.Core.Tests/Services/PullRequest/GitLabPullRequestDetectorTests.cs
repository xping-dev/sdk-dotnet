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

public sealed class GitLabPullRequestDetectorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Variables of a plain (non merged-results) GitLab merge request pipeline. Tests override
    /// or remove entries as needed.
    /// </summary>
    private static Dictionary<string, string?> ValidVariables() => new()
    {
        ["CI_SERVER_HOST"] = "gitlab.com",
        ["CI_MERGE_REQUEST_IID"] = "17",
        ["CI_MERGE_REQUEST_PROJECT_PATH"] = "acme/api-service",
        ["CI_MERGE_REQUEST_SOURCE_BRANCH_NAME"] = "feature/login",
        ["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "main",
        ["CI_COMMIT_SHA"] = "headsha123",
        ["GITLAB_USER_LOGIN"] = "janedev",
    };

    private static GitLabPullRequestDetector CreateDetector(Dictionary<string, string?> variables)
    {
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>()))
           .Returns((string name) => variables.TryGetValue(name, out string? value) ? value : null);
        return new GitLabPullRequestDetector(env.Object, NullLogger<GitLabPullRequestDetector>.Instance);
    }

    private static PullRequestContext? Detect(Dictionary<string, string?> variables) =>
        CreateDetector(variables).Detect();

    // ---------------------------------------------------------------------------
    // Detect — merge request pipeline
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_MergeRequestPipeline_MapsEveryField()
    {
        // Act
        var result = Detect(ValidVariables());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.GitLab, result.Platform);
        Assert.Equal("acme", result.RepositoryOwner);
        Assert.Equal("api-service", result.RepositoryName);
        Assert.Equal(17, result.PullRequestNumber);
        Assert.Equal("headsha123", result.CommitSha);
        Assert.Equal("main", result.BaseBranch);
        Assert.Equal("feature/login", result.HeadBranch);
        Assert.Equal("janedev", result.Author);
    }

    [Fact]
    public void Detect_NestedGroup_KeepsTheGroupsInTheOwner()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CI_MERGE_REQUEST_PROJECT_PATH"] = "group/sub/project";

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("group/sub", result.RepositoryOwner);
        Assert.Equal("project", result.RepositoryName);
    }

    [Fact]
    public void Detect_MergedResultsPipeline_UsesTheSourceBranchShaNotTheMergeResult()
    {
        // Arrange — in merged-results and merge-train pipelines CI_COMMIT_SHA is the merge result
        var variables = ValidVariables();
        variables["CI_COMMIT_SHA"] = "mergeresult000";
        variables["CI_MERGE_REQUEST_SOURCE_BRANCH_SHA"] = "headsha456";

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("headsha456", result.CommitSha);
    }

    [Fact]
    public void Detect_BlankSourceBranchSha_FallsBackToCommitSha()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CI_MERGE_REQUEST_SOURCE_BRANCH_SHA"] = " ";

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("headsha123", result.CommitSha);
    }

    [Fact]
    public void Detect_NoUserLogin_ReturnsContextWithNullAuthor()
    {
        // Arrange
        var variables = ValidVariables();
        variables.Remove("GITLAB_USER_LOGIN");

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Author);
    }

    // ---------------------------------------------------------------------------
    // Detect — not a merge request pipeline, or incomplete
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("gitlab.example.com")]
    [InlineData("gitlab.com.example.org")]
    public void Detect_SelfManagedInstance_ReturnsNull(string serverHost)
    {
        // Arrange — the context names no host, so a self-managed project would be read as gitlab.com
        var variables = ValidVariables();
        variables["CI_SERVER_HOST"] = serverHost;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_GitLabComHostInAnyCase_ReturnsContext()
    {
        // Arrange
        var variables = ValidVariables();
        variables["CI_SERVER_HOST"] = "GitLab.com";

        // Act & Assert
        Assert.NotNull(Detect(variables));
    }

    [Fact]
    public void Detect_BranchPipeline_ReturnsNull()
    {
        // Arrange — a push pipeline has CI_COMMIT_SHA but no merge request variables
        var variables = new Dictionary<string, string?> { ["CI_COMMIT_SHA"] = "headsha123" };

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("CI_SERVER_HOST")]
    [InlineData("CI_MERGE_REQUEST_PROJECT_PATH")]
    [InlineData("CI_MERGE_REQUEST_SOURCE_BRANCH_NAME")]
    [InlineData("CI_MERGE_REQUEST_TARGET_BRANCH_NAME")]
    [InlineData("CI_COMMIT_SHA")]
    public void Detect_RequiredVariableBlank_ReturnsNull(string variable)
    {
        // Arrange
        var variables = ValidVariables();
        variables[variable] = "  ";

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("0")]
    public void Detect_IidIsNotAPositiveNumber_ReturnsNull(string iid)
    {
        // Arrange
        var variables = ValidVariables();
        variables["CI_MERGE_REQUEST_IID"] = iid;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("project-only")]
    [InlineData("/project")]
    [InlineData("group/")]
    public void Detect_ProjectPathNotNamespaced_ReturnsNull(string projectPath)
    {
        // Arrange
        var variables = ValidVariables();
        variables["CI_MERGE_REQUEST_PROJECT_PATH"] = projectPath;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_ProviderThrows_ReturnsNull()
    {
        // Arrange
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>()))
           .Throws(new InvalidOperationException("Simulated environment failure"));
        var detector = new GitLabPullRequestDetector(env.Object, NullLogger<GitLabPullRequestDetector>.Instance);

        // Act & Assert
        Assert.Null(detector.Detect());
    }

    // ---------------------------------------------------------------------------
    // IsPullRequestBuild — shared with EnvironmentDetector's CI.IsPullRequest
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("17", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPullRequestBuild_FollowsMergeRequestIid(string? iid, bool expected)
    {
        Assert.Equal(expected, GitLabPullRequestDetector.IsPullRequestBuild(
            name => name == "CI_MERGE_REQUEST_IID" ? iid : null));
    }
}
