/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.PullRequests;

namespace Xping.Sdk.Core.Tests.Models;

public sealed class PullRequestContextTests
{
    // ---------------------------------------------------------------------------
    // Parameterless constructor (public — for JSON deserialization)
    // ---------------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_SetsDefaultPlatformToUnknown()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(PullRequestPlatform.Unknown, ctx.Platform);
    }

    [Fact]
    public void DefaultConstructor_SetsRepositoryOwnerToEmptyString()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(string.Empty, ctx.RepositoryOwner);
    }

    [Fact]
    public void DefaultConstructor_SetsRepositoryNameToEmptyString()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(string.Empty, ctx.RepositoryName);
    }

    [Fact]
    public void DefaultConstructor_SetsPullRequestNumberToZero()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(0, ctx.PullRequestNumber);
    }

    [Fact]
    public void DefaultConstructor_SetsCommitShaToEmptyString()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(string.Empty, ctx.CommitSha);
    }

    [Fact]
    public void DefaultConstructor_SetsBaseBranchToEmptyString()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(string.Empty, ctx.BaseBranch);
    }

    [Fact]
    public void DefaultConstructor_SetsHeadBranchToEmptyString()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Equal(string.Empty, ctx.HeadBranch);
    }

    [Fact]
    public void DefaultConstructor_SetsAuthorToNull()
    {
        // Act
        var ctx = new PullRequestContext();

        // Assert
        Assert.Null(ctx.Author);
    }

    // ---------------------------------------------------------------------------
    // Internal constructor — valid construction
    // ---------------------------------------------------------------------------

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsPlatform()
    {
        // Act
        var ctx = BuildValidContext(platform: PullRequestPlatform.GitHub);

        // Assert
        Assert.Equal(PullRequestPlatform.GitHub, ctx.Platform);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsRepositoryOwner()
    {
        // Act
        var ctx = BuildValidContext(repositoryOwner: "myorg");

        // Assert
        Assert.Equal("myorg", ctx.RepositoryOwner);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsRepositoryName()
    {
        // Act
        var ctx = BuildValidContext(repositoryName: "myrepo");

        // Assert
        Assert.Equal("myrepo", ctx.RepositoryName);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsPullRequestNumber()
    {
        // Act
        var ctx = BuildValidContext(pullRequestNumber: 42);

        // Assert
        Assert.Equal(42, ctx.PullRequestNumber);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsCommitSha()
    {
        // Act
        var ctx = BuildValidContext(commitSha: "abc123");

        // Assert
        Assert.Equal("abc123", ctx.CommitSha);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsBaseBranch()
    {
        // Act
        var ctx = BuildValidContext(baseBranch: "main");

        // Assert
        Assert.Equal("main", ctx.BaseBranch);
    }

    [Fact]
    public void InternalConstructor_WithValidArguments_SetsHeadBranch()
    {
        // Act
        var ctx = BuildValidContext(headBranch: "feature/x");

        // Assert
        Assert.Equal("feature/x", ctx.HeadBranch);
    }

    [Fact]
    public void InternalConstructor_WithAuthorProvided_SetsAuthor()
    {
        // Act
        var ctx = BuildValidContext(author: "devuser");

        // Assert
        Assert.Equal("devuser", ctx.Author);
    }

    [Fact]
    public void InternalConstructor_WithNullAuthor_SetsAuthorToNull()
    {
        // Act
        var ctx = BuildValidContext(author: null);

        // Assert
        Assert.Null(ctx.Author);
    }

    // ---------------------------------------------------------------------------
    // Internal constructor — validation: empty required strings throw ArgumentException
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalConstructor_EmptyOrWhitespaceRepositoryOwner_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BuildValidContext(repositoryOwner: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalConstructor_EmptyOrWhitespaceRepositoryName_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BuildValidContext(repositoryName: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalConstructor_EmptyOrWhitespaceCommitSha_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BuildValidContext(commitSha: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalConstructor_EmptyOrWhitespaceBaseBranch_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BuildValidContext(baseBranch: value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalConstructor_EmptyOrWhitespaceHeadBranch_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BuildValidContext(headBranch: value));
    }

    // ---------------------------------------------------------------------------
    // PullRequestPlatform enum values
    // ---------------------------------------------------------------------------

    [Fact]
    public void PullRequestPlatform_Unknown_HasValueZero()
    {
        Assert.Equal(0, (int)PullRequestPlatform.Unknown);
    }

    [Fact]
    public void PullRequestPlatform_GitHub_HasValueOne()
    {
        Assert.Equal(1, (int)PullRequestPlatform.GitHub);
    }

    [Fact]
    public void PullRequestPlatform_GitLab_HasValueTwo()
    {
        Assert.Equal(2, (int)PullRequestPlatform.GitLab);
    }

    [Fact]
    public void PullRequestPlatform_AzureDevOps_HasValueThree()
    {
        Assert.Equal(3, (int)PullRequestPlatform.AzureDevOps);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static PullRequestContext BuildValidContext(
        PullRequestPlatform platform = PullRequestPlatform.GitHub,
        string repositoryOwner = "myorg",
        string repositoryName = "myrepo",
        int pullRequestNumber = 1,
        string commitSha = "abc123",
        string baseBranch = "main",
        string headBranch = "feature/test",
        string? author = "testuser")
    {
        return new PullRequestContext(
            platform,
            repositoryOwner,
            repositoryName,
            pullRequestNumber,
            commitSha,
            baseBranch,
            headBranch,
            author);
    }
}
