/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class RetryMetadataBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnRetryMetadata_WithDefaults()
    {
        var retry = new RetryMetadataBuilder().Build();

        Assert.Equal(1, retry.AttemptNumber);
        Assert.Equal(0, retry.MaxRetries);
        Assert.False(retry.PassedOnRetry);
        Assert.Equal(TimeSpan.Zero, retry.DelayBetweenRetries);
        Assert.Null(retry.RetryReason);
        Assert.Equal(string.Empty, retry.RetryAttributeName);
        Assert.Empty(retry.AdditionalMetadata);
    }

    // ---------------------------------------------------------------------------
    // With* methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithAttemptNumber_ShouldSetAttemptNumber()
    {
        var retry = new RetryMetadataBuilder().WithAttemptNumber(3).Build();
        Assert.Equal(3, retry.AttemptNumber);
    }

    [Fact]
    public void WithMaxRetries_ShouldSetMaxRetries()
    {
        var retry = new RetryMetadataBuilder().WithMaxRetries(5).Build();
        Assert.Equal(5, retry.MaxRetries);
    }

    [Fact]
    public void WithPassedOnRetry_True_ShouldSetFlag()
    {
        var retry = new RetryMetadataBuilder().WithPassedOnRetry(true).Build();
        Assert.True(retry.PassedOnRetry);
    }

    [Fact]
    public void WithDelayBetweenRetries_ShouldSetDelay()
    {
        var delay = TimeSpan.FromSeconds(2);
        var retry = new RetryMetadataBuilder().WithDelayBetweenRetries(delay).Build();
        Assert.Equal(delay, retry.DelayBetweenRetries);
    }

    [Fact]
    public void WithRetryReason_ShouldSetRetryReason()
    {
        var retry = new RetryMetadataBuilder().WithRetryReason("Timeout").Build();
        Assert.Equal("Timeout", retry.RetryReason);
    }

    [Fact]
    public void WithRetryAttributeName_ShouldSetAttributeName()
    {
        var retry = new RetryMetadataBuilder().WithRetryAttributeName("RetryFact").Build();
        Assert.Equal("RetryFact", retry.RetryAttributeName);
    }

    // ---------------------------------------------------------------------------
    // AddMetadata (single)
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddMetadata_Single_ShouldAddEntry()
    {
        var retry = new RetryMetadataBuilder().AddMetadata("key1", "value1").Build();
        Assert.True(retry.AdditionalMetadata.ContainsKey("key1"));
        Assert.Equal("value1", retry.AdditionalMetadata["key1"]);
    }

    [Fact]
    public void AddMetadata_EmptyKey_ShouldNotAdd()
    {
        var retry = new RetryMetadataBuilder().AddMetadata(string.Empty, "value").Build();
        Assert.Empty(retry.AdditionalMetadata);
    }

    [Fact]
    public void AddMetadata_NullKey_ShouldNotAdd()
    {
        var retry = new RetryMetadataBuilder().AddMetadata(null!, "value").Build();
        Assert.Empty(retry.AdditionalMetadata);
    }

    // ---------------------------------------------------------------------------
    // AddMetadata (dictionary)
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddMetadata_Dictionary_ShouldAddAllEntries()
    {
        var dict = new Dictionary<string, string> { { "a", "1" }, { "b", "2" } };
        var retry = new RetryMetadataBuilder().AddMetadata(dict).Build();

        Assert.Equal(2, retry.AdditionalMetadata.Count);
        Assert.Equal("1", retry.AdditionalMetadata["a"]);
        Assert.Equal("2", retry.AdditionalMetadata["b"]);
    }

    [Fact]
    public void AddMetadata_NullDictionary_ShouldThrowArgumentNullException()
    {
        var builder = new RetryMetadataBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddMetadata((IDictionary<string, string>)null!));
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreDefaults()
    {
        var builder = new RetryMetadataBuilder()
            .WithAttemptNumber(3)
            .WithMaxRetries(5)
            .WithPassedOnRetry(true)
            .WithRetryReason("NetworkError")
            .WithRetryAttributeName("RetryFact")
            .AddMetadata("k", "v");

        builder.Reset();
        var retry = builder.Build();

        Assert.Equal(1, retry.AttemptNumber);
        Assert.Equal(0, retry.MaxRetries);
        Assert.False(retry.PassedOnRetry);
        Assert.Null(retry.RetryReason);
        Assert.Equal(string.Empty, retry.RetryAttributeName);
        Assert.Empty(retry.AdditionalMetadata);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new RetryMetadataBuilder();
        Assert.Same(builder, builder.Reset());
    }

    // ---------------------------------------------------------------------------
    // Fluent chain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldSupportFullFluentChain()
    {
        var retry = new RetryMetadataBuilder()
            .WithAttemptNumber(2)
            .WithMaxRetries(3)
            .WithPassedOnRetry(true)
            .WithDelayBetweenRetries(TimeSpan.FromMilliseconds(500))
            .WithRetryReason("Flaky")
            .WithRetryAttributeName("RetryAttribute")
            .AddMetadata("framework", "xunit")
            .Build();

        Assert.Equal(2, retry.AttemptNumber);
        Assert.Equal(3, retry.MaxRetries);
        Assert.True(retry.PassedOnRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(500), retry.DelayBetweenRetries);
        Assert.Equal("Flaky", retry.RetryReason);
        Assert.Equal("RetryAttribute", retry.RetryAttributeName);
        Assert.Equal("xunit", retry.AdditionalMetadata["framework"]);
    }
}
