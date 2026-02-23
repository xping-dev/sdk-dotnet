/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Tests.Serialization;

public sealed class XpingJsonDeserializerTests
{
    private const string ResourceName =
        "Xping.Sdk.Core.Tests.Serialization.TestData.afbba725-d460-4b9f-88c6-a39c040e85aa.json";

    private static IXpingSerializer BuildSerializer()
    {
        var services = new ServiceCollection();
        services.AddXpingSerialization();
        return services.BuildServiceProvider().GetRequiredService<IXpingSerializer>();
    }

    private static Stream OpenResourceStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException(
                   $"Embedded resource '{ResourceName}' not found.");
    }

    // ---------------------------------------------------------------------------
    // Session-level fields
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_SessionId_ShouldMatchExpectedValue()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal(Guid.Parse("dc6cdcdc-8567-439b-8a1d-d86fad27d05e"), session!.SessionId);
    }

    [Fact]
    public async Task Deserialize_StartedAt_ShouldMatchExpectedUtcTimestamp()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var expected = DateTime.Parse("2026-02-18T09:23:51.513715Z", null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(expected, session!.StartedAt);
    }

    [Fact]
    public async Task Deserialize_EndedAt_ShouldMatchExpectedUtcTimestamp()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.NotNull(session!.EndedAt);
        var expected = DateTime.Parse("2026-02-18T09:23:51.991022Z", null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(expected, session.EndedAt);
    }

    // ---------------------------------------------------------------------------
    // EnvironmentInfo fields
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_EnvironmentInfo_ShouldPopulateMachineName()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal("MacBook-Pro", session!.EnvironmentInfo.MachineName);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_ShouldPopulateOperatingSystem()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Contains("macOS", session!.EnvironmentInfo.OperatingSystem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_ShouldPopulateRuntimeVersion()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal(".NET 9.0.4", session!.EnvironmentInfo.RuntimeVersion);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_ShouldPopulateFramework()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal(".NET", session!.EnvironmentInfo.Framework);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_ShouldPopulateEnvironmentName()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal("Local", session!.EnvironmentInfo.EnvironmentName);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_IsCIEnvironment_ShouldBeFalse()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.False(session!.EnvironmentInfo.IsCIEnvironment);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_NetworkMetrics_ShouldBePopulated()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var networkMetrics = session!.EnvironmentInfo.NetworkMetrics;
        Assert.NotNull(networkMetrics);
        Assert.Equal(0, networkMetrics!.LatencyMs);
        Assert.True(networkMetrics.IsOnline);
        Assert.Equal("WiFi", networkMetrics.ConnectionType);
        Assert.Equal(0, networkMetrics.PacketLossPercent);
    }

    [Fact]
    public async Task Deserialize_EnvironmentInfo_CustomProperties_ShouldBePopulated()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var props = session!.EnvironmentInfo.CustomProperties;
        Assert.Equal("macOS", props["Platform"]);
        Assert.Equal("Arm64", props["ProcessorArchitecture"]);
        Assert.Equal("10", props["ProcessorCount"]);
    }

    // ---------------------------------------------------------------------------
    // Executions count and outcomes
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_Executions_ShouldContainExpectedCount()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        Assert.Equal(12, session!.Executions.Count);
    }

    [Fact]
    public async Task Deserialize_Executions_ShouldContainOneFailedTest()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var failed = session!.Executions.Where(e => e.Outcome == TestOutcome.Failed).ToList();
        Assert.Single(failed);
    }

    [Fact]
    public async Task Deserialize_Executions_ShouldContainOneSkippedTest()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var skipped = session!.Executions.Where(e => e.Outcome == TestOutcome.Skipped).ToList();
        Assert.Single(skipped);
    }

    // ---------------------------------------------------------------------------
    // Individual execution fields
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_FirstExecution_ShouldHaveCorrectExecutionId()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var first = session!.Executions.First();
        Assert.Equal(Guid.Parse("408c62a6-43df-4a1a-8164-158de695f660"), first.ExecutionId);
    }

    [Fact]
    public async Task Deserialize_FirstExecution_ShouldHaveCorrectTestName()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var first = session!.Executions.First();
        Assert.Equal("SampleApp.XUnit.CollectionTests.TestInCollection", first.TestName);
    }

    [Fact]
    public async Task Deserialize_FirstExecution_OutcomeShouldBePassed()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var first = session!.Executions.First();
        Assert.Equal(TestOutcome.Passed, first.Outcome);
    }

    [Fact]
    public async Task Deserialize_FailedExecution_ShouldHaveExceptionDetails()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var failed = session!.Executions.Single(e => e.Outcome == TestOutcome.Failed);
        Assert.Equal("System.InvalidOperationException", failed.ExceptionType);
        Assert.Equal("This is a test exception for tracking purposes.", failed.ErrorMessage);
        Assert.NotNull(failed.StackTrace);
        Assert.NotNull(failed.ErrorMessageHash);
        Assert.NotNull(failed.StackTraceHash);
    }

    [Fact]
    public async Task Deserialize_SkippedExecution_ShouldHaveSkipMessage()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var skipped = session!.Executions.Single(e => e.Outcome == TestOutcome.Skipped);
        Assert.Equal("SampleApp.XUnit.SampleTests.SkippedTestIsTracked", skipped.TestName);
        Assert.Equal("Test skipped: Demonstrating skipped test tracking", skipped.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // TestIdentity fields
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_FirstExecution_Identity_ShouldBePopulated()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var identity = session!.Executions.First().Identity;
        Assert.Equal("SampleApp.XUnit", identity.Namespace);
        Assert.Equal("CollectionTests", identity.ClassName);
        Assert.Equal("TestInCollection", identity.MethodName);
        Assert.Equal("SampleApp.XUnit.CollectionTests.TestInCollection", identity.FullyQualifiedName);
    }

    [Fact]
    public async Task Deserialize_ParameterizedExecution_Identity_ShouldIncludeParameterHash()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var parameterized = session!.Executions
            .First(e => e.Identity.MethodName == "ParameterizedTestIsTracked"
                        && e.Identity.ParameterHash == "5,5,10");
        Assert.NotNull(parameterized.Identity.ParameterHash);
        Assert.Equal("5,5,10", parameterized.Identity.ParameterHash);
    }

    // ---------------------------------------------------------------------------
    // TestOrchestrationRecord fields
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Deserialize_FirstExecution_OrchestrationRecord_ShouldHaveGlobalPositionOne()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var first = session!.Executions.First();
        Assert.Equal(1, first.TestOrchestrationRecord.GlobalPosition);
        Assert.Equal(1, first.TestOrchestrationRecord.PositionInSuite);
    }

    [Fact]
    public async Task Deserialize_SecondExecution_OrchestrationRecord_WasParallelized_ShouldBeTrue()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var second = session!.Executions.Skip(1).First();
        Assert.True(second.TestOrchestrationRecord.WasParallelized);
        Assert.Equal(2, second.TestOrchestrationRecord.ConcurrentTestCount);
    }

    [Fact]
    public async Task Deserialize_ThirdExecution_OrchestrationRecord_ShouldHavePreviousTestInfo()
    {
        var serializer = BuildSerializer();
        using var stream = OpenResourceStream();

        var session = await serializer.DeserializeAsync<TestSession>(stream);

        Assert.NotNull(session);
        var third = session!.Executions.Skip(2).First();
        Assert.NotNull(third.TestOrchestrationRecord.PreviousTestId);
        Assert.NotNull(third.TestOrchestrationRecord.PreviousTestName);
        Assert.Equal(TestOutcome.Passed, third.TestOrchestrationRecord.PreviousTestOutcome);
    }
}
