/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.PullRequest.Internals;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.Environment.Internals;

/// <summary>
/// Default implementation of <see cref="IEnvironmentDetector"/> that detects environment information
/// from the runtime, operating system, and environment variables.
/// Uses instance-level caching for DI-friendly behavior.
/// </summary>
internal sealed class EnvironmentDetector : IEnvironmentDetector
{
    private readonly XpingConfiguration _configuration;
    private readonly IXpingSerializer _serializer;
    private readonly IEnvironmentVariableProvider _env;

    // Instance-level lazy initialization for thread-safe, cached detection
    private readonly Lazy<string> _machineName;
    private readonly Lazy<string> _operatingSystem;
    private readonly Lazy<string> _runtimeVersion;
    private readonly Lazy<string> _framework;
    private readonly Lazy<CIPlatform?> _ciPlatform;
    private readonly Lazy<bool> _isContainer;
    private readonly Lazy<TimeZoneInfo?> _localTimeZone;
    private readonly Lazy<Dictionary<string, string>> _customProperties;

    // Not lazy: reading it is a null check on a string the configuration already holds. The
    // laziness the other fields need exists to defer probing the host - environment variables, the
    // file system, the time zone database - and there is nothing here left to defer.
    private readonly string _environmentName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentDetector"/> class.
    /// </summary>
    public EnvironmentDetector(
        IOptions<XpingConfiguration> options, IXpingSerializer serializer, IEnvironmentVariableProvider env)
    {
        _configuration = options.Value;
        _serializer = serializer;
        _env = env;

        // Initialize instance-level lazy fields
        _machineName = new Lazy<string>(GetMachineName);
        _operatingSystem = new Lazy<string>(DetectOperatingSystem);
        _runtimeVersion = new Lazy<string>(DetectRuntimeVersion);
        _framework = new Lazy<string>(() => DetectFramework(_operatingSystem.Value));
        _environmentName = _configuration.ResolvedEnvironment;
        _ciPlatform = new Lazy<CIPlatform?>(DetectCiPlatform);
        _isContainer = new Lazy<bool>(DetectIsContainer);
        _localTimeZone = new Lazy<TimeZoneInfo?>(DetectLocalTimeZone);
        _customProperties = new Lazy<Dictionary<string, string>>(() =>
            CollectCustomProperties(_operatingSystem.Value, _ciPlatform.Value, _isContainer.Value));
    }

    /// <inheritdoc/>
    string IEnvironmentDetector.MachineName => _machineName.Value;

    /// <inheritdoc/>
    string IEnvironmentDetector.OperatingSystem => _operatingSystem.Value;

    /// <inheritdoc/>
    string IEnvironmentDetector.RuntimeVersion => _runtimeVersion.Value;

    /// <inheritdoc/>
    string IEnvironmentDetector.Framework => _framework.Value;

    /// <inheritdoc/>
    string IEnvironmentDetector.EnvironmentName => _environmentName;

    /// <inheritdoc/>
    bool IEnvironmentDetector.IsCiEnvironment => _ciPlatform.Value.HasValue;

    /// <inheritdoc/>
    bool IEnvironmentDetector.IsContainer => _isContainer.Value;

    /// <inheritdoc/>
    IReadOnlyDictionary<string, string> IEnvironmentDetector.CustomProperties => _customProperties.Value;

    /// <inheritdoc/>
    Task<EnvironmentInfo> IEnvironmentDetector.BuildEnvironmentInfoAsync(CancellationToken cancellationToken)
    {
        // Resolved against now rather than cached with the zone: the offset is a property of the
        // instant, not of the machine, and a suite started either side of a daylight-saving
        // transition is exactly the case this field exists to make visible.
        TimeZoneInfo? zone = _localTimeZone.Value;

        // A local builder, as in ExecutionTracker.CreateExecutionContext. This type is registered as
        // a singleton so the detection lazies are paid for once, and a builder held alongside them
        // would be shared by every caller: two concurrent calls could interleave their Reset() and
        // With...() calls and emit an EnvironmentInfo mixing fields from both.
        EnvironmentInfo environmentInfo = new EnvironmentInfoBuilder()
            .WithMachineName(_machineName.Value)
            .WithOperatingSystem(_operatingSystem.Value)
            .WithRuntimeVersion(_runtimeVersion.Value)
            .WithFramework(_framework.Value)
            .WithEnvironmentName(_environmentName)
            .WithIsCIEnvironment(_ciPlatform.Value.HasValue)
            .WithLocalTimeZone(zone?.GetUtcOffset(DateTime.UtcNow), zone?.Id)
            .AddCustomProperties(_customProperties.Value)
            .Build();

        // Nothing here awaits, and nothing observes the token: every field is read from a cached
        // lazy or the local clock, so there is no operation for a cancellation to interrupt. Both
        // callers already check the token before arriving — FlushOnceAsync and FinalizeSessionAsync enter
        // through _flushLock.WaitAsync(cancellationToken), which throws on an already-cancelled one
        // — so a check here would be unreachable, and throwing during finalization would abandon the
        // run rather than persist it. The interface stays task-returning so a detector that does
        // need to await is not a signature change away.
        return Task.FromResult(environmentInfo);
    }

