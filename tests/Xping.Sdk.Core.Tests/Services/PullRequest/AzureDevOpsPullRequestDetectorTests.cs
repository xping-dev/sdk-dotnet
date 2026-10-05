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

public sealed class AzureDevOpsPullRequestDetectorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Variables of an Azure Repos PR build. Tests override or remove entries as needed.</summary>
    private static Dictionary<string, string?> AzureReposVariables() => new()
    {
        ["BUILD_REASON"] = "PullRequest",
        ["BUILD_REPOSITORY_PROVIDER"] = "TfsGit",
        ["BUILD_REPOSITORY_NAME"] = "api-service",
        ["SYSTEM_COLLECTIONURI"] = "https://dev.azure.com/fabrikam/",
        ["SYSTEM_TEAMPROJECT"] = "Payments",
        ["SYSTEM_PULLREQUEST_PULLREQUESTID"] = "17",
        ["SYSTEM_PULLREQUEST_SOURCEBRANCH"] = "refs/heads/users/jane/login",
        ["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "refs/heads/main",
        ["SYSTEM_PULLREQUEST_SOURCECOMMITID"] = "headsha123",
        ["BUILD_SOURCEVERSION"] = "mergesha000",
        ["BUILD_REQUESTEDFOR"] = "Jane Dev",
    };

    /// <summary>Variables of a PR build for a GitHub-hosted repository.</summary>
    private static Dictionary<string, string?> GitHubRepoVariables() => new()
    {
        ["BUILD_REASON"] = "PullRequest",
        ["BUILD_REPOSITORY_PROVIDER"] = "GitHub",
        ["BUILD_REPOSITORY_NAME"] = "acme/api-service",
        ["BUILD_REPOSITORY_URI"] = "https://github.com/acme/api-service",
        ["SYSTEM_COLLECTIONURI"] = "https://dev.azure.com/fabrikam/",
        ["SYSTEM_TEAMPROJECT"] = "Payments",
        ["SYSTEM_PULLREQUEST_PULLREQUESTID"] = "1234567890",
        ["SYSTEM_PULLREQUEST_PULLREQUESTNUMBER"] = "42",
        ["SYSTEM_PULLREQUEST_SOURCEBRANCH"] = "feature/login",
        ["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "main",
        ["SYSTEM_PULLREQUEST_SOURCECOMMITID"] = "headsha456",
        ["BUILD_SOURCEVERSION"] = "mergesha000",
        ["BUILD_REQUESTEDFOR"] = "Jane Dev",
    };

    private static PullRequestContext? Detect(Dictionary<string, string?> variables)
    {
        var env = new Mock<IEnvironmentVariableProvider>();
        env.Setup(e => e.GetVariable(It.IsAny<string>()))
           .Returns((string name) => variables.TryGetValue(name, out string? value) ? value : null);
        return new AzureDevOpsPullRequestDetector(env.Object, NullLogger<AzureDevOpsPullRequestDetector>.Instance)
            .Detect();
    }

    // ---------------------------------------------------------------------------
    // Detect — Azure Repos
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_AzureReposPullRequest_MapsEveryField()
    {
        // Act
        var result = Detect(AzureReposVariables());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.AzureDevOps, result.Platform);
        Assert.Equal("fabrikam/Payments", result.RepositoryOwner);
        Assert.Equal("api-service", result.RepositoryName);
        Assert.Equal(17, result.PullRequestNumber);
        Assert.Equal("headsha123", result.CommitSha);
        Assert.Equal("main", result.BaseBranch);
        Assert.Equal("users/jane/login", result.HeadBranch);
        Assert.Equal("Jane Dev", result.Author);
    }

    [Theory]
    [InlineData("https://dev.azure.com/fabrikam/")]
    [InlineData("https://dev.azure.com/fabrikam")]
    [InlineData("https://fabrikam.visualstudio.com/")]
    public void Detect_EitherAzureDevOpsServicesUrlForm_GivesTheSameOwner(string collectionValue)
    {
        // Arrange
        var variables = AzureReposVariables();
        variables["SYSTEM_COLLECTIONURI"] = collectionValue;

        // Act
        var result = Detect(variables);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fabrikam/Payments", result.RepositoryOwner);
    }

    [Theory]
    [InlineData("https://tfs.contoso.local/DefaultCollection/")]
    [InlineData("https://dev.azure.com/")]
    [InlineData("not a url")]
    public void Detect_CollectionUriNotAnAzureDevOpsServicesOrganization_ReturnsNull(string collectionValue)
    {
        // Arrange
        var variables = AzureReposVariables();
        variables["SYSTEM_COLLECTIONURI"] = collectionValue;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("BUILD_REPOSITORY_NAME")]
    [InlineData("SYSTEM_COLLECTIONURI")]
    [InlineData("SYSTEM_TEAMPROJECT")]
    [InlineData("SYSTEM_PULLREQUEST_PULLREQUESTID")]
    [InlineData("SYSTEM_PULLREQUEST_SOURCEBRANCH")]
    [InlineData("SYSTEM_PULLREQUEST_TARGETBRANCH")]
    [InlineData("SYSTEM_PULLREQUEST_SOURCECOMMITID")]
    public void Detect_AzureReposRequiredVariableBlank_ReturnsNull(string variable)
    {
        // Arrange
        var variables = AzureReposVariables();
        variables[variable] = " ";

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_SourceCommitIdMissing_DoesNotFallBackToTheMergeCommit()
    {
        // Arrange — BUILD_SOURCEVERSION is the merge commit on a PR build
        var variables = AzureReposVariables();
        variables.Remove("SYSTEM_PULLREQUEST_SOURCECOMMITID");

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_BranchIsOnlyTheHeadsPrefix_ReturnsNull()
    {
        // Arrange
        var variables = AzureReposVariables();
        variables["SYSTEM_PULLREQUEST_SOURCEBRANCH"] = "refs/heads/";

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    // ---------------------------------------------------------------------------
    // Detect — GitHub-hosted repository
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_GitHubRepoPullRequest_MapsToGitHubWithThePullRequestNumber()
    {
        // Act
        var result = Detect(GitHubRepoVariables());

        // Assert — PULLREQUESTID is GitHub's internal ID, not the number users see
        Assert.NotNull(result);
        Assert.Equal(PullRequestPlatform.GitHub, result.Platform);
        Assert.Equal("acme", result.RepositoryOwner);
        Assert.Equal("api-service", result.RepositoryName);
        Assert.Equal(42, result.PullRequestNumber);
        Assert.Equal("headsha456", result.CommitSha);
        Assert.Equal("main", result.BaseBranch);
        Assert.Equal("feature/login", result.HeadBranch);
    }

    [Fact]
    public void Detect_GitHubRepoWithoutPullRequestNumber_ReturnsNull()
    {
        // Arrange
        var variables = GitHubRepoVariables();
        variables.Remove("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER");

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Fact]
    public void Detect_GitHubRepoNameWithoutOwner_ReturnsNull()
    {
        // Arrange
        var variables = GitHubRepoVariables();
        variables["BUILD_REPOSITORY_NAME"] = "api-service";

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("https://github.com/acme/api-service.git")]
    [InlineData("https://GitHub.com/acme/api-service")]
    public void Detect_GitHubComRepositoryUri_ReturnsContext(string repositoryValue)
    {
        // Arrange
        var variables = GitHubRepoVariables();
        variables["BUILD_REPOSITORY_URI"] = repositoryValue;

        // Act & Assert
        Assert.NotNull(Detect(variables));
    }

    // The context names no host, so Xping Cloud would file these repositories under github.com.
    [Theory]
    [InlineData("https://acme.ghe.com/acme/api-service")]
    [InlineData("https://ghes.acme.com/acme/api-service")]
    [InlineData("https://github.com.example.org/acme/api-service")]
    [InlineData("acme/api-service")]
    [InlineData(null)]
    public void Detect_GitHubRepositoryUriNotGitHubCom_ReturnsNull(string? repositoryValue)
    {
        // Arrange
        var variables = GitHubRepoVariables();
        variables["BUILD_REPOSITORY_URI"] = repositoryValue;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    // ---------------------------------------------------------------------------
    // Detect — not a supported PR build
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("IndividualCI")]
    [InlineData("Manual")]
    [InlineData(null)]
    public void Detect_NotAPullRequestBuild_ReturnsNull(string? buildReason)
    {
        // Arrange
        var variables = AzureReposVariables();
        variables["BUILD_REASON"] = buildReason;

        // Act & Assert
        Assert.Null(Detect(variables));
    }

    [Theory]
    [InlineData("Bitbucket")]
    [InlineData("GitHubEnterprise")]
    [InlineData("Git")]
    [InlineData(" ")]
    public void Detect_UnsupportedRepositoryProvider_ReturnsNull(string provider)
    {
        // Arrange
        var variables = AzureReposVariables();
        variables["BUILD_REPOSITORY_PROVIDER"] = provider;

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
        var detector = new AzureDevOpsPullRequestDetector(env.Object, NullLogger<AzureDevOpsPullRequestDetector>.Instance);

        // Act & Assert
        Assert.Null(detector.Detect());
    }

    // ---------------------------------------------------------------------------
    // IsPullRequestBuild — shared with EnvironmentDetector's CI.IsPullRequest
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("PullRequest", true)]
    [InlineData("pullrequest", true)]
    [InlineData("IndividualCI", false)]
    [InlineData(null, false)]
    public void IsPullRequestBuild_FollowsBuildReason(string? buildReason, bool expected)
    {
        Assert.Equal(expected, AzureDevOpsPullRequestDetector.IsPullRequestBuild(
            name => name == "BUILD_REASON" ? buildReason : null));
    }
}
