/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.PullRequest.Internals;
using Xping.Sdk.Core.Services.Serialization;
using Xping.Sdk.Core.Services.Serialization.Internals;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

public sealed class GitHubPullRequestDetectorTests : IDisposable
{
    private const string HeadSha = "abc123def456";

    // GITHUB_SHA is deliberately different from the payload's head SHA so every test shows it's ignored.
    private const string MergeSha = "0000merge0000";

    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public GitHubPullRequestDetectorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private string WritePayload(string json)
    {
        string path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private string WriteHeadShaPayload(string sha) =>
        WritePayload($$"""{ "number": 42, "pull_request": { "head": { "sha": "{{sha}}", "ref": "feature" } } }""");

    /// <summary>
    /// Builds a mock IEnvironmentVariableProvider with all GitHub PR variables set to valid values.
    /// Tests override specific variables as needed. A null <paramref name="eventPath"/> writes a
    /// valid payload whose head SHA is <see cref="HeadSha"/>.
    /// </summary>
    private Mock<IEnvironmentVariableProvider> BuildValidEnvMock(
        string eventName = "pull_request",
        string githubRef = "refs/pull/42/merge",
        string repository = "myorg/myrepo",
        string? eventPath = null,
        string baseRef = "main",
        string headRef = "feature/new-feature",
        string? actor = "devuser",
        string? serverUrl = "https://github.com")
    {
        var mock = new Mock<IEnvironmentVariableProvider>();
        mock.Setup(e => e.GetVariable("GITHUB_EVENT_NAME")).Returns(eventName);
        mock.Setup(e => e.GetVariable("GITHUB_SERVER_URL")).Returns(serverUrl);
        mock.Setup(e => e.GetVariable("GITHUB_REF")).Returns(githubRef);
        mock.Setup(e => e.GetVariable("GITHUB_REPOSITORY")).Returns(repository);
        mock.Setup(e => e.GetVariable("GITHUB_SHA")).Returns(MergeSha);
        mock.Setup(e => e.GetVariable("GITHUB_EVENT_PATH")).Returns(eventPath ?? WriteHeadShaPayload(HeadSha));
        mock.Setup(e => e.GetVariable("GITHUB_BASE_REF")).Returns(baseRef);
        mock.Setup(e => e.GetVariable("GITHUB_HEAD_REF")).Returns(headRef);
        mock.Setup(e => e.GetVariable("GITHUB_ACTOR")).Returns(actor);
        return mock;
    }

    private static GitHubPullRequestDetector CreateDetector(
        IEnvironmentVariableProvider env,
        ILogger<GitHubPullRequestDetector>? logger = null)
        => new(
            env,
            new XpingJsonSerializer(XpingSerializerOptions.ApiOptions),
            logger ?? NullLogger<GitHubPullRequestDetector>.Instance);

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

    [Theory]
    [InlineData("pull_request")]
    [InlineData("pull_request_target")]
    public void Detect_PullRequestEvent_SetsCommitShaToPayloadHeadNotGitHubSha(string eventName)
    {
        // Arrange — GITHUB_SHA is the merge commit (pull_request) or the base tip (pull_request_target)
        var env = BuildValidEnvMock(eventName: eventName, eventPath: WriteHeadShaPayload("deadbeef1234"));
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
    // Detect — GITHUB_SERVER_URL names the server
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("https://github.com", "https://github.com")]
    [InlineData("https://github.com/", "https://github.com")]
    [InlineData("https://GitHub.com", "https://github.com")]
    [InlineData("https://acme.ghe.com", "https://acme.ghe.com")]
    [InlineData("https://GHES.acme.com/", "https://ghes.acme.com")]
    [InlineData("http://ghes.acme.local:8080", "http://ghes.acme.local:8080")]
    public void Detect_AnyGitHubServer_ReturnsContextWithNormalizedServerUrl(string serverValue, string expected)
    {
        // Arrange
        var env = BuildValidEnvMock(serverUrl: serverValue);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.ServerUrl);
    }

