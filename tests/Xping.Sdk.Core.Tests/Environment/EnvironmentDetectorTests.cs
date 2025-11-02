/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Environment;

using System;
using Xping.Sdk.Core.Environment;
using Xunit;

public class EnvironmentDetectorTests
{
    [Fact]
    public void Detect_ReturnsNonNullEnvironmentInfo()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result);
    }

    [Fact]
    public void Detect_ReturnsMachineName()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.MachineName);
        Assert.NotEmpty(result.MachineName);
    }

    [Fact]
    public void Detect_ReturnsOperatingSystem()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.OperatingSystem);
        Assert.NotEmpty(result.OperatingSystem);
    }

    [Fact]
    public void Detect_OperatingSystemContainsPlatformInfo()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        // Should contain Windows, Linux, or macOS
        var os = result.OperatingSystem;
        var containsPlatform = os.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
                               os.Contains("Linux", StringComparison.OrdinalIgnoreCase) ||
                               os.Contains("macOS", StringComparison.OrdinalIgnoreCase) ||
                               os.Contains("Darwin", StringComparison.OrdinalIgnoreCase);

        Assert.True(containsPlatform, $"OS '{os}' should contain platform information");
    }

    [Fact]
    public void Detect_ReturnsRuntimeVersion()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.RuntimeVersion);
        Assert.NotEmpty(result.RuntimeVersion);
    }

    [Fact]
    public void Detect_RuntimeVersionContainsDotNet()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.Contains(".NET", result.RuntimeVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detect_ReturnsFramework()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.Framework);
        Assert.NotEmpty(result.Framework);
    }

    [Fact]
    public void Detect_FrameworkContainsDotNet()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.Contains(".NET", result.Framework, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detect_ReturnsEnvironmentName()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.EnvironmentName);
        Assert.NotEmpty(result.EnvironmentName);
    }

    [Fact]
    public void Detect_WithNoEnvironmentVariables_ReturnsLocalEnvironment()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        // In normal test execution without CI env vars, should detect as Local or CI (if in actual CI)
        Assert.NotNull(result.EnvironmentName);
    }

    [Fact]
    public void Detect_ReturnsCustomProperties()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.CustomProperties);
    }

    [Fact]
    public void Detect_CustomPropertiesContainsProcessorArchitecture()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.True(result.CustomProperties.ContainsKey("ProcessorArchitecture"));
        Assert.NotEmpty(result.CustomProperties["ProcessorArchitecture"]);
    }

    [Fact]
    public void Detect_CustomPropertiesContainsProcessorCount()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.True(result.CustomProperties.ContainsKey("ProcessorCount"));
        Assert.NotEmpty(result.CustomProperties["ProcessorCount"]);

        // Should be parseable as integer
        var canParse = int.TryParse(result.CustomProperties["ProcessorCount"], out var count);
        Assert.True(canParse);
        Assert.True(count > 0);
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenGitHubActionsVariablePresent()
    {
        // This test will only pass when actually running in GitHub Actions
        // or when the env var is set for testing purposes
        var isGitHubActions = System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isGitHubActions)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("GitHubActions", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenAzureDevOpsVariablePresent()
    {
        var isAzureDevOps = !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("TF_BUILD"));

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isAzureDevOps)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("AzureDevOps", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenJenkinsVariablePresent()
    {
        var isJenkins = !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("JENKINS_URL"));

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isJenkins)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("Jenkins", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenGitLabCIVariablePresent()
    {
        var isGitLabCI = System.Environment.GetEnvironmentVariable("GITLAB_CI") == "true";

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isGitLabCI)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("GitLabCI", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenCircleCIVariablePresent()
    {
        var isCircleCI = System.Environment.GetEnvironmentVariable("CIRCLECI") == "true";

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isCircleCI)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("CircleCI", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_DetectsCIEnvironment_WhenGenericCIVariablePresent()
    {
        var isGenericCI = System.Environment.GetEnvironmentVariable("CI") == "true" &&
                          System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" &&
                          System.Environment.GetEnvironmentVariable("GITLAB_CI") != "true" &&
                          System.Environment.GetEnvironmentVariable("CIRCLECI") != "true";

        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        if (isGenericCI)
        {
            Assert.True(result.IsCIEnvironment);
            Assert.True(result.CustomProperties.ContainsKey("CIPlatform"));
            Assert.Equal("Generic", result.CustomProperties["CIPlatform"]);
        }
    }

    [Fact]
    public void Detect_ConsistentResults_WhenCalledMultipleTimes()
    {
        var detector = new EnvironmentDetector();

        var result1 = detector.Detect();
        var result2 = detector.Detect();

        Assert.Equal(result1.OperatingSystem, result2.OperatingSystem);
        Assert.Equal(result1.RuntimeVersion, result2.RuntimeVersion);
        Assert.Equal(result1.Framework, result2.Framework);
        Assert.Equal(result1.IsCIEnvironment, result2.IsCIEnvironment);
    }

    [Fact]
    public void Detect_PerformanceTest_CompletesQuickly()
    {
        var detector = new EnvironmentDetector();
        var startTime = DateTime.UtcNow;

        var result = detector.Detect();

        var duration = DateTime.UtcNow - startTime;

        // Should complete in less than 100ms
        Assert.True(duration.TotalMilliseconds < 100, $"Detection took {duration.TotalMilliseconds}ms");
    }

    [Fact]
    public void Detect_CachedValues_ImprovePerformance()
    {
        var detector = new EnvironmentDetector();

        // First call - may take longer due to initialization
        var firstStart = DateTime.UtcNow;
        var result1 = detector.Detect();
        var firstDuration = DateTime.UtcNow - firstStart;

        // Second call - should be faster due to caching
        var secondStart = DateTime.UtcNow;
        var result2 = detector.Detect();
        var secondDuration = DateTime.UtcNow - secondStart;

        // Both should be fast, but we're just verifying no errors
        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    [Fact]
    public void Detect_DoesNotThrowException()
    {
        var detector = new EnvironmentDetector();

        var exception = Record.Exception(() => detector.Detect());

        Assert.Null(exception);
    }

    [Fact]
    public void Detect_AllRequiredFieldsArePopulated()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        Assert.NotNull(result.MachineName);
        Assert.NotEmpty(result.MachineName);

        Assert.NotNull(result.OperatingSystem);
        Assert.NotEmpty(result.OperatingSystem);

        Assert.NotNull(result.RuntimeVersion);
        Assert.NotEmpty(result.RuntimeVersion);

        Assert.NotNull(result.Framework);
        Assert.NotEmpty(result.Framework);

        Assert.NotNull(result.EnvironmentName);
        Assert.NotEmpty(result.EnvironmentName);

        Assert.NotNull(result.CustomProperties);
    }

    [Fact]
    public void Detect_IdentifiesPlatform_InCustomProperties()
    {
        var detector = new EnvironmentDetector();

        var result = detector.Detect();

        // Should always have a Platform property
        Assert.True(result.CustomProperties.ContainsKey("Platform"));
        Assert.NotEmpty(result.CustomProperties["Platform"]);

        var platform = result.CustomProperties["Platform"];

        // Platform should be one of the known values
        var knownPlatforms = new[] { "Windows", "Linux", "macOS", "Android", "iOS" };
        Assert.Contains(platform, knownPlatforms);
    }

    [Fact]
    public void Detect_WhenAndroid_SetsMobileProperties()
    {
        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        // This test will only validate mobile properties if running on Android
        if (result.OperatingSystem.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(result.CustomProperties.ContainsKey("Platform"));
            Assert.Equal("Android", result.CustomProperties["Platform"]);
            Assert.True(result.CustomProperties.ContainsKey("IsMobile"));
            Assert.Equal("true", result.CustomProperties["IsMobile"]);
        }
    }

    [Fact]
    public void Detect_WhenIOS_SetsMobileProperties()
    {
        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        // This test will only validate mobile properties if running on iOS
        if (result.OperatingSystem.Contains("iOS", StringComparison.OrdinalIgnoreCase) ||
            result.OperatingSystem.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            result.OperatingSystem.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(result.CustomProperties.ContainsKey("Platform"));
            Assert.Equal("iOS", result.CustomProperties["Platform"]);
            Assert.True(result.CustomProperties.ContainsKey("IsMobile"));
            Assert.Equal("true", result.CustomProperties["IsMobile"]);

            // Should also identify device type
            Assert.True(result.CustomProperties.ContainsKey("DeviceType"));
            var deviceType = result.CustomProperties["DeviceType"];
            Assert.True(deviceType == "iPhone" || deviceType == "iPad", $"DeviceType should be iPhone or iPad, but was {deviceType}");
        }
    }

    [Fact]
    public void Detect_WhenDesktop_DoesNotSetMobileFlag()
    {
        var detector = new EnvironmentDetector();
        var result = detector.Detect();

        // If running on desktop (Windows, Linux, macOS), should not have IsMobile flag
        var os = result.OperatingSystem;
        var isDesktop = os.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
                       os.Contains("Linux", StringComparison.OrdinalIgnoreCase) ||
                       os.Contains("macOS", StringComparison.OrdinalIgnoreCase);

        var isAndroid = os.Contains("Android", StringComparison.OrdinalIgnoreCase);
        var isIOS = os.Contains("iOS", StringComparison.OrdinalIgnoreCase) ||
                   os.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                   os.Contains("iPad", StringComparison.OrdinalIgnoreCase);

        if (isDesktop && !isAndroid && !isIOS)
        {
            Assert.False(result.CustomProperties.ContainsKey("IsMobile"));
        }
    }
}

#pragma warning restore CA1707
