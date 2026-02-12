/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.Network;

namespace Xping.Sdk.Core.Services.Environment.Internals;

/// <summary>
/// Default implementation of <see cref="IEnvironmentDetector"/> that detects environment information
/// from the runtime, operating system, and environment variables.
/// Uses instance-level caching for DI-friendly behavior.
/// </summary>
internal sealed class EnvironmentDetector : IEnvironmentDetector
{
    private readonly EnvironmentInfoBuilder _builder = new();
    private readonly XpingConfiguration _configuration;
    private readonly INetworkMetricsCollector _networkMetricsCollector;

    // Instance-level lazy initialization for thread-safe, cached detection
    private readonly Lazy<string> _machineName;
    private readonly Lazy<string> _operatingSystem;
    private readonly Lazy<string> _runtimeVersion;
    private readonly Lazy<string> _framework;
    private readonly Lazy<string> _environmentName;
    private readonly Lazy<CIPlatform?> _ciPlatform;
    private readonly Lazy<bool> _isContainer;
    private readonly Lazy<Dictionary<string, string>> _customProperties;

    // Cache network metrics per endpoint (instance-level, thread-safe)
    private readonly ConcurrentDictionary<string, NetworkMetrics?> _networkMetricsCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentDetector"/> class.
    /// </summary>
    public EnvironmentDetector(
        IOptions<XpingConfiguration> options,
        INetworkMetricsCollector networkMetricsCollector)
    {
        _configuration = options.Value;
        _networkMetricsCollector = networkMetricsCollector;

        // Initialize instance-level lazy fields
        _machineName = new Lazy<string>(GetMachineName);
        _operatingSystem = new Lazy<string>(DetectOperatingSystem);
        _runtimeVersion = new Lazy<string>(DetectRuntimeVersion);
        _framework = new Lazy<string>(() => DetectFramework(_operatingSystem.Value));
        _environmentName = new Lazy<string>(() => DetectEnvironmentName(_configuration));
        _ciPlatform = new Lazy<CIPlatform?>(DetectCiPlatform);
        _isContainer = new Lazy<bool>(DetectIsContainer);
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
    string IEnvironmentDetector.EnvironmentName => _environmentName.Value;

    /// <inheritdoc/>
    bool IEnvironmentDetector.IsCiEnvironment => _ciPlatform.Value.HasValue;

    /// <inheritdoc/>
    bool IEnvironmentDetector.IsContainer => _isContainer.Value;

    /// <inheritdoc/>
    IReadOnlyDictionary<string, string> IEnvironmentDetector.CustomProperties => _customProperties.Value;

    /// <inheritdoc/>
    async Task<EnvironmentInfo> IEnvironmentDetector.BuildEnvironmentInfoAsync(CancellationToken cancellationToken)
    {
        NetworkMetrics? networkMetrics = await GetNetworkMetricsAsync(cancellationToken).ConfigureAwait(false);

        _builder.Reset();
        _builder.WithMachineName(_machineName.Value);
        _builder.WithOperatingSystem(_operatingSystem.Value);
        _builder.WithRuntimeVersion(_runtimeVersion.Value);
        _builder.WithFramework(_framework.Value);
        _builder.WithEnvironmentName(_environmentName.Value);
        _builder.WithIsCIEnvironment(_ciPlatform.Value.HasValue);
        _builder.WithNetworkMetrics(networkMetrics);
        _builder.AddCustomProperties(_customProperties.Value);

        return _builder.Build();
    }

    private async Task<NetworkMetrics?> GetNetworkMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.CollectNetworkMetrics || string.IsNullOrWhiteSpace(_configuration.ApiEndpoint))
        {
            return null;
        }

        string endpoint = _configuration.ApiEndpoint;

        // Check cache first
        if (_networkMetricsCache.TryGetValue(endpoint, out NetworkMetrics? cached))
        {
            return cached;
        }

