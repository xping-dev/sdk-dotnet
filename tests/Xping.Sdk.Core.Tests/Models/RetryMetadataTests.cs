/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Models;

using System;
using System.Text.Json;
using Xping.Sdk.Core.Models;

public sealed class RetryMetadataTests
{
    [Fact]
    public void Constructor_Should_Initialize_With_Default_Values()
    {
        // Act
        var metadata = new RetryMetadata();

        // Assert
        Assert.Equal(1, metadata.AttemptNumber);
        Assert.Equal(0, metadata.MaxRetries);
        Assert.False(metadata.PassedOnRetry);
        Assert.Equal(TimeSpan.Zero, metadata.DelayBetweenRetries);
        Assert.Null(metadata.RetryReason);
        Assert.Null(metadata.RetryAttributeName);
        Assert.NotNull(metadata.AdditionalMetadata);
        Assert.Empty(metadata.AdditionalMetadata);
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var attemptNumber = 3;
        var maxRetries = 5;
        var passedOnRetry = true;
        var delay = TimeSpan.FromMilliseconds(500);
        var reason = "NetworkError";
        var attributeName = "RetryFact";

        // Act
        var metadata = new RetryMetadata
        {
            AttemptNumber = attemptNumber,
            MaxRetries = maxRetries,
            PassedOnRetry = passedOnRetry,
            DelayBetweenRetries = delay,
            RetryReason = reason,
            RetryAttributeName = attributeName
        };

        // Assert
        Assert.Equal(attemptNumber, metadata.AttemptNumber);
        Assert.Equal(maxRetries, metadata.MaxRetries);
        Assert.Equal(passedOnRetry, metadata.PassedOnRetry);
        Assert.Equal(delay, metadata.DelayBetweenRetries);
        Assert.Equal(reason, metadata.RetryReason);
        Assert.Equal(attributeName, metadata.RetryAttributeName);
    }

    [Fact]
    public void Should_Allow_Adding_Additional_Metadata()
    {
        // Arrange
        var metadata = new RetryMetadata();

        // Act
        metadata.AdditionalMetadata["ExceptionFilter"] = "TimeoutException";
        metadata.AdditionalMetadata["CustomProperty"] = "CustomValue";

        // Assert
        Assert.Equal(2, metadata.AdditionalMetadata.Count);
        Assert.Equal("TimeoutException", metadata.AdditionalMetadata["ExceptionFilter"]);
        Assert.Equal("CustomValue", metadata.AdditionalMetadata["CustomProperty"]);
    }

    [Fact]
    public void Should_Serialize_To_Json_Successfully()
    {
        // Arrange
        var metadata = new RetryMetadata
        {
            AttemptNumber = 2,
            MaxRetries = 3,
            PassedOnRetry = true,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(100),
            RetryReason = "Timeout",
            RetryAttributeName = "RetryFact"
        };
        metadata.AdditionalMetadata["TestKey"] = "TestValue";

        // Act
        var json = JsonSerializer.Serialize(metadata);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"AttemptNumber\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"MaxRetries\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"PassedOnRetry\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"Timeout\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RetryFact\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TestKey\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TestValue\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_Deserialize_From_Json_Successfully()
    {
        // Arrange
        var json = @"{
            ""AttemptNumber"": 2,
            ""MaxRetries"": 3,
            ""PassedOnRetry"": true,
            ""DelayBetweenRetries"": ""00:00:00.100"",
            ""RetryReason"": ""Timeout"",
            ""RetryAttributeName"": ""RetryFact""
        }";

        // Act
        var metadata = JsonSerializer.Deserialize<RetryMetadata>(json);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(2, metadata.AttemptNumber);
        Assert.Equal(3, metadata.MaxRetries);
        Assert.True(metadata.PassedOnRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(100), metadata.DelayBetweenRetries);
        Assert.Equal("Timeout", metadata.RetryReason);
        Assert.Equal("RetryFact", metadata.RetryAttributeName);
        Assert.NotNull(metadata.AdditionalMetadata);
    }

