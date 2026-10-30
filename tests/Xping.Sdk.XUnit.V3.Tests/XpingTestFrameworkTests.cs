/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Json;
using Xping.Sdk.Core.Services.LocalStore;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Runs the probe assembly through a real xUnit v3 pipeline with <see cref="XpingTestFramework"/>, and
/// checks what the session it stored says about each test.
/// </summary>
[Collection(XpingContextCollection.Name)]
public sealed class XpingTestFrameworkTests(WholeProbeRun probe) : IClassFixture<WholeProbeRun>
{
    private readonly ProbeRun _run = probe.Run;

    [Theory]
    [InlineData("OutcomeTests.Passes", "passed")]
    [InlineData("OutcomeTests.FailsAnAssertion", "failed")]
    [InlineData("OutcomeTests.Throws", "failed")]
    [InlineData("OutcomeTests.TimesOut", "timeout")]
    [InlineData("OutcomeTests.SkippedStatically", "skipped")]
    [InlineData("OutcomeTests.SkippedWhileRunning", "skipped")]
    [InlineData("ClassFixtureTests.NeedsTheFixture", "failed")]
    [InlineData("CollectionFixtureTests.NeedsTheCollection", "failed")]
    public void EveryTestThatRanIsRecordedWithItsOutcome(string test, string outcome)
    {
        Assert.Equal(outcome, _run.Execution(test).GetProperty("outcome").GetString());
    }

    [Fact]
    public void AnExplicitTestTheRunLeftOutIsNotRecorded()
    {
        Assert.DoesNotContain(_run.Executions.Keys, name => name.Contains("RunsOnlyWhenAskedFor", StringComparison.Ordinal));
    }

