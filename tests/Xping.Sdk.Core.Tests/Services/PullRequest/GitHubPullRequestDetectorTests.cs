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

public sealed class GitHubPullRequestDetectorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a mock IEnvironmentVariableProvider with all GitHub PR variables set to valid values.
    /// Tests override specific variables as needed.
    /// </summary>
    private static Mock<IEnvironmentVariableProvider> BuildValidEnvMock(
        string eventName = "pull_request",
        string githubRef = "refs/pull/42/merge",
        string repository = "myorg/myrepo",
        string sha = "abc123def456",
        string baseRef = "main",
        string headRef = "feature/new-feature",
        string? actor = "devuser")
    {
        var mock = new Mock<IEnvironmentVariableProvider>();
        mock.Setup(e => e.GetVariable("GITHUB_EVENT_NAME")).Returns(eventName);
        mock.Setup(e => e.GetVariable("GITHUB_REF")).Returns(githubRef);
        mock.Setup(e => e.GetVariable("GITHUB_REPOSITORY")).Returns(repository);
        mock.Setup(e => e.GetVariable("GITHUB_SHA")).Returns(sha);
        mock.Setup(e => e.GetVariable("GITHUB_BASE_REF")).Returns(baseRef);
        mock.Setup(e => e.GetVariable("GITHUB_HEAD_REF")).Returns(headRef);
        mock.Setup(e => e.GetVariable("GITHUB_ACTOR")).Returns(actor);
        return mock;
    }

    private static GitHubPullRequestDetector CreateDetector(IEnvironmentVariableProvider env)
        => new(env, NullLogger<GitHubPullRequestDetector>.Instance);

    // ---------------------------------------------------------------------------
    // Detect — full valid pull_request event
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_ValidPullRequestEvent_ReturnsNonNullContext()
    {
        // Arrange
        var env = BuildValidEnvMock();
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsPlatformToGitHub()
    {
        // Arrange
        var env = BuildValidEnvMock();
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.GitHub, result.Platform);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsRepositoryOwner()
    {
        // Arrange
        var env = BuildValidEnvMock(repository: "acme-corp/api-service");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("acme-corp", result.RepositoryOwner);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsRepositoryName()
    {
        // Arrange
        var env = BuildValidEnvMock(repository: "acme-corp/api-service");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("api-service", result.RepositoryName);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsPullRequestNumber()
    {
        // Arrange
        var env = BuildValidEnvMock(githubRef: "refs/pull/99/merge");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(99, result.PullRequestNumber);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsCommitSha()
    {
        // Arrange
        var env = BuildValidEnvMock(sha: "deadbeef1234");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("deadbeef1234", result.CommitSha);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsBaseBranch()
    {
        // Arrange
        var env = BuildValidEnvMock(baseRef: "develop");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("develop", result.BaseBranch);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsHeadBranch()
    {
        // Arrange
        var env = BuildValidEnvMock(headRef: "feature/login");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("feature/login", result.HeadBranch);
    }

    [Fact]
    public void Detect_ValidPullRequestEvent_SetsAuthorFromGitHubActor()
    {
        // Arrange
        var env = BuildValidEnvMock(actor: "janedev");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("janedev", result.Author);
    }

    // ---------------------------------------------------------------------------
    // Detect — pull_request_target event is also supported
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_PullRequestTargetEvent_ReturnsNonNullContext()
    {
        // Arrange
        var env = BuildValidEnvMock(eventName: "pull_request_target");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — event name filtering
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_PushEvent_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(eventName: "push");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_EmptyEventName_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(eventName: string.Empty);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_NullEventName_ReturnsNull()
    {
        // Arrange
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>())).Returns((string?)null);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_EventNameIsCaseInsensitive_PullRequestUpperCase_ReturnsContext()
    {
        // Arrange
        var env = BuildValidEnvMock(eventName: "PULL_REQUEST");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert — case-insensitive match should succeed
        Assert.NotNull(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — GITHUB_REF format validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_GitHubRefDoesNotMatchPrPattern_ReturnsNull()
    {
        // Arrange — refs/heads/ is a branch ref, not a PR ref
        var env = BuildValidEnvMock(githubRef: "refs/heads/main");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_GitHubRefIsEmpty_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(githubRef: "   ");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_GitHubRefHasNonNumericPrNumber_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(githubRef: "refs/pull/abc/merge");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — GITHUB_REPOSITORY format validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_RepositoryMissingSlash_ReturnsNull()
    {
        // Arrange — no slash separator means no owner/name split
        var env = BuildValidEnvMock(repository: "justarepo");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_RepositorySlashAtStart_ReturnsNull()
    {
        // Arrange — slash at position 0 means empty owner
        var env = BuildValidEnvMock(repository: "/reponame");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_RepositorySlashAtEnd_ReturnsNull()
    {
        // Arrange — trailing slash means empty repo name
        var env = BuildValidEnvMock(repository: "owner/");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_RepositoryIsEmpty_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(repository: "   ");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — required variable missing scenarios
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_MissingGitHubSha_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(sha: "   ");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_MissingBaseRef_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(baseRef: "");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_MissingHeadRef_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(headRef: "");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — optional GITHUB_ACTOR
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_NullGitHubActor_ReturnsContextWithNullAuthor()
    {
        // Arrange
        var env = BuildValidEnvMock(actor: null);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Author);
    }

    [Fact]
    public void Detect_EmptyGitHubActor_ReturnsContextWithNullOrEmptyAuthor()
    {
        // Arrange
        var env = BuildValidEnvMock(actor: "");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert — context is still returned; author may be null or empty
        Assert.NotNull(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — never throws (swallows exceptions)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_ProviderThrowsException_ReturnsNull()
    {
        // Arrange — IEnvironmentVariableProvider.GetVariable throws unexpectedly
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>()))
           .Throws(new InvalidOperationException("Simulated environment failure"));
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert — must absorb the exception rather than propagating
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — PR number extracted correctly from ref
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_LargePrNumber_ParsedCorrectly()
    {
        // Arrange
        var env = BuildValidEnvMock(githubRef: "refs/pull/9999/merge");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9999, result.PullRequestNumber);
    }

    [Fact]
    public void Detect_ValidMergeRef_ParsesPrNumber()
    {
        // Arrange — refs/pull/1/merge is a valid pattern
        var env = BuildValidEnvMock(githubRef: "refs/pull/1/merge");
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.PullRequestNumber);
    }
}