    [Fact]
    public void Should_Handle_Null_Optional_Properties_In_Serialization()
    {
        // Arrange
        var metadata = new RetryMetadata
        {
            AttemptNumber = 1,
            MaxRetries = 2
        };

        // Act
        var json = JsonSerializer.Serialize(metadata);
        var deserialized = JsonSerializer.Deserialize<RetryMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.RetryReason);
        Assert.Null(deserialized.RetryAttributeName);
        Assert.NotNull(deserialized.AdditionalMetadata);
    }

    [Fact]
    public void Should_Deserialize_With_Missing_Optional_Properties()
    {
        // Arrange - minimal JSON with only required fields
        var json = @"{
            ""AttemptNumber"": 1,
            ""MaxRetries"": 0
        }";

        // Act
        var metadata = JsonSerializer.Deserialize<RetryMetadata>(json);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(1, metadata.AttemptNumber);
        Assert.Equal(0, metadata.MaxRetries);
        Assert.False(metadata.PassedOnRetry);
        Assert.Equal(TimeSpan.Zero, metadata.DelayBetweenRetries);
        Assert.Null(metadata.RetryReason);
        Assert.Null(metadata.RetryAttributeName);
    }

    [Fact]
    public void Should_Handle_First_Attempt_Scenario()
    {
        // Arrange - first attempt (no retry)
        var metadata = new RetryMetadata
        {
            AttemptNumber = 1,
            MaxRetries = 3,
            PassedOnRetry = false
        };

        // Assert
        Assert.Equal(1, metadata.AttemptNumber);
        Assert.False(metadata.PassedOnRetry);
    }

    [Fact]
    public void Should_Handle_Passed_On_Retry_Scenario()
    {
        // Arrange - passed on second attempt (first retry)
        var metadata = new RetryMetadata
        {
            AttemptNumber = 2,
            MaxRetries = 3,
            PassedOnRetry = true
        };

        // Assert
        Assert.Equal(2, metadata.AttemptNumber);
        Assert.True(metadata.PassedOnRetry);
    }

    [Fact]
    public void Should_Handle_Multiple_Retries_Scenario()
    {
        // Arrange - passed on third attempt (second retry)
        var metadata = new RetryMetadata
        {
            AttemptNumber = 3,
            MaxRetries = 5,
            PassedOnRetry = true
        };

        // Assert
        Assert.Equal(3, metadata.AttemptNumber);
        Assert.True(metadata.PassedOnRetry);
        Assert.True(metadata.AttemptNumber <= metadata.MaxRetries + 1); // attempt <= maxRetries + initial attempt
    }

    [Fact]
    public void Should_Handle_Different_Time_Delays()
    {
        // Arrange
        var delays = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5)
        };

        foreach (var delay in delays)
        {
            // Act
            var metadata = new RetryMetadata
            {
                DelayBetweenRetries = delay
            };

            // Assert
            Assert.Equal(delay, metadata.DelayBetweenRetries);
        }
    }

    [Fact]
    public void Should_Support_Common_Retry_Reasons()
    {
        // Arrange
        var reasons = new[]
        {
            "Timeout",
            "NetworkError",
            "DatabaseContention",
            "RaceCondition",
            "TransientFailure"
        };

        foreach (var reason in reasons)
        {
            // Act
            var metadata = new RetryMetadata
            {
                RetryReason = reason
            };

            // Assert
            Assert.Equal(reason, metadata.RetryReason);
        }
    }

    [Fact]
    public void Should_Support_Common_Retry_Attribute_Names()
    {
        // Arrange
        var attributeNames = new[]
        {
            "RetryFact",
            "RetryTheory",
            "Retry",
            "TestRetry",
            "CustomRetry"
        };

        foreach (var name in attributeNames)
        {
            // Act
            var metadata = new RetryMetadata
            {
                RetryAttributeName = name
            };

            // Assert
            Assert.Equal(name, metadata.RetryAttributeName);
        }
    }

    [Fact]
    public void AdditionalMetadata_Should_Be_Mutable()
    {
        // Arrange
        var metadata = new RetryMetadata();

        // Act
        metadata.AdditionalMetadata.Add("Key1", "Value1");
        metadata.AdditionalMetadata["Key2"] = "Value2";
        metadata.AdditionalMetadata["Key1"] = "UpdatedValue1";

        // Assert
        Assert.Equal(2, metadata.AdditionalMetadata.Count);
        Assert.Equal("UpdatedValue1", metadata.AdditionalMetadata["Key1"]);
        Assert.Equal("Value2", metadata.AdditionalMetadata["Key2"]);
    }

    [Fact]
    public void Should_Roundtrip_Through_Json_Serialization()
    {
        // Arrange
        var original = new RetryMetadata
        {
            AttemptNumber = 4,
            MaxRetries = 5,
            PassedOnRetry = true,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(250),
            RetryReason = "NetworkError",
            RetryAttributeName = "RetryTheory"
        };
        original.AdditionalMetadata["Filter"] = "TimeoutException";
        original.AdditionalMetadata["Custom"] = "Data";

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<RetryMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.AttemptNumber, deserialized.AttemptNumber);
        Assert.Equal(original.MaxRetries, deserialized.MaxRetries);
        Assert.Equal(original.PassedOnRetry, deserialized.PassedOnRetry);
        Assert.Equal(original.DelayBetweenRetries, deserialized.DelayBetweenRetries);
        Assert.Equal(original.RetryReason, deserialized.RetryReason);
        Assert.Equal(original.RetryAttributeName, deserialized.RetryAttributeName);
        // Note: AdditionalMetadata won't deserialize into read-only property, 
        // but will serialize successfully. This is consistent with TestMetadata.CustomAttributes behavior.
        Assert.NotNull(deserialized.AdditionalMetadata);
    }
}