    [Fact]
    public void AFailureKeepsItsExceptionMessageAndTrace()
    {
        JsonElement thrown = _run.Execution("OutcomeTests.Throws");

        Assert.Equal(typeof(InvalidOperationException).FullName, thrown.GetProperty("exceptionType").GetString());
        Assert.Equal("thrown by the test body", thrown.GetProperty("errorMessage").GetString());
        Assert.Contains("OutcomeTests.Throws()", thrown.GetProperty("stackTrace").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// This run does not pre-enumerate theories, as xUnit's own console runner does not, and a case
    /// that was not pre-enumerated carries no arguments. Were the rows not told apart by the arguments
    /// in their display names, both would share one identity and their history would merge.
    /// </summary>
    [Fact]
    public void TheoryRowsAreRecordedSeparatelyWithTheirArguments()
    {
        JsonElement first = _run.Execution("OutcomeTests.Theory(value: 1)");
        JsonElement second = _run.Execution("OutcomeTests.Theory(value: 2)");

        Assert.NotEqual(Fingerprint(first), Fingerprint(second));
        Assert.Equal("value: 1", first.GetProperty("metadata").GetProperty("customAttributes").GetProperty("Arguments").GetString());
        Assert.Contains("type:theory", Tags(first));
    }

    [Fact]
    public void TraitsBecomeCategoriesADescriptionAndTags()
    {
        JsonElement metadata = _run.Execution("OutcomeTests.HasTraits").GetProperty("metadata");

        Assert.Equal(["Fast"], metadata.GetProperty("categories").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("described by a trait", metadata.GetProperty("description").GetString());
        Assert.Contains("Owner:checkout", Tags(_run.Execution("OutcomeTests.HasTraits")));
        Assert.Contains("type:fact", Tags(_run.Execution("OutcomeTests.HasTraits")));
    }

    [Fact]
    public void APinnedFingerprintIsUsed()
    {
        Assert.Equal("pinned-fingerprint", Fingerprint(_run.Execution("OutcomeTests.HasAPinnedFingerprint")));
    }

    [Fact]
    public void TheSourceLocationXunitRecordedIsKeptRelativeToTheRepository()
    {
        JsonElement identity = _run.Execution("OutcomeTests.Passes").GetProperty("identity");

        Assert.Equal("tests/Xping.Sdk.XUnit.V3.Probe/ProbeTests.cs", identity.GetProperty("sourceFile").GetString());
        Assert.True(identity.GetProperty("sourceLineNumber").GetInt32() > 0);
    }

    [Fact]
    public void TheOutputATestWroteIsKept()
    {
        JsonElement attributes = _run.Execution("OutcomeTests.Passes").GetProperty("metadata").GetProperty("customAttributes");

        Assert.Contains("written by the test", attributes.GetProperty("Output").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ATimeoutKeepsItsDeclaredBudgetAndHowLongItRan()
    {
        JsonElement timedOut = _run.Execution("OutcomeTests.TimesOut");

        Assert.Equal(TimeSpan.FromMilliseconds(100), Duration(timedOut, "timeoutBudget"));
        Assert.Equal("declared", timedOut.GetProperty("timeoutBudgetSource").GetString());
        Assert.True(Duration(timedOut, "duration") >= TimeSpan.FromMilliseconds(90));
    }

    [Theory]
    [InlineData("OutcomeTests.Throws", "testBody", null)]
    [InlineData("OutcomeTests.TimesOut", "unknown", null)]
    [InlineData("ClassFixtureTests.NeedsTheFixture", "fixtureSetup", "BrokenClassFixture..ctor")]
    [InlineData("CollectionFixtureTests.NeedsTheCollection", "fixtureSetup", "BrokenCollectionFixture..ctor")]
    [InlineData("ConstructorThrows.Test", "testSetup", "ConstructorThrows..ctor")]
    [InlineData("InitializeAsyncThrows.Test", "testSetup", "InitializeAsyncThrows.InitializeAsync")]
    [InlineData("DisposeThrows.Test", "testTeardown", "DisposeThrows.Dispose")]
    public void TheFailureSiteSaysWhereInTheLifecycleTheTestFailed(string test, string site, string? member)
    {
        JsonElement execution = _run.Execution(test);

        Assert.Equal(site, execution.GetProperty("site").GetString());
        Assert.Equal(member, execution.TryGetProperty("failureSiteMember", out JsonElement m) ? m.GetString() : null);
    }

    /// <summary>
    /// xRetry reports only the attempt it ends on. The attempt number comes from its announcement of
    /// each attempt, so a test that passed on its second is a pass on retry rather than a plain pass.
    /// </summary>
    [Fact]
    public void ARetriedTestIsRecordedAsTheAttemptItEndedOn()
    {
        JsonElement retried = _run.Execution("RetryTests.PassesOnItsSecondAttempt");

        Assert.Equal("passed", retried.GetProperty("outcome").GetString());

        JsonElement retry = retried.GetProperty("retry");
        Assert.Equal(2, retry.GetProperty("attemptNumber").GetInt32());
        Assert.True(retry.GetProperty("passedOnRetry").GetBoolean());
        Assert.Equal(3, retry.GetProperty("maxRetries").GetInt32());
        Assert.Equal("RetryFact", retry.GetProperty("retryAttributeName").GetString());
    }

    [Fact]
    public void TheSessionNamesXunitV3AsItsFramework()
    {
        JsonElement properties = _run.Session.GetProperty("environmentInfo").GetProperty("customProperties");

        Assert.Equal(LocalSessionProperties.XUnitV3Framework, properties.GetProperty(LocalSessionProperties.TestFramework).GetString());
    }

    [Fact]
    public void AnUnfilteredRunDiscoversAndSelectsEveryTestCase()
    {
        // Every case starts, the explicit one the run then leaves out included.
        int cases = _run.Messages.OfType<ITestCaseStarting>().Count();

        Assert.Equal(cases, _run.AssemblyStatistics.GetProperty("discoveredTestCases").GetInt32());
        Assert.Equal(cases, _run.AssemblyStatistics.GetProperty("selectedTestCases").GetInt32());
    }

    [Fact]
    public async Task TheTestsStillRunUntrackedWhenTheSdkFailsToStart()
    {
        using ProbeRun run = await ProbeRun.RunAsync(createFramework: () =>
            new XpingTestFramework(configFileName: null, () => throw new InvalidOperationException("host failed to start")));

        Assert.Contains(run.Messages.OfType<ITestStarting>(), m => m.TestDisplayName.EndsWith("OutcomeTests.Passes", StringComparison.Ordinal));
        Assert.NotEmpty(run.Messages.OfType<ITestPassed>());
        Assert.Empty(run.Messages.OfType<IErrorMessage>());
        Assert.Equal(0, run.SessionCount);
    }

    private static string? Fingerprint(JsonElement execution) =>
        execution.GetProperty("identity").GetProperty("testFingerprint").GetString();

    private static IEnumerable<string?> Tags(JsonElement execution) =>
        execution.GetProperty("metadata").GetProperty("tags").EnumerateArray().Select(t => t.GetString());

    private static TimeSpan Duration(JsonElement execution, string property) =>
        TimeSpan.Parse(execution.GetProperty(property).GetString()!, CultureInfo.InvariantCulture);
}
