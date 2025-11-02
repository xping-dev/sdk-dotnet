/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Models;

using Xping.Sdk.Core.Models;

public class TestMetadataTests
{
    [Fact]
    public void ConstructorShouldInitializeCollections()
    {
        // Act
        var metadata = new TestMetadata();

        // Assert
        Assert.NotNull(metadata.Categories);
        Assert.Empty(metadata.Categories);
        Assert.NotNull(metadata.Tags);
        Assert.Empty(metadata.Tags);
        Assert.NotNull(metadata.CustomAttributes);
        Assert.Empty(metadata.CustomAttributes);
    }

    [Fact]
    public void ShouldAllowSettingCategories()
    {
        // Arrange
        var categories = new List<string> { "Integration", "Database" };

        // Act
        var metadata = new TestMetadata
        {
            Categories = categories
        };

        // Assert
        Assert.Equal(2, metadata.Categories.Count);
        Assert.Contains("Integration", metadata.Categories);
        Assert.Contains("Database", metadata.Categories);
    }

    [Fact]
    public void ShouldAllowSettingTags()
    {
        // Arrange
        var tags = new List<string> { "smoke", "critical" };

        // Act
        var metadata = new TestMetadata
        {
            Tags = tags
        };

        // Assert
        Assert.Equal(2, metadata.Tags.Count);
        Assert.Contains("smoke", metadata.Tags);
        Assert.Contains("critical", metadata.Tags);
    }

    [Fact]
    public void ShouldAllowSettingDescription()
    {
        // Arrange
        const string description = "Tests the calculator addition function";

        // Act
        var metadata = new TestMetadata
        {
            Description = description
        };

        // Assert
        Assert.Equal(description, metadata.Description);
    }

    [Fact]
    public void ShouldAllowAddingCustomAttributes()
    {
        // Arrange
        var metadata = new TestMetadata();

        // Act
        metadata.CustomAttributes["Priority"] = "High";
        metadata.CustomAttributes["Owner"] = "TeamA";

        // Assert
        Assert.Equal(2, metadata.CustomAttributes.Count);
        Assert.Equal("High", metadata.CustomAttributes["Priority"]);
        Assert.Equal("TeamA", metadata.CustomAttributes["Owner"]);
    }

    [Fact]
    public void CustomAttributesShouldBeReadOnly()
    {
        // Arrange
        var metadata = new TestMetadata();

        // Act & Assert - Should not be able to reassign
        var property = typeof(TestMetadata).GetProperty(nameof(TestMetadata.CustomAttributes));
        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void ShouldSupportEmptyMetadata()
    {
        // Act
        var metadata = new TestMetadata();

        // Assert
        Assert.Empty(metadata.Categories);
        Assert.Empty(metadata.Tags);
        Assert.Empty(metadata.CustomAttributes);
        Assert.Null(metadata.Description);
    }
}
