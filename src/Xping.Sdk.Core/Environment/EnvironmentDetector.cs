/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA1031 // Do not catch general exception types - we want to handle all errors gracefully

namespace Xping.Sdk.Core.Environment;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Xping.Sdk.Core.Models;

/// <summary>
/// Default implementation of <see cref="IEnvironmentDetector"/> that detects environment information
/// from the runtime, operating system, and environment variables.
/// </summary>
public sealed class EnvironmentDetector : IEnvironmentDetector
{
    private static readonly Lazy<string> CachedOperatingSystem = new(() => DetectOperatingSystem());
    private static readonly Lazy<string> CachedRuntimeVersion = new(() => DetectRuntimeVersion());
    private static readonly Lazy<string> CachedFramework = new(() => DetectFramework());
    private static readonly Lazy<CIPlatform?> CachedCIPlatform = new(() => DetectCIPlatform());
    private static readonly Lazy<bool> CachedIsContainer = new(() => DetectIsContainer());

    /// <inheritdoc/>
    public EnvironmentInfo Detect()
    {
        var ciPlatform = CachedCIPlatform.Value;
        var isCI = ciPlatform.HasValue;

        var envInfo = new EnvironmentInfo
        {
            MachineName = GetMachineName(),
            OperatingSystem = CachedOperatingSystem.Value,
            RuntimeVersion = CachedRuntimeVersion.Value,
            Framework = CachedFramework.Value,
            EnvironmentName = DetectEnvironmentName(isCI),
            IsCIEnvironment = isCI,
        };

        // Add custom properties to the existing dictionary
        var customProps = CollectCustomProperties(ciPlatform);
        foreach (var kvp in customProps)
        {
            envInfo.CustomProperties[kvp.Key] = kvp.Value;
        }

        return envInfo;
    }

    private static string GetMachineName()
    {
        try
        {
            var machineName = Environment.MachineName;
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
            var os = Environment.OSVersion;
            var platform = os.Platform;

            // Get OS description using RuntimeInformation (cross-platform)
            var description = RuntimeInformation.OSDescription;

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
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"Linux ({description})";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
            var runtimeVersion = RuntimeInformation.FrameworkDescription;
            return runtimeVersion;
        }
        catch
        {
            return "Unknown Runtime";
        }
    }

    private static string DetectFramework()
    {
        try
        {
            // Detect framework type based on runtime
            var frameworkDescription = RuntimeInformation.FrameworkDescription;
            var os = CachedOperatingSystem.Value;

            // Check for mobile-specific frameworks first
            if (os.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // .NET for Android (formerly Xamarin.Android)
                if (frameworkDescription.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return frameworkDescription; // Return full description like ".NET 9.0-android"
                }

                return $".NET for Android ({frameworkDescription})";
            }

            if (os.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                os.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                os.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // .NET for iOS (formerly Xamarin.iOS)
                if (frameworkDescription.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return frameworkDescription; // Return full description like ".NET 9.0-ios"
                }

                return $".NET for iOS ({frameworkDescription})";
            }

            // Desktop frameworks
            if (frameworkDescription.IndexOf(".NET Framework", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Framework";
            }
            else if (frameworkDescription.IndexOf(".NET Core", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Core";
            }
            else if (frameworkDescription.IndexOf(".NET Native", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".NET Native";
            }
            else if (frameworkDescription.IndexOf(".NET", StringComparison.OrdinalIgnoreCase) >= 0)
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

    private static CIPlatform? DetectCIPlatform()
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

    private static string DetectEnvironmentName(bool isCI)
    {
        // Check explicit environment variable
        var envName = GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? GetEnvironmentVariable("ENVIRONMENT")
            ?? GetEnvironmentVariable("XPING_ENVIRONMENT");

        if (!string.IsNullOrWhiteSpace(envName))
        {
            return envName;
        }

        // Infer from CI status
        if (isCI)
        {
            return "CI";
        }

        // Default to Local
        return "Local";
    }

    private static bool DetectIsContainer()
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
                    var cgroupContent = File.ReadAllText("/proc/1/cgroup");
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

    private static Dictionary<string, string> CollectCustomProperties(CIPlatform? ciPlatform)
    {
        var properties = new Dictionary<string, string>();

        // Add container information
        if (CachedIsContainer.Value)
        {
            properties["IsContainer"] = "true";
        }

        // Add mobile platform detection
        var os = CachedOperatingSystem.Value;
        if (os.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Android";
            properties["IsMobile"] = "true";
        }
        else if (os.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "iOS";
            properties["IsMobile"] = "true";

            // Distinguish between iPhone and iPad
            if (os.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                properties["DeviceType"] = "iPad";
            }
            else if (os.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                properties["DeviceType"] = "iPhone";
            }
        }
        else if (os.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Windows";
        }
        else if (os.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            properties["Platform"] = "Linux";
        }
        else if (os.IndexOf("macOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 os.IndexOf("OSX", StringComparison.OrdinalIgnoreCase) >= 0)
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
        properties["ProcessorCount"] = Environment.ProcessorCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return properties;
    }

    private static void AddIfNotNull(Dictionary<string, string> dictionary, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            dictionary[key] = value;
        }
    }

    private static string GetEnvironmentVariable(string variable)
    {
        try
        {
            return Environment.GetEnvironmentVariable(variable);
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

#pragma warning restore CA1031
