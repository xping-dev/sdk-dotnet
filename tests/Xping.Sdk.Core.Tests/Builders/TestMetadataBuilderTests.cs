/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class TestMetadataBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnMetadata_WithEmptyCollections()
    {
        var metadata = new TestMetadataBuilder().Build();

        Assert.Empty(metadata.Categories);
        Assert.Empty(metadata.Tags);
        Assert.Empty(metadata.CustomAttributes);
        Assert.Null(metadata.Description);
    }

    // ---------------------------------------------------------------------------
    // AddCategory
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCategory_ShouldAddCategory()
    {
        var metadata = new TestMetadataBuilder().AddCategory("Regression").Build();
        Assert.Contains("Regression", metadata.Categories);
    }

    [Fact]
    public void AddCategory_WhitespaceOnly_ShouldNotAdd()
    {
        var metadata = new TestMetadataBuilder().AddCategory("   ").Build();
        Assert.Empty(metadata.Categories);
    }

    [Fact]
    public void AddCategory_EmptyString_ShouldNotAdd()
    {
        var metadata = new TestMetadataBuilder().AddCategory(string.Empty).Build();
        Assert.Empty(metadata.Categories);
    }

    // ---------------------------------------------------------------------------
    // AddCategories
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCategories_ShouldAddAllNonWhitespace()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategories(["Unit", "Integration"])
            .Build();

        Assert.Contains("Unit", metadata.Categories);
        Assert.Contains("Integration", metadata.Categories);
        Assert.Equal(2, metadata.Categories.Count);
    }

    [Fact]
    public void AddCategories_FiltersWhitespaceItems()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategories(["Valid", "  ", string.Empty, "AlsoValid"])
            .Build();

        Assert.Equal(2, metadata.Categories.Count);
        Assert.Contains("Valid", metadata.Categories);
        Assert.Contains("AlsoValid", metadata.Categories);
    }

    [Fact]
    public void AddCategories_EmptyList_ShouldNotModify()
    {
        var metadata = new TestMetadataBuilder().AddCategories([]).Build();
        Assert.Empty(metadata.Categories);
    }

    [Fact]
    public void AddCategories_NullList_ShouldNotThrow_AndNotModify()
    {
        // null list → pattern `categories is { Count: > 0 }` is false, so no-op
        var builder = new TestMetadataBuilder();
        var exception = Record.Exception(() => builder.AddCategories(null!));
        Assert.Null(exception);
        Assert.Empty(builder.Build().Categories);
    }

    // ---------------------------------------------------------------------------
    // AddTag
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddTag_ShouldAddTag()
    {
        var metadata = new TestMetadataBuilder().AddTag("smoke").Build();
        Assert.Contains("smoke", metadata.Tags);
    }

    [Fact]
    public void AddTag_WhitespaceOnly_ShouldNotAdd()
    {
        var metadata = new TestMetadataBuilder().AddTag("  ").Build();
        Assert.Empty(metadata.Tags);
    }

    // ---------------------------------------------------------------------------
    // AddTags
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddTags_ShouldAddAllNonWhitespace()
    {
        var metadata = new TestMetadataBuilder().AddTags(["alpha", "beta"]).Build();
        Assert.Contains("alpha", metadata.Tags);
        Assert.Contains("beta", metadata.Tags);
    }

    [Fact]
    public void AddTags_EmptyList_ShouldNotModify()
    {
        var metadata = new TestMetadataBuilder().AddTags([]).Build();
        Assert.Empty(metadata.Tags);
    }

    // ---------------------------------------------------------------------------
    // AddCustomAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCustomAttribute_ShouldAddAttribute()
    {
        var metadata = new TestMetadataBuilder().AddCustomAttribute("owner", "team-a").Build();
        Assert.True(metadata.CustomAttributes.ContainsKey("owner"));
        Assert.Equal("team-a", metadata.CustomAttributes["owner"]);
    }

    [Fact]
    public void AddCustomAttribute_EmptyKey_ShouldNotAdd()
    {
        var metadata = new TestMetadataBuilder().AddCustomAttribute(string.Empty, "value").Build();
        Assert.Empty(metadata.CustomAttributes);
    }

    [Fact]
    public void AddCustomAttribute_OverwritesExistingKey()
    {
        var metadata = new TestMetadataBuilder()
            .AddCustomAttribute("k", "first")
            .AddCustomAttribute("k", "second")
            .Build();

        Assert.Equal("second", metadata.CustomAttributes["k"]);
    }

    // ---------------------------------------------------------------------------
    // AddCustomAttributes
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCustomAttributes_ShouldAddAllEntries()
    {
        var attrs = new Dictionary<string, string> { { "x", "1" }, { "y", "2" } };
        var metadata = new TestMetadataBuilder().AddCustomAttributes(attrs).Build();

        Assert.Equal(2, metadata.CustomAttributes.Count);
        Assert.Equal("1", metadata.CustomAttributes["x"]);
    }

    [Fact]
    public void AddCustomAttributes_EmptyDictionary_ShouldNotModify()
    {
        var metadata = new TestMetadataBuilder()
            .AddCustomAttributes(new Dictionary<string, string>())
            .Build();

        Assert.Empty(metadata.CustomAttributes);
    }

    // ---------------------------------------------------------------------------
    // WithDescription
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDescription_ShouldSetDescription()
    {
        var metadata = new TestMetadataBuilder().WithDescription("Verifies edge case X").Build();
        Assert.Equal("Verifies edge case X", metadata.Description);
    }

    [Fact]
    public void WithDescription_Null_ShouldSetNull()
    {
        var metadata = new TestMetadataBuilder()
            .WithDescription("desc")
            .WithDescription(null)
            .Build();

        Assert.Null(metadata.Description);
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldClearAllCollectionsAndDescription()
    {
        var builder = new TestMetadataBuilder()
            .AddCategory("Cat")
            .AddTag("tag")
            .AddCustomAttribute("k", "v")
            .WithDescription("desc");

        builder.Reset();
        var metadata = builder.Build();

        Assert.Empty(metadata.Categories);
        Assert.Empty(metadata.Tags);
        Assert.Empty(metadata.CustomAttributes);
        Assert.Null(metadata.Description);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new TestMetadataBuilder();
        Assert.Same(builder, builder.Reset());
    }
}