    /// <summary>
    /// Reads the machine's time zone, or returns <see langword="null"/> when it has no usable one.
    /// </summary>
    /// <remarks>
    /// A container built without a time zone database throws here rather than reporting UTC, and a
    /// misconfigured <c>TZ</c> variable does the same. Neither is a reason to fail a test run, and
    /// substituting UTC would be worse than recording nothing: analysis distinguishes "not captured"
    /// from "captured as UTC", and a fabricated zero would put every such machine in the same bin as
    /// a genuine UTC one.
    /// </remarks>
    private static TimeZoneInfo? DetectLocalTimeZone()
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

    private static string GetMachineName()
    {
        try
        {
            string machineName = System.Environment.MachineName;
            return string.IsNullOrWhiteSpace(machineName) ? "unknown" : machineName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string DetectOperatingSystem()
    {
        try
        {
            OperatingSystem os = System.Environment.OSVersion;

            // Get OS description using RuntimeInformation (cross-platform)
            string? description = RuntimeInformation.OSDescription;

            // Check for mobile platforms first (they can report as Unix/Linux)
            // Android detection
            if (description.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"Android ({description})";
            }

            // iOS detection
            if (description.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                description.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                description.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"iOS ({description})";
            }

            // Try to get more specific version info for desktop platforms
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"Windows {os.Version.Major}.{os.Version.Minor} ({description})";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"Linux {os.Version.Major}.{os.Version.Minor}.{os.Version.Build}";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"macOS {os.Version.Major}.{os.Version.Minor}.{os.Version.Build}";
            }

            return description;
        }
        catch
        {
            return "Unknown OS";
        }
    }

    private static string DetectRuntimeVersion()
    {
        try
        {
            // Get .NET runtime version
            string? runtimeVersion = RuntimeInformation.FrameworkDescription;
            return runtimeVersion;
        }
        catch
        {
            return "Unknown Runtime";
        }
    }

    private static string DetectFramework(string operatingSystem)
    {
        try
        {
            // Detect a framework type based on runtime
            string? frameworkDescription = RuntimeInformation.FrameworkDescription;

            // Check for mobile-specific frameworks first
            if (operatingSystem.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // .NET for Android (formerly Xamarin.Android)
                if (frameworkDescription.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return frameworkDescription; // Return full description like ".NET 9.0-android"
                }

                return $".NET for Android ({frameworkDescription})";
            }

            if (operatingSystem.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                operatingSystem.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                operatingSystem.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // .NET for iOS (formerly Xamarin.iOS)
                if (frameworkDescription.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return frameworkDescription; // Return a full description like ".NET 9.0-ios"
                }

                return $".NET for iOS ({frameworkDescription})";
            }

            // Desktop frameworks
            if (frameworkDescription.IndexOf(".NET Framework", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Framework";
            }
            if (frameworkDescription.IndexOf(".NET Core", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Core";
            }
            if (frameworkDescription.IndexOf(".NET Native", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Native";
            }
            if (frameworkDescription.IndexOf(".NET", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Modern .NET (5+)
                return ".NET";
            }

            return frameworkDescription;
        }
        catch
        {
            return "Unknown Framework";
        }
    }

    private CIPlatform? DetectCiPlatform()
    {
        // GitHub Actions
        if (GetVariable("GITHUB_ACTIONS") == "true")
        {
            return CIPlatform.GitHubActions;
        }

        // Azure DevOps (Azure Pipelines)
        if (!string.IsNullOrEmpty(GetVariable("TF_BUILD")))
        {
            return CIPlatform.AzureDevOps;
        }

        // Jenkins
        if (!string.IsNullOrEmpty(GetVariable("JENKINS_URL")))
        {
            return CIPlatform.Jenkins;
        }

        // GitLab CI
        if (GetVariable("GITLAB_CI") == "true")
        {
            return CIPlatform.GitLabCI;
        }

        // CircleCI
        if (GetVariable("CIRCLECI") == "true")
        {
            return CIPlatform.CircleCI;
        }

        // Travis CI
        if (GetVariable("TRAVIS") == "true")
        {
            return CIPlatform.TravisCI;
        }

        // TeamCity
        if (!string.IsNullOrEmpty(GetVariable("TEAMCITY_VERSION")))
        {
            return CIPlatform.TeamCity;
        }

        // Bitbucket Pipelines
        if (GetVariable("BITBUCKET_PIPELINE_UUID") != null)
        {
            return CIPlatform.BitbucketPipelines;
        }

        // AppVeyor
        if (GetVariable("APPVEYOR") == "True")
        {
            return CIPlatform.AppVeyor;
        }

        // Generic CI indicator
        if (GetVariable("CI") == "true" || GetVariable("CI") == "True")
        {
            return CIPlatform.Generic;
        }

        return null;
    }

    private bool DetectIsContainer()
    {
        try
        {
            // Docker: Check for .dockerenv file
            if (File.Exists("/.dockerenv"))
            {
                return true;
            }

            // Kubernetes: Check for kubernetes service environment variables
            if (!string.IsNullOrEmpty(GetVariable("KUBERNETES_SERVICE_HOST")))
            {
                return true;
            }

            // Check cgroup for docker/kubepods
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/proc/1/cgroup"))
                {
                    string cgroupContent = File.ReadAllText("/proc/1/cgroup");
                    if (cgroupContent.IndexOf("docker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cgroupContent.IndexOf("kubepods", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private Dictionary<string, string> CollectCustomProperties(string operatingSystem, CIPlatform? ciPlatform, bool isContainer)
    {
        Dictionary<string, string> properties = new()
        {
            // Where the run happened, which is a different question from the environment name and
            // is why these are literals rather than XpingConfiguration.DefaultEnvironment. The
            // environment name no longer carries the CI-vs-local distinction; this property and
            // IsCIEnvironment do, diagnostically.
            ["ExecutionContext"] = ciPlatform.HasValue ? "CI" : "Local"
        };

        if (!ciPlatform.HasValue && !isContainer)
        {
            properties["IsDeveloperMachine"] = "true";
        }

        // Detect whether running inside a git repository
        string? gitDir = FindGitDirectory();
        bool isInsideGitRepository = gitDir is not null;
        properties["IsInsideGitRepository"] = isInsideGitRepository ? "true" : "false";

        // Collect local git metadata when not running in a CI environment
        if (!ciPlatform.HasValue && gitDir is not null)
        {
            CollectLocalGitMetadata(properties, gitDir, _configuration.CollectLocalGitAuthor);
        }

        // Add container information
        if (isContainer)
        {
            properties["IsContainer"] = "true";
        }

        // Add mobile platform detection
        if (operatingSystem.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Android";
            properties["IsMobile"] = "true";
        }
        else if (operatingSystem.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "iOS";
            properties["IsMobile"] = "true";

            // Distinguish between iPhone and iPad
            if (operatingSystem.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                properties["DeviceType"] = "iPad";
            }
            else if (operatingSystem.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                properties["DeviceType"] = "iPhone";
            }
        }
        else if (operatingSystem.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Windows";
        }
        else if (operatingSystem.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Linux";
        }
        else if (operatingSystem.IndexOf("macOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 operatingSystem.IndexOf("OSX", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "macOS";
        }

        // Add CI-specific properties
        if (ciPlatform.HasValue)
        {
            properties["CIPlatform"] = ciPlatform.Value.ToString();

            switch (ciPlatform.Value)
            {
                case CIPlatform.GitHubActions:
                    AddIfNotNull(properties, "CI.Repository", GetVariable("GITHUB_REPOSITORY"));
                    AddServerUrl(properties, ServerUrlOf(GetVariable("GITHUB_SERVER_URL")));
                    AddIfNotNull(properties, "CI.RunId", GetVariable("GITHUB_RUN_ID"));
                    AddIfNotNull(properties, "CI.RunNumber", GetVariable("GITHUB_RUN_NUMBER"));
                    AddIfNotNull(properties, "CI.Ref", GetVariable("GITHUB_REF"));
                    AddIfNotNull(properties, "CI.Branch", GetFirstNonEmptyValue(
                        GetVariable("GITHUB_HEAD_REF"),
                        GetVariable("GITHUB_REF_NAME"),
                        ExtractBranchName(GetVariable("GITHUB_REF"))));
                    AddIfNotNull(properties, "CI.DefaultBranch", ReadGitHubDefaultBranch());
                    AddIfNotNull(properties, "CI.HeadBranch", GetVariable("GITHUB_HEAD_REF"));
                    AddIfNotNull(properties, "CI.BaseBranch", GetVariable("GITHUB_BASE_REF"));
                    string githubEventName = GetVariable("GITHUB_EVENT_NAME") ?? string.Empty;
                    bool? isGitHubPullRequest = GitHubPullRequestDetector.PullRequestFlag(GetVariable);
                    if (isGitHubPullRequest is not null)
                    {
                        // CI.CommitSha is the commit the build tested. On pull_request_target GITHUB_SHA is
                        // the base branch's tip, but such workflows usually check out the PR head, so the
                        // built commit is unknown and sending the base tip would tie PR results to main.
                        if (!string.Equals(githubEventName, "pull_request_target", StringComparison.OrdinalIgnoreCase))
                        {
                            AddIfNotNull(properties, "CI.CommitSha", GetVariable("GITHUB_SHA"));
                        }

                        AddPullRequestFlag(properties, isGitHubPullRequest.Value);
                    }

                    AddIfNotNull(properties, "CI.Actor", GetVariable("GITHUB_ACTOR"));
                    AddIfNotNull(properties, "CI.Workflow", GetVariable("GITHUB_WORKFLOW"));
                    break;

                case CIPlatform.AzureDevOps:
                    AddIfNotNull(properties, "CI.BuildId", GetVariable("BUILD_BUILDID"));
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("BUILD_BUILDNUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetVariable("BUILD_REPOSITORY_NAME"));
                    AddServerUrl(properties, AzurePipelinesServerUrl());
                    bool isAzurePullRequest = AzureDevOpsPullRequestDetector.IsPullRequestBuild(GetVariable);
                    AddIfNotNull(properties, "CI.SourceBranch", GetVariable("BUILD_SOURCEBRANCH"));
                    AddIfNotNull(properties, "CI.Branch", AzurePipelinesBranch(isAzurePullRequest));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("BUILD_SOURCEVERSION"));
                    AddPullRequestFlag(properties, isAzurePullRequest);
                    AddIfNotNull(properties, "CI.RequestedFor", GetVariable("BUILD_REQUESTEDFOR"));
                    break;

                case CIPlatform.Jenkins:
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.JobName", GetVariable("JOB_NAME"));
                    AddIfNotNull(properties, "CI.BuildUrl", GetVariable("BUILD_URL"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("GIT_COMMIT"));
                    // CHANGE_ID comes from the Branch Source plugin, ghprbPullId from GHPRB (flagged, but no PR
                    // context is detected for it). A job built by any other PR plugin reads as a push build.
                    bool isBranchSourcePullRequest = JenkinsPullRequestDetector.IsPullRequestBuild(GetVariable);
                    bool isGhprbPullRequest = HasValue("ghprbPullId");
                    AddPullRequestFlag(properties, isBranchSourcePullRequest || isGhprbPullRequest);
                    AddIfNotNull(properties, "CI.GitBranch", GetVariable("GIT_BRANCH"));
                    // On a PR build, GIT_BRANCH names the PR, not its source branch: Branch Source names the job
                    // (BRANCH_NAME, and GIT_BRANCH with it) PR-12, and GHPRB checks out origin/pr/12/merge.
                    AddIfNotNull(properties, "CI.Branch",
                        isBranchSourcePullRequest ? GetVariable("CHANGE_BRANCH")
                        : isGhprbPullRequest ? GetVariable("ghprbSourceBranch")
                        : GetFirstNonEmptyValue(
                            GetVariable("BRANCH_NAME"),
                            StripOriginRemote(GetVariable("GIT_BRANCH"))));
                    break;

                case CIPlatform.GitLabCI:
                    AddIfNotNull(properties, "CI.JobId", GetVariable("CI_JOB_ID"));
                    AddIfNotNull(properties, "CI.PipelineId", GetVariable("CI_PIPELINE_ID"));
                    AddIfNotNull(properties, "CI.ProjectPath", GetVariable("CI_PROJECT_PATH"));
                    AddServerUrl(properties, ServerUrlOf(GetVariable("CI_SERVER_URL")));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("CI_COMMIT_SHA"));
                    AddPullRequestFlag(properties, GitLabPullRequestDetector.IsPullRequestBuild(GetVariable));
                    AddIfNotNull(properties, "CI.CommitBranch", GetVariable("CI_COMMIT_BRANCH"));
                    AddIfNotNull(properties, "CI.DefaultBranch", GetVariable("CI_DEFAULT_BRANCH"));
                    AddIfNotNull(properties, "CI.Branch", GetFirstNonEmptyValue(
                        GetVariable("CI_COMMIT_BRANCH"),
                        GetVariable("CI_COMMIT_REF_NAME")));
                    AddIfNotNull(properties, "CI.CommitAuthor", GetVariable("CI_COMMIT_AUTHOR"));
                    AddIfNotNull(properties, "CI.Actor", GetVariable("GITLAB_USER_LOGIN"));
                    break;

                case CIPlatform.CircleCI:
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("CIRCLE_BUILD_NUM"));
                    AddIfNotNull(properties, "CI.WorkflowId", GetVariable("CIRCLE_WORKFLOW_ID"));
                    AddIfNotNull(properties, "CI.ProjectName", GetVariable("CIRCLE_PROJECT_REPONAME"));
                    AddIfNotNull(properties, "CI.Branch", GetVariable("CIRCLE_BRANCH"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("CIRCLE_SHA1"));
                    // CircleCI also sets this on a plain branch build whenever the branch has an open PR. That
                    // commit is still unmerged PR code, so counting it as a PR is correct.
                    AddPullRequestFlag(properties, HasValue("CIRCLE_PULL_REQUEST"));
                    AddIfNotNull(properties, "CI.Username", GetVariable("CIRCLE_USERNAME"));
                    break;

                case CIPlatform.TravisCI:
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("TRAVIS_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.JobNumber", GetVariable("TRAVIS_JOB_NUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetVariable("TRAVIS_REPO_SLUG"));
                    AddIfNotNull(properties, "CI.Branch", GetVariable("TRAVIS_BRANCH"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("TRAVIS_COMMIT"));
                    // Travis sets TRAVIS_PULL_REQUEST to the PR number, or to the literal "false" on a push build.
                    AddPullRequestFlag(properties, HasValue("TRAVIS_PULL_REQUEST") && !string.Equals(
                        GetVariable("TRAVIS_PULL_REQUEST"), "false", StringComparison.OrdinalIgnoreCase));
                    break;

                case CIPlatform.TeamCity:
                    AddIfNotNull(properties, "CI.BuildId", GetVariable("TEAMCITY_BUILD_ID"));
                    AddIfNotNull(properties, "CI.Version", GetVariable("TEAMCITY_VERSION"));
                    AddIfNotNull(properties, "CI.ProjectName", GetVariable("TEAMCITY_PROJECT_NAME"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("BUILD_VCS_NUMBER"));
                    // TeamCity has no reliable PR marker. Omitting the flag means "unknown", which Xping Cloud
                    // treats as possibly a PR; "false" would wrongly vouch for the commit.
                    break;

                case CIPlatform.BitbucketPipelines:
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("BITBUCKET_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetVariable("BITBUCKET_REPO_FULL_NAME"));
                    AddIfNotNull(properties, "CI.Branch", GetVariable("BITBUCKET_BRANCH"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("BITBUCKET_COMMIT"));
                    AddPullRequestFlag(properties, HasValue("BITBUCKET_PR_ID"));
                    break;

                case CIPlatform.AppVeyor:
                    AddIfNotNull(properties, "CI.BuildNumber", GetVariable("APPVEYOR_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.BuildVersion", GetVariable("APPVEYOR_BUILD_VERSION"));
                    AddIfNotNull(properties, "CI.Repository", GetVariable("APPVEYOR_REPO_NAME"));
                    AddIfNotNull(properties, "CI.Branch", GetVariable("APPVEYOR_REPO_BRANCH"));
                    AddIfNotNull(properties, "CI.CommitSha", GetVariable("APPVEYOR_REPO_COMMIT"));
                    AddPullRequestFlag(properties, HasValue("APPVEYOR_PULL_REQUEST_NUMBER"));
                    break;
            }
        }

        // Add processor architecture
        properties["ProcessorArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString();

        // Add process information
        properties["ProcessorCount"] = System.Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture);

        return properties;
    }

    private static string? FindGitDirectory()
    {
        try
        {
            var dir = new DirectoryInfo(System.Environment.CurrentDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, ".git");

                if (Directory.Exists(candidate))
                    return candidate;

                // Worktrees and submodules: .git is a file containing "gitdir: <path>"
                if (File.Exists(candidate))
                {
                    string content = File.ReadAllText(candidate).Trim();
                    const string gitdirPrefix = "gitdir: ";
                    if (content.StartsWith(gitdirPrefix, StringComparison.Ordinal))
                    {
                        string gitdirPath = content.Substring(gitdirPrefix.Length).Trim();
                        if (!Path.IsPathRooted(gitdirPath))
                            gitdirPath = Path.GetFullPath(Path.Combine(dir.FullName, gitdirPath));
                        if (Directory.Exists(gitdirPath))
                            return gitdirPath;
                    }
                }

                dir = dir.Parent;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void CollectLocalGitMetadata(Dictionary<string, string> properties, string gitDir, bool includeAuthor)
    {
        try
        {
            string headPath = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headPath))
                return;

            string headContent = File.ReadAllText(headPath).Trim();
            const string headsRefPrefix = "ref: refs/heads/";
            const string refAnnotation = "ref: ";
            bool isDetachedHead;
            string? branch;
            string? sha;

            if (headContent.StartsWith(headsRefPrefix, StringComparison.Ordinal))
            {
                // Normal branch checkout
                isDetachedHead = false;
                branch = headContent.Substring(headsRefPrefix.Length);
                sha = ResolveCommitSha(gitDir, branch);
            }
            else if (headContent.StartsWith(refAnnotation, StringComparison.Ordinal))
            {
                // Symbolic ref to a non-branch (tag, remote, etc.) — not detached HEAD,
                // but we cannot safely resolve a branch name or SHA without following the full ref chain.
                isDetachedHead = false;
                branch = null;
                sha = null;
            }
            else if (IsValidSha(headContent))
            {
                // Raw SHA — truly detached HEAD
                isDetachedHead = true;
                branch = null;
                sha = headContent;
            }
            else
            {
                // Unrecognized HEAD format — skip metadata collection
                return;
            }

            if (isDetachedHead)
            {
                properties["IsDetachedHead"] = "true";
            }

            if (branch is not null)
            {
                AddIfNotNull(properties, "Git.Branch", branch);
            }

            AddIfNotNull(properties, "Git.SHA", sha);

            if (includeAuthor)
            {
                string? authorName = ReadGitConfigUserName(gitDir);
                AddIfNotNull(properties, "Git.Actor", authorName);
            }

            bool? hasStagedChanges = DetectStagedChanges(gitDir, branch);
            if (hasStagedChanges.HasValue)
            {
                properties["HasStagedChanges"] = hasStagedChanges.Value ? "true" : "false";
            }
        }
        catch
        {
            // Never throw from environment detection methods
        }
    }

    private static string? ResolveCommitSha(string gitDir, string branch)
    {
        try
        {
            string refFilePath = Path.Combine(gitDir, "refs", "heads", branch);
            if (File.Exists(refFilePath))
            {
                return File.ReadAllText(refFilePath).Trim();
            }

            // Fall back to packed-refs
            string packedRefsPath = Path.Combine(gitDir, "packed-refs");
            if (File.Exists(packedRefsPath))
            {
                string target = $"refs/heads/{branch}";
                foreach (string line in File.ReadAllLines(packedRefsPath))
                {
                    if (line.EndsWith(target, StringComparison.Ordinal))
                    {
                        int spaceIndex = line.IndexOf(' ');
                        if (spaceIndex > 0)
                        {
                            return line.Substring(0, spaceIndex);
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string? ReadGitConfigUserName(string gitDir)
    {
        // Check local repo config first, then fall back to global git config (~/.gitconfig or
        // XDG_CONFIG_HOME/git/config), which is where most users set their name.
        string localConfig = Path.Combine(gitDir, "config");
        string? result = ReadUserNameFromFile(localConfig);
        if (result is not null)
            return result;

        // Global config: $HOME/.gitconfig
        string? home = GetVariable("HOME")
                       ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            result = ReadUserNameFromFile(Path.Combine(home, ".gitconfig"));
            if (result is not null)
                return result;

            // XDG-compliant location: $XDG_CONFIG_HOME/git/config (defaults to ~/.config/git/config)
            string xdgConfigHome = GetVariable("XDG_CONFIG_HOME")
                                   ?? Path.Combine(home, ".config");
            result = ReadUserNameFromFile(Path.Combine(xdgConfigHome, "git", "config"));
            if (result is not null)
                return result;
        }

        return null;
    }

    private static string? ReadUserNameFromFile(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return null;

            bool inUserSection = false;
            foreach (string line in File.ReadAllLines(configPath))
            {
                string trimmed = line.Trim();

                if (trimmed == "[user]")
                {
                    inUserSection = true;
                    continue;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && inUserSection)
                {
                    break;
                }

                if (inUserSection)
                {
                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex < 0)
                    {
                        continue;
                    }

                    string key = trimmed.Substring(0, eqIndex).Trim();
                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Substring(eqIndex + 1).Trim();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // Heuristic: compares the mtime of .git/index (updated on git add) against the mtime of
    // the last-commit ref file. Returns true if the index appears newer than the last commit,
    // suggesting staged-but-not-yet-committed changes. Does NOT detect unstaged working-tree
    // edits. Property key is "HasStagedChanges" to reflect this limitation accurately.
    private static bool? DetectStagedChanges(string gitDir, string? branch)
    {
        try
        {
            string indexFile = Path.Combine(gitDir, "index");
            if (!File.Exists(indexFile))
                return null;

            DateTime indexModified = File.GetLastWriteTimeUtc(indexFile);

            // Try loose ref first
            if (branch is not null)
            {
                string loosePath = Path.Combine(gitDir, "refs", "heads", branch);
                if (File.Exists(loosePath))
                    return indexModified > File.GetLastWriteTimeUtc(loosePath);
            }

            // Fall back to packed-refs mtime as a conservative proxy
            string packedRefsPath = Path.Combine(gitDir, "packed-refs");
            if (File.Exists(packedRefsPath))
                return indexModified > File.GetLastWriteTimeUtc(packedRefsPath);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidSha(string value)
    {
        if (value.Length is not 40 and not 64)
            return false;
        foreach (char c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }

    private static void AddIfNotNull(Dictionary<string, string> dictionary, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            dictionary[key] = value!;
        }
    }

    // CI.Repository is owner/name only, so the server is what tells GitHub Enterprise's acme/api apart
    // from github.com's. It is normalized the way PullRequestContext.ServerUrl is, so the two compare.
    private static void AddServerUrl(Dictionary<string, string> dictionary, string? serverUrl)
    {
        if (serverUrl is not null)
            dictionary["CI.ServerUrl"] = serverUrl;
    }

    private static string? ServerUrlOf(string? raw) =>
        PullRequestEnvironment.TryNormalizeServerUrl(raw, out string? serverUrl) ? serverUrl : null;

    // Azure Repos takes the server from the collection, as AzureDevOpsPullRequestDetector does: the
    // repository URI's host alone would give https://fabrikam.visualstudio.com for an organization the
    // PR context files under https://dev.azure.com, and would drop a Server's virtual directory (/tfs).
    private string? AzurePipelinesServerUrl()
    {
        if (string.Equals(GetVariable("BUILD_REPOSITORY_PROVIDER"), "TfsGit", StringComparison.OrdinalIgnoreCase))
        {
            string? collectionUri = GetVariable("SYSTEM_COLLECTIONURI");
            return collectionUri is null ? null : AzureDevOpsPullRequestDetector.ParseCollection(collectionUri)?.ServerUrl;
        }

        return PullRequestEnvironment.TryGetServerRoot(GetVariable("BUILD_REPOSITORY_URI"), out string? serverUrl)
            ? serverUrl
            : null;
    }

    private static void AddPullRequestFlag(Dictionary<string, string> dictionary, bool isPullRequest) =>
        dictionary["CI.IsPullRequest"] = isPullRequest ? "true" : "false";

    private bool HasValue(string variable) =>
        !string.IsNullOrEmpty(GetVariable(variable));

    private static string? GetFirstNonEmptyValue(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ExtractBranchName(string? gitRef)
    {
        if (string.IsNullOrWhiteSpace(gitRef))
        {
            return null;
        }

        const string headsPrefix = "refs/heads/";
        return gitRef!.StartsWith(headsPrefix, StringComparison.OrdinalIgnoreCase)
            ? gitRef.Substring(headsPrefix.Length, gitRef.Length - headsPrefix.Length)
            : gitRef;
    }

    // Not BUILD_SOURCEBRANCHNAME for a Git ref: it is the ref's last segment only, so feature/main would
    // read as main. A PR build's BUILD_SOURCEBRANCH is refs/pull/N/merge, so the PR's source is taken;
    // Azure Repos gives it as refs/heads/feature/x, GitHub repos as a bare feature/x. A TFVC build's
    // BUILD_SOURCEBRANCH is a server path ($/project/main), which only BUILD_SOURCEBRANCHNAME names.
    private string? AzurePipelinesBranch(bool isPullRequest)
    {
        if (isPullRequest)
            return ExtractBranchName(GetVariable("SYSTEM_PULLREQUEST_SOURCEBRANCH"));

        string? sourceBranch = GetVariable("BUILD_SOURCEBRANCH");
        return sourceBranch is not null && sourceBranch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase)
            ? BranchOrTagOfRef(sourceBranch)
            : GetVariable("BUILD_SOURCEBRANCHNAME");
    }

    // A tag build sends the tag name, as GITHUB_REF_NAME and CI_COMMIT_REF_NAME do, so Xping Cloud
    // files it as a non-default ref rather than an unplaceable run. Any other ref (refs/pull/N/merge)
    // names no branch and yields nothing.
    private static string? BranchOrTagOfRef(string? gitRef)
    {
        if (gitRef is null)
            return null;

        foreach (string prefix in (string[])["refs/heads/", "refs/tags/"])
        {
            if (gitRef.Length > prefix.Length && gitRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return gitRef.Substring(prefix.Length);
        }

        return null;
    }

    // Freestyle jobs report GIT_BRANCH as origin/main. Only the origin remote is stripped: a job that
    // checks out to a local branch reports a bare feature/x, whose first segment is not a remote.
    private static string? StripOriginRemote(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return null;

        foreach (string prefix in (string[])["refs/remotes/origin/", "origin/"])
        {
            if (branch!.StartsWith(prefix, StringComparison.Ordinal) && branch.Length > prefix.Length)
                return branch.Substring(prefix.Length);
        }

        return branch;
    }

    // repository.default_branch from the event payload. It is optional context, so a missing or
    // unreadable payload (a container without the runner's temp directory, an old GitHub Enterprise
    // schedule payload without "repository") just leaves it out.
    private string? ReadGitHubDefaultBranch()
    {
        string? eventPath = GetVariable("GITHUB_EVENT_PATH");
        if (string.IsNullOrWhiteSpace(eventPath))
            return null;

        try
        {
            return GitHubEventPayload.Read(eventPath!, _serializer)?.Repository?.DefaultBranch;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The provider is replaceable, and a lazy caches an exception: one throwing read would fail
    // every later detection call instead of just leaving that variable out.
    private string? GetVariable(string variable)
    {
        try
        {
            return _env.GetVariable(variable);
        }
        catch
        {
            return null;
        }
    }

    private enum CIPlatform
    {
        GitHubActions,
        AzureDevOps,
        Jenkins,
        GitLabCI,
        CircleCI,
        TravisCI,
        TeamCity,
        BitbucketPipelines,
        AppVeyor,
        Generic,
    }
}
