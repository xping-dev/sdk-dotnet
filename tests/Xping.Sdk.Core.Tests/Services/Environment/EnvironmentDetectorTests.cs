/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Options;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Environment.Internals;
using Xping.Sdk.Core.Services.Network;

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

    private static EnvironmentDetector CreateDetector(XpingConfiguration? configuration = null)
    {
        XpingConfiguration resolvedConfiguration = configuration ?? new XpingConfiguration();
        resolvedConfiguration.CollectNetworkMetrics = false;

        return new EnvironmentDetector(
            Options.Create(resolvedConfiguration),
            Mock.Of<INetworkMetricsCollector>());
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
}