    [Theory]
    [InlineData("github.com")]
    [InlineData("ftp://ghes.acme.com")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Detect_ServerUrlNotAnAbsoluteHttpUrl_ReturnsNull(string? serverValue)
    {
        // Arrange
        var env = BuildValidEnvMock(serverUrl: serverValue);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    // Actions always sets it, so an unset value is a container given only some GITHUB_* variables.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Detect_ServerUrlNotSet_LogsWarning(string? serverValue)
    {
        // Arrange
        var logger = new LevelRecordingLogger<GitHubPullRequestDetector>();
        var detector = CreateDetector(BuildValidEnvMock(serverUrl: serverValue).Object, logger);

        // Act
        detector.Detect();

        // Assert
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    [Fact]
    public void Detect_MalformedServerUrl_DoesNotWarn()
    {
        // Arrange
        var logger = new LevelRecordingLogger<GitHubPullRequestDetector>();
        var detector = CreateDetector(BuildValidEnvMock(serverUrl: "github.com").Object, logger);

        // Act
        detector.Detect();

        // Assert
        Assert.DoesNotContain(LogLevel.Warning, logger.Levels);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Detect_MissingEventPath_ReturnsNull(string eventPath)
    {
        // Arrange
        var env = BuildValidEnvMock(eventPath: eventPath);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_EventPathNotSet_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock();
        env.Setup(e => e.GetVariable("GITHUB_EVENT_PATH")).Returns((string?)null);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_EventPayloadStartsWithUtf8Bom_ReadsHeadSha()
    {
        // Arrange
        string path = Path.Combine(_tempDir, "bom.json");
        File.WriteAllText(path, """{ "pull_request": { "head": { "sha": "bom123" } } }""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var env = BuildValidEnvMock(eventPath: path);
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bom123", result.CommitSha);
    }

    [Fact]
    public void Detect_EventPayloadFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(eventPath: Path.Combine(_tempDir, "missing.json"));
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_EventPayloadIsMalformedJson_ReturnsNull()
    {
        // Arrange
        var env = BuildValidEnvMock(eventPath: WritePayload("{ not json"));
        var detector = CreateDetector(env.Object);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"pull_request":null}""")]
    [InlineData("""{"pull_request":{}}""")]
    [InlineData("""{"pull_request":{"head":{}}}""")]
    [InlineData("""{"pull_request":{"head":{"sha":"  "}}}""")]
    public void Detect_EventPayloadHasNoHeadSha_ReturnsNull(string json)
    {
        // Arrange
        var env = BuildValidEnvMock(eventPath: WritePayload(json));
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

    // ---------------------------------------------------------------------------
    // PullRequestFlag — EnvironmentDetector's CI.IsPullRequest
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("pull_request", "refs/pull/17/merge", true)]
    [InlineData("pull_request_target", "refs/heads/main", true)]
    [InlineData("pull_request_review", "refs/pull/17/merge", true)]
    [InlineData("push", "refs/heads/main", false)]
    [InlineData("PUSH", "refs/heads/main", false)]
    [InlineData("schedule", "refs/heads/main", false)]
    [InlineData("workflow_dispatch", "refs/heads/feature", false)]
    [InlineData("merge_group", "refs/heads/gh-readonly-queue/main/pr-17-0123abcd", false)]
    [InlineData("release", "refs/tags/v1.0.0", false)]
    [InlineData("create", "refs/heads/feature", false)]
    [InlineData("deployment", "refs/heads/main", false)]
    [InlineData("deployment_status", "refs/heads/main", false)]
    [InlineData("issue_comment", "refs/heads/main", null)]
    [InlineData("workflow_run", "refs/heads/main", null)]
    [InlineData("repository_dispatch", "refs/heads/main", null)]
    [InlineData(null, "refs/heads/main", null)]
    public void PullRequestFlag_FollowsEventAndRef(string? eventName, string githubRef, bool? expected)
    {
        Assert.Equal(expected, GitHubPullRequestDetector.PullRequestFlag(name => name switch
        {
            "GITHUB_EVENT_NAME" => eventName,
            "GITHUB_REF" => githubRef,
            _ => null,
        }));
    }
}
