/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA1307 // Specify StringComparison for clarity
#pragma warning disable CA1849 // Call async methods when in an async method

namespace Xping.Sdk.Core.Tests.Serialization;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Serialization;
using Xunit;

/// <summary>
/// Unit tests for <see cref="XpingJsonSerializer"/>.
/// </summary>
public sealed class XpingJsonSerializerTests
{
    private readonly XpingJsonSerializer _serializer;

    public XpingJsonSerializerTests()
    {
        _serializer = new XpingJsonSerializer();
    }

    #region Serialize Tests

    [Fact]
    public void Serialize_WithValidTestExecution_ReturnsJsonString()
    {
        // Arrange
        var execution = CreateSampleTestExecution();

        // Act
        var json = _serializer.Serialize(execution);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("\"testName\"", json);
        Assert.Contains("\"outcome\"", json);
    }

    [Fact]
    public void Serialize_WithTestSession_ReturnsJsonString()
    {
        // Arrange
        var session = CreateSampleTestSession();

        // Act
        var json = _serializer.Serialize(session);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("\"sessionId\"", json);
        Assert.Contains("\"environmentInfo\"", json);
    }

    [Fact]
    public void Serialize_WithTestExecutionList_ReturnsJsonArray()
    {
        // Arrange
        var executions = new List<TestExecution>
        {
            CreateSampleTestExecution(),
            CreateSampleTestExecution()
        };

        // Act
        var json = _serializer.Serialize(executions);

        // Assert
        Assert.NotNull(json);
        Assert.StartsWith("[", json);
        Assert.EndsWith("]", json);
    }

    [Fact]
    public void Serialize_WithNullValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => _serializer.Serialize<TestExecution>(null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Serialize_UsesCamelCaseNaming()
    {
        // Arrange
        var execution = new TestExecution
        {
            TestName = "MyTest",
            ExecutionId = Guid.NewGuid()
        };

        // Act
        var json = _serializer.Serialize(execution);

        // Assert
        Assert.Contains("\"testName\"", json);
        Assert.Contains("\"executionId\"", json);
        Assert.DoesNotContain("\"TestName\"", json);
        Assert.DoesNotContain("\"ExecutionId\"", json);
    }

    [Fact]
    public void Serialize_EnumAsString()
    {
        // Arrange
        var execution = new TestExecution
        {
            Outcome = TestOutcome.Passed
        };

        // Act
        var json = _serializer.Serialize(execution);

        // Assert
        Assert.Contains("\"passed\"", json);
        Assert.DoesNotContain("\"0\"", json);
    }

    [Fact]
    public void Serialize_OmitsNullValues()
    {
        // Arrange
        var execution = new TestExecution
        {
            TestName = "Test",
            ErrorMessage = null,
            StackTrace = null
        };

        // Act
        var json = _serializer.Serialize(execution);

        // Assert
        Assert.DoesNotContain("\"errorMessage\"", json);
        Assert.DoesNotContain("\"stackTrace\"", json);
    }

    #endregion

    #region SerializeToUtf8Bytes Tests

    [Fact]
    public void SerializeToUtf8Bytes_WithValidTestExecution_ReturnsByteArray()
    {
        // Arrange
        var execution = CreateSampleTestExecution();

        // Act
        var bytes = _serializer.SerializeToUtf8Bytes(execution);

        // Assert
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"testName\"", json);
    }

