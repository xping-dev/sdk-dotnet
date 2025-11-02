/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Models;

using Xping.Sdk.Core.Models;

public class EnvironmentInfoTests
{
    [Fact]
    public void ConstructorShouldInitializeCustomProperties()
    {
        // Act
        var environmentInfo = new EnvironmentInfo();

        // Assert
        Assert.NotNull(environmentInfo.CustomProperties);
        Assert.Empty(environmentInfo.CustomProperties);
    }

    [Fact]
    public void ShouldAllowSettingAllProperties()
    {
        // Arrange
        var customProps = new Dictionary<string, string>
        {
            { "Branch", "main" },
            { "Commit", "abc123" }
        };

        // Act
        var environmentInfo = new EnvironmentInfo
        {
            MachineName = "test-machine",
            OperatingSystem = "macOS 14.0",
            RuntimeVersion = ".NET 8.0.0",
            Framework = "NUnit 4.0.1",
            EnvironmentName = "CI",
            IsCIEnvironment = true
        };

        foreach (var kvp in customProps)
        {
            environmentInfo.CustomProperties[kvp.Key] = kvp.Value;
        }

        // Assert
        Assert.Equal("test-machine", environmentInfo.MachineName);
        Assert.Equal("macOS 14.0", environmentInfo.OperatingSystem);
        Assert.Equal(".NET 8.0.0", environmentInfo.RuntimeVersion);
        Assert.Equal("NUnit 4.0.1", environmentInfo.Framework);
        Assert.Equal("CI", environmentInfo.EnvironmentName);
        Assert.True(environmentInfo.IsCIEnvironment);
        Assert.Equal(2, environmentInfo.CustomProperties.Count);
        Assert.Equal("main", environmentInfo.CustomProperties["Branch"]);
        Assert.Equal("abc123", environmentInfo.CustomProperties["Commit"]);
    }

    [Fact]
    public void CustomPropertiesShouldBeReadOnly()
    {
        // Arrange
        var environmentInfo = new EnvironmentInfo();
        var originalDict = environmentInfo.CustomProperties;

        // Act & Assert - Should not be able to reassign
        var property = typeof(EnvironmentInfo).GetProperty(nameof(EnvironmentInfo.CustomProperties));
        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void IsCIEnvironmentShouldDefaultToFalse()
    {
        // Act
        var environmentInfo = new EnvironmentInfo();

        // Assert
        Assert.False(environmentInfo.IsCIEnvironment);
    }
}
