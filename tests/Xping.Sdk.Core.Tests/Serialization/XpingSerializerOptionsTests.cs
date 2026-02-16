/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using System.Text.Json.Serialization;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Tests.Serialization;

public sealed class XpingSerializerOptionsTests
{
    // ---------------------------------------------------------------------------
    // ApiOptions
    // ---------------------------------------------------------------------------

    [Fact]
    public void ApiOptions_ShouldUseCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase, XpingSerializerOptions.ApiOptions.PropertyNamingPolicy);
    }

    [Fact]
    public void ApiOptions_ShouldNotWriteIndented()
    {
        Assert.False(XpingSerializerOptions.ApiOptions.WriteIndented);
    }

    [Fact]
    public void ApiOptions_ShouldIgnoreNullPropertiesWhenWriting()
    {
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, XpingSerializerOptions.ApiOptions.DefaultIgnoreCondition);
    }

    [Fact]
    public void ApiOptions_ShouldBeCaseInsensitive()
    {
        Assert.True(XpingSerializerOptions.ApiOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void ApiOptions_ShouldSerializeEnumsAsStrings()
    {
        // TestOutcome is an enum — with JsonStringEnumConverter it should serialize as a string
        var obj = new { Outcome = TestOutcome.Passed };
        var json = JsonSerializer.Serialize(obj, XpingSerializerOptions.ApiOptions);
        Assert.Contains("\"passed\"", json, StringComparison.Ordinal); // camelCase enum + string form
    }

    [Fact]
    public void ApiOptions_ShouldBeSameInstance_OnMultipleAccesses()
    {
        var opts1 = XpingSerializerOptions.ApiOptions;
        var opts2 = XpingSerializerOptions.ApiOptions;
        Assert.Same(opts1, opts2);
    }

    // ---------------------------------------------------------------------------
    // FileOptions
    // ---------------------------------------------------------------------------

    [Fact]
    public void FileOptions_ShouldUseCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase, XpingSerializerOptions.FileOptions.PropertyNamingPolicy);
    }

    [Fact]
    public void FileOptions_ShouldNotWriteIndented()
    {
        Assert.False(XpingSerializerOptions.FileOptions.WriteIndented);
    }

    [Fact]
    public void FileOptions_ShouldIgnoreNullWhenWriting()
    {
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, XpingSerializerOptions.FileOptions.DefaultIgnoreCondition);
    }

    [Fact]
    public void FileOptions_ShouldBeSameInstance_OnMultipleAccesses()
    {
        Assert.Same(XpingSerializerOptions.FileOptions, XpingSerializerOptions.FileOptions);
    }

    // ---------------------------------------------------------------------------
    // TestOptions
    // ---------------------------------------------------------------------------

    [Fact]
    public void TestOptions_ShouldWriteIndented()
    {
        Assert.True(XpingSerializerOptions.TestOptions.WriteIndented);
    }

    [Fact]
    public void TestOptions_ShouldUseCamelCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.CamelCase, XpingSerializerOptions.TestOptions.PropertyNamingPolicy);
    }

    [Fact]
    public void TestOptions_ShouldBeSameInstance_OnMultipleAccesses()
    {
        Assert.Same(XpingSerializerOptions.TestOptions, XpingSerializerOptions.TestOptions);
    }

    // ---------------------------------------------------------------------------
    // Null-suppression verification (integration)
    // ---------------------------------------------------------------------------

    [Fact]
    public void ApiOptions_ShouldOmitNullProperties_InSerializedOutput()
    {
        var obj = new { Name = "test", Optional = (string?)null };
        var json = JsonSerializer.Serialize(obj, XpingSerializerOptions.ApiOptions);
        Assert.DoesNotContain("optional", json, StringComparison.Ordinal);
        Assert.Contains("\"name\"", json, StringComparison.Ordinal);
    }
}