        // Collect metrics asynchronously
        try
        {
            NetworkMetrics? metrics = await _networkMetricsCollector
                .CollectAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            // Cache the result
            _networkMetricsCache[endpoint] = metrics;
            return metrics;
        }
        catch
        {
            // Cache null result to avoid repeated failures
            _networkMetricsCache[endpoint] = null;
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
                return $"Linux ({description})";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"macOS ({description})";
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

    private static CIPlatform? DetectCiPlatform()
    {
        // GitHub Actions
        if (GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            return CIPlatform.GitHubActions;
        }

        // Azure DevOps (Azure Pipelines)
        if (!string.IsNullOrEmpty(GetEnvironmentVariable("TF_BUILD")))
        {
            return CIPlatform.AzureDevOps;
        }

        // Jenkins
        if (!string.IsNullOrEmpty(GetEnvironmentVariable("JENKINS_URL")))
        {
            return CIPlatform.Jenkins;
        }

        // GitLab CI
        if (GetEnvironmentVariable("GITLAB_CI") == "true")
        {
            return CIPlatform.GitLabCI;
        }

        // CircleCI
        if (GetEnvironmentVariable("CIRCLECI") == "true")
        {
            return CIPlatform.CircleCI;
        }

        // Travis CI
        if (GetEnvironmentVariable("TRAVIS") == "true")
        {
            return CIPlatform.TravisCI;
        }

        // TeamCity
        if (!string.IsNullOrEmpty(GetEnvironmentVariable("TEAMCITY_VERSION")))
        {
            return CIPlatform.TeamCity;
        }

        // Bitbucket Pipelines
        if (GetEnvironmentVariable("BITBUCKET_PIPELINE_UUID") != null)
        {
            return CIPlatform.BitbucketPipelines;
        }

        // AppVeyor
        if (GetEnvironmentVariable("APPVEYOR") == "True")
        {
            return CIPlatform.AppVeyor;
        }

        // Generic CI indicator
        if (GetEnvironmentVariable("CI") == "true" || GetEnvironmentVariable("CI") == "True")
        {
            return CIPlatform.Generic;
        }

        return null;
    }

    private string DetectEnvironmentName(XpingConfiguration configuration)
    {
        // Priority 1: XPING_ENVIRONMENT - explicit Xping environment variable (the highest priority)
        string? xpingEnv = GetEnvironmentVariable("XPING_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(xpingEnv))
        {
            return xpingEnv!;
        }

        // Priority 2: Auto-detect CI if enabled
        if (configuration.AutoDetectCIEnvironment && _ciPlatform.Value.HasValue)
        {
            return "CI";
        }

        // Priority 3: Use configuration property
        if (!string.IsNullOrWhiteSpace(configuration.Environment))
        {
            return configuration.Environment;
        }

        // Priority 4: Framework environment variables (ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT)
        string? aspNetCoreEnv = GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(aspNetCoreEnv))
        {
            return aspNetCoreEnv!;
        }

        string? dotnetEnv = GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(dotnetEnv))
        {
            return dotnetEnv!;
        }

        // Priority 5: Default to "Local"
        return XpingConfiguration.DefaultEnvironment;
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
            if (!string.IsNullOrEmpty(GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
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
        Dictionary<string, string> properties = new Dictionary<string, string>();

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
                    AddIfNotNull(properties, "CI.Repository", GetEnvironmentVariable("GITHUB_REPOSITORY"));
                    AddIfNotNull(properties, "CI.RunId", GetEnvironmentVariable("GITHUB_RUN_ID"));
                    AddIfNotNull(properties, "CI.RunNumber", GetEnvironmentVariable("GITHUB_RUN_NUMBER"));
                    AddIfNotNull(properties, "CI.Ref", GetEnvironmentVariable("GITHUB_REF"));
                    AddIfNotNull(properties, "CI.SHA", GetEnvironmentVariable("GITHUB_SHA"));
                    AddIfNotNull(properties, "CI.Actor", GetEnvironmentVariable("GITHUB_ACTOR"));
                    AddIfNotNull(properties, "CI.Workflow", GetEnvironmentVariable("GITHUB_WORKFLOW"));
                    break;

                case CIPlatform.AzureDevOps:
                    AddIfNotNull(properties, "CI.BuildId", GetEnvironmentVariable("BUILD_BUILDID"));
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("BUILD_BUILDNUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetEnvironmentVariable("BUILD_REPOSITORY_NAME"));
                    AddIfNotNull(properties, "CI.SourceBranch", GetEnvironmentVariable("BUILD_SOURCEBRANCH"));
                    AddIfNotNull(properties, "CI.SourceVersion", GetEnvironmentVariable("BUILD_SOURCEVERSION"));
                    AddIfNotNull(properties, "CI.RequestedFor", GetEnvironmentVariable("BUILD_REQUESTEDFOR"));
                    break;

                case CIPlatform.Jenkins:
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.JobName", GetEnvironmentVariable("JOB_NAME"));
                    AddIfNotNull(properties, "CI.BuildUrl", GetEnvironmentVariable("BUILD_URL"));
                    AddIfNotNull(properties, "CI.GitCommit", GetEnvironmentVariable("GIT_COMMIT"));
                    AddIfNotNull(properties, "CI.GitBranch", GetEnvironmentVariable("GIT_BRANCH"));
                    break;

                case CIPlatform.GitLabCI:
                    AddIfNotNull(properties, "CI.JobId", GetEnvironmentVariable("CI_JOB_ID"));
                    AddIfNotNull(properties, "CI.PipelineId", GetEnvironmentVariable("CI_PIPELINE_ID"));
                    AddIfNotNull(properties, "CI.ProjectPath", GetEnvironmentVariable("CI_PROJECT_PATH"));
                    AddIfNotNull(properties, "CI.CommitSHA", GetEnvironmentVariable("CI_COMMIT_SHA"));
                    AddIfNotNull(properties, "CI.CommitBranch", GetEnvironmentVariable("CI_COMMIT_BRANCH"));
                    AddIfNotNull(properties, "CI.CommitAuthor", GetEnvironmentVariable("CI_COMMIT_AUTHOR"));
                    break;

                case CIPlatform.CircleCI:
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("CIRCLE_BUILD_NUM"));
                    AddIfNotNull(properties, "CI.WorkflowId", GetEnvironmentVariable("CIRCLE_WORKFLOW_ID"));
                    AddIfNotNull(properties, "CI.ProjectName", GetEnvironmentVariable("CIRCLE_PROJECT_REPONAME"));
                    AddIfNotNull(properties, "CI.Branch", GetEnvironmentVariable("CIRCLE_BRANCH"));
                    AddIfNotNull(properties, "CI.SHA", GetEnvironmentVariable("CIRCLE_SHA1"));
                    AddIfNotNull(properties, "CI.Username", GetEnvironmentVariable("CIRCLE_USERNAME"));
                    break;

                case CIPlatform.TravisCI:
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("TRAVIS_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.JobNumber", GetEnvironmentVariable("TRAVIS_JOB_NUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetEnvironmentVariable("TRAVIS_REPO_SLUG"));
                    AddIfNotNull(properties, "CI.Branch", GetEnvironmentVariable("TRAVIS_BRANCH"));
                    AddIfNotNull(properties, "CI.Commit", GetEnvironmentVariable("TRAVIS_COMMIT"));
                    break;

                case CIPlatform.TeamCity:
                    AddIfNotNull(properties, "CI.BuildId", GetEnvironmentVariable("TEAMCITY_BUILD_ID"));
                    AddIfNotNull(properties, "CI.Version", GetEnvironmentVariable("TEAMCITY_VERSION"));
                    AddIfNotNull(properties, "CI.ProjectName", GetEnvironmentVariable("TEAMCITY_PROJECT_NAME"));
                    break;

                case CIPlatform.BitbucketPipelines:
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.Repository", GetEnvironmentVariable("BITBUCKET_REPO_FULL_NAME"));
                    AddIfNotNull(properties, "CI.Branch", GetEnvironmentVariable("BITBUCKET_BRANCH"));
                    AddIfNotNull(properties, "CI.Commit", GetEnvironmentVariable("BITBUCKET_COMMIT"));
                    break;

                case CIPlatform.AppVeyor:
                    AddIfNotNull(properties, "CI.BuildNumber", GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER"));
                    AddIfNotNull(properties, "CI.BuildVersion", GetEnvironmentVariable("APPVEYOR_BUILD_VERSION"));
                    AddIfNotNull(properties, "CI.Repository", GetEnvironmentVariable("APPVEYOR_REPO_NAME"));
                    AddIfNotNull(properties, "CI.Branch", GetEnvironmentVariable("APPVEYOR_REPO_BRANCH"));
                    AddIfNotNull(properties, "CI.Commit", GetEnvironmentVariable("APPVEYOR_REPO_COMMIT"));
                    break;
            }
        }

        // Add processor architecture
        properties["ProcessorArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString();

        // Add process information
        properties["ProcessorCount"] = System.Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture);

        return properties;
    }

    private static void AddIfNotNull(Dictionary<string, string> dictionary, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            dictionary[key] = value!;
        }
    }

    private static string? GetEnvironmentVariable(string variable)
    {
        try
        {
            return System.Environment.GetEnvironmentVariable(variable);
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