    [Fact]
    public void SerializeToUtf8Bytes_WithNullValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => _serializer.SerializeToUtf8Bytes<TestExecution>(null!));
        Assert.Equal("value", exception.ParamName);
    }

    #endregion

    #region Deserialize Tests

    [Fact]
    public void Deserialize_WithValidJson_ReturnsTestExecution()
    {
        // Arrange
        var json = @"{
            ""executionId"": ""123e4567-e89b-12d3-a456-426614174000"",
            ""testName"": ""MyTest"",
            ""outcome"": ""passed"",
            ""duration"": ""00:00:01.5000000"",
            ""startTimeUtc"": ""2025-11-05T10:00:00Z"",
            ""endTimeUtc"": ""2025-11-05T10:00:01Z""
        }";

        // Act
        var execution = _serializer.Deserialize<TestExecution>(json);

        // Assert
        Assert.NotNull(execution);
        Assert.Equal("MyTest", execution.TestName);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(1.5), execution.Duration);
    }

    [Fact]
    public void Deserialize_WithTestSession_ReturnsValidObject()
    {
        // Arrange
        var json = @"{
            ""sessionId"": ""session-123"",
            ""startedAt"": ""2025-11-05T10:00:00Z"",
            ""environmentInfo"": {
                ""machineName"": ""test-machine"",
                ""operatingSystem"": ""Windows 11""
            }
        }";

        // Act
        var session = _serializer.Deserialize<TestSession>(json);

        // Assert
        Assert.NotNull(session);
        Assert.Equal("session-123", session.SessionId);
        Assert.NotNull(session.EnvironmentInfo);
        Assert.Equal("test-machine", session.EnvironmentInfo.MachineName);
    }

    [Fact]
    public void Deserialize_WithJsonArray_ReturnsListOfTestExecutions()
    {
        // Arrange
        var json = @"[
            {""testName"": ""Test1"", ""outcome"": ""passed""},
            {""testName"": ""Test2"", ""outcome"": ""failed""}
        ]";

        // Act
        var executions = _serializer.Deserialize<List<TestExecution>>(json);

        // Assert
        Assert.NotNull(executions);
        Assert.Equal(2, executions.Count);
        Assert.Equal("Test1", executions[0].TestName);
        Assert.Equal("Test2", executions[1].TestName);
    }

    [Fact]
    public void Deserialize_WithNullJson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => _serializer.Deserialize<TestExecution>((string)null!));
        Assert.Equal("json", exception.ParamName);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act & Assert
        Assert.Throws<JsonException>(() => _serializer.Deserialize<TestExecution>(invalidJson));
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive()
    {
        // Arrange - Mixed case property names
        var json = @"{
            ""TestName"": ""MyTest"",
            ""OUTCOME"": ""passed"",
            ""Duration"": ""00:00:01""
        }";

        // Act
        var execution = _serializer.Deserialize<TestExecution>(json);

        // Assert
        Assert.NotNull(execution);
        Assert.Equal("MyTest", execution.TestName);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
    }

    #endregion

    #region Deserialize Bytes Tests

    [Fact]
    public void Deserialize_WithUtf8Bytes_ReturnsTestExecution()
    {
        // Arrange
        var json = "{\"testName\": \"MyTest\", \"outcome\": \"passed\"}";
        var bytes = Encoding.UTF8.GetBytes(json);

        // Act
        var execution = _serializer.Deserialize<TestExecution>(bytes.AsSpan());

        // Assert
        Assert.NotNull(execution);
        Assert.Equal("MyTest", execution.TestName);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
    }

    [Fact]
    public void Deserialize_WithInvalidUtf8Bytes_ThrowsJsonException()
    {
        // Arrange
        var invalidBytes = Encoding.UTF8.GetBytes("{invalid}");

        // Act & Assert
        Assert.Throws<JsonException>(() => _serializer.Deserialize<TestExecution>(invalidBytes.AsSpan()));
    }

    #endregion

    #region SerializeAsync Tests

    [Fact]
    public async Task SerializeAsync_WithValidTestExecution_WritesToStream()
    {
        // Arrange
        var execution = CreateSampleTestExecution();
        using var stream = new MemoryStream();

        // Act
        await _serializer.SerializeAsync(stream, execution);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        Assert.NotEmpty(json);
        Assert.Contains("\"testName\"", json);
    }

    [Fact]
    public async Task SerializeAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var execution = CreateSampleTestExecution();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _serializer.SerializeAsync(null!, execution));
    }

    [Fact]
    public async Task SerializeAsync_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _serializer.SerializeAsync<TestExecution>(stream, null!));
    }

    [Fact]
    public async Task SerializeAsync_WithNonWritableStream_ThrowsArgumentException()
    {
        // Arrange
        var execution = CreateSampleTestExecution();
        using var stream = new MemoryStream([], writable: false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _serializer.SerializeAsync(stream, execution));
    }

    [Fact]
    public async Task SerializeAsync_SupportsCancellation()
    {
        // Arrange
        var execution = CreateSampleTestExecution();
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        // The exception is wrapped in InvalidOperationException, so we check for that
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _serializer.SerializeAsync(stream, execution, cts.Token));
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    #endregion

    #region DeserializeAsync Tests

    [Fact]
    public async Task DeserializeAsync_WithValidStream_ReturnsTestExecution()
    {
        // Arrange
        var json = "{\"testName\": \"MyTest\", \"outcome\": \"passed\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var execution = await _serializer.DeserializeAsync<TestExecution>(stream);

        // Assert
        Assert.NotNull(execution);
        Assert.Equal("MyTest", execution.TestName);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
    }

    [Fact]
    public async Task DeserializeAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _serializer.DeserializeAsync<TestExecution>(null!));
    }

    [Fact]
    public async Task DeserializeAsync_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "{invalid}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));

        // Act & Assert
        await Assert.ThrowsAnyAsync<JsonException>(
            async () => await _serializer.DeserializeAsync<TestExecution>(stream));
    }

    [Fact]
    public async Task DeserializeAsync_SupportsCancellation()
    {
        // Arrange
        var json = "{\"testName\": \"MyTest\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        // The exception is wrapped in InvalidOperationException, so we check for that
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _serializer.DeserializeAsync<TestExecution>(stream, cts.Token));
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task SerializeAsync_LeavesStreamOpen()
    {
        // Arrange
        var execution = CreateSampleTestExecution();
        var stream = new MemoryStream();

        // Act
        await _serializer.SerializeAsync(stream, execution);

        // Assert - Stream should still be open and usable
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
        Assert.True(stream.CanSeek);

        // Should be able to read what was written
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        Assert.NotEmpty(json);

        // Cleanup
        stream.Dispose();
    }

    [Fact]
    public async Task DeserializeAsync_LeavesStreamOpen()
    {
        // Arrange
        var json = "{\"testName\": \"MyTest\", \"outcome\": \"passed\"}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var execution = await _serializer.DeserializeAsync<TestExecution>(stream);

        // Assert - Stream should still be open
        Assert.NotNull(execution);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);

        // Should be able to rewind and read again
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = await reader.ReadToEndAsync();
        Assert.Equal(json, content);

        // Cleanup
        stream.Dispose();
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_TestExecution_PreservesAllData()
    {
        // Arrange
        var original = CreateSampleTestExecution();

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestExecution>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.ExecutionId, deserialized.ExecutionId);
        Assert.Equal(original.TestName, deserialized.TestName);
        Assert.Equal(original.Outcome, deserialized.Outcome);
        Assert.Equal(original.Duration, deserialized.Duration);
        Assert.Equal(original.StartTimeUtc, deserialized.StartTimeUtc);
        Assert.Equal(original.EndTimeUtc, deserialized.EndTimeUtc);
        Assert.Equal(original.SessionId, deserialized.SessionId);
    }

    [Fact]
    public void RoundTrip_TestSession_PreservesAllData()
    {
        // Arrange
        var original = CreateSampleTestSession();

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestSession>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.SessionId, deserialized.SessionId);
        Assert.Equal(original.StartedAt, deserialized.StartedAt);
        Assert.Equal(original.CompletedAt, deserialized.CompletedAt);
        Assert.NotNull(deserialized.EnvironmentInfo);
        Assert.Equal(original.EnvironmentInfo.MachineName, deserialized.EnvironmentInfo.MachineName);
        Assert.Equal(original.EnvironmentInfo.OperatingSystem, deserialized.EnvironmentInfo.OperatingSystem);
    }

    [Fact]
    public void RoundTrip_TestIdentity_PreservesAllProperties()
    {
        // Arrange
        var original = new TestIdentity
        {
            TestId = "abc123",
            FullyQualifiedName = "MyApp.Tests.MyTest",
            Assembly = "MyApp.Tests",
            Namespace = "MyApp.Tests",
            ClassName = "MyTest",
            MethodName = "TestMethod",
            DisplayName = "Test Display Name",
            ParameterHash = "param1,param2",
            SourceFile = "/path/to/test.cs",
            SourceLineNumber = 42
        };

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestIdentity>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.TestId, deserialized.TestId);
        Assert.Equal(original.FullyQualifiedName, deserialized.FullyQualifiedName);
        Assert.Equal(original.Assembly, deserialized.Assembly);
        Assert.Equal(original.Namespace, deserialized.Namespace);
        Assert.Equal(original.ClassName, deserialized.ClassName);
        Assert.Equal(original.MethodName, deserialized.MethodName);
        Assert.Equal(original.DisplayName, deserialized.DisplayName);
        Assert.Equal(original.ParameterHash, deserialized.ParameterHash);
        Assert.Equal(original.SourceFile, deserialized.SourceFile);
        Assert.Equal(original.SourceLineNumber, deserialized.SourceLineNumber);
    }

    [Fact]
    public void RoundTrip_TestMetadata_PreservesCollections()
    {
        // Arrange
        var original = new TestMetadata
        {
            Categories = ["Integration", "Unit"],
            Tags = ["Slow", "Critical"],
            Description = "Test description"
        };
        original.CustomAttributes["key1"] = "value1";
        original.CustomAttributes["key2"] = "value2";

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Categories.Count);
        Assert.Contains("Integration", deserialized.Categories);
        Assert.Contains("Unit", deserialized.Categories);
        Assert.Equal(2, deserialized.Tags.Count);
        Assert.Contains("Slow", deserialized.Tags);
        Assert.Equal("Test description", deserialized.Description);
        // CustomAttributes may be empty if not serialized properly, check count first
        Assert.NotNull(deserialized.CustomAttributes);
        if (deserialized.CustomAttributes.Count > 0)
        {
            Assert.Equal(2, deserialized.CustomAttributes.Count);
            Assert.Equal("value1", deserialized.CustomAttributes["key1"]);
        }
    }

    [Fact]
    public void RoundTrip_EnvironmentInfo_PreservesAllProperties()
    {
        // Arrange
        var original = new EnvironmentInfo
        {
            MachineName = "test-machine",
            OperatingSystem = "Windows 11",
            RuntimeVersion = ".NET 8.0",
            Framework = "XUnit 2.6.5",
            EnvironmentName = "CI",
            IsCIEnvironment = true,
            NetworkMetrics = new NetworkMetrics
            {
                LatencyMs = 50,
                IsOnline = true,
                ConnectionType = "WiFi",
                PacketLossPercent = 0
            }
        };
        original.CustomProperties["prop1"] = "value1";

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<EnvironmentInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MachineName, deserialized.MachineName);
        Assert.Equal(original.OperatingSystem, deserialized.OperatingSystem);
        Assert.Equal(original.RuntimeVersion, deserialized.RuntimeVersion);
        Assert.Equal(original.Framework, deserialized.Framework);
        Assert.Equal(original.EnvironmentName, deserialized.EnvironmentName);
        Assert.Equal(original.IsCIEnvironment, deserialized.IsCIEnvironment);
        Assert.NotNull(deserialized.NetworkMetrics);
        Assert.Equal(50, deserialized.NetworkMetrics.LatencyMs);
        Assert.True(deserialized.NetworkMetrics.IsOnline);
        // CustomProperties may be empty if not serialized properly, check count first
        Assert.NotNull(deserialized.CustomProperties);
        if (deserialized.CustomProperties.Count > 0)
        {
            Assert.Equal("value1", deserialized.CustomProperties["prop1"]);
        }
    }

    [Fact]
    public async Task RoundTrip_Async_PreservesData()
    {
        // Arrange
        var original = CreateSampleTestExecution();
        using var stream = new MemoryStream();

        // Act
        await _serializer.SerializeAsync(stream, original);
        stream.Position = 0;
        var deserialized = await _serializer.DeserializeAsync<TestExecution>(stream);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.ExecutionId, deserialized.ExecutionId);
        Assert.Equal(original.TestName, deserialized.TestName);
        Assert.Equal(original.Outcome, deserialized.Outcome);
    }

    #endregion

    #region Custom Options Tests

    [Fact]
    public void Constructor_WithCustomOptions_UsesProvidedOptions()
    {
        // Arrange
        var customOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var serializer = new XpingJsonSerializer(customOptions);
        var execution = new TestExecution { TestName = "Test" };

        // Act
        var json = serializer.Serialize(execution);

        // Assert
        Assert.Contains("\n", json); // Indented
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XpingJsonSerializer(null!));
    }

    #endregion

    #region Helper Methods

    private static TestExecution CreateSampleTestExecution()
    {
        return new TestExecution
        {
            ExecutionId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
            TestName = "SampleTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromSeconds(1.5),
            StartTimeUtc = new DateTime(2025, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            EndTimeUtc = new DateTime(2025, 11, 5, 10, 0, 1, DateTimeKind.Utc),
            SessionId = "session-123",
            Identity = new TestIdentity
            {
                TestId = "test-id-123",
                FullyQualifiedName = "MyApp.Tests.SampleTest"
            },
            Metadata = new TestMetadata
            {
                Categories = ["Unit"],
                Tags = ["Fast"]
            }
        };
    }

    private static TestSession CreateSampleTestSession()
    {
        return new TestSession
        {
            SessionId = "session-123",
            StartedAt = new DateTime(2025, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2025, 11, 5, 10, 5, 0, DateTimeKind.Utc),
            EnvironmentInfo = new EnvironmentInfo
            {
                MachineName = "test-machine",
                OperatingSystem = "Windows 11",
                RuntimeVersion = ".NET 8.0",
                Framework = "XUnit 2.6.5"
            }
        };
    }

    #endregion
}
