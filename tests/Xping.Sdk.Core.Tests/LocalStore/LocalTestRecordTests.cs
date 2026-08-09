/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Local;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class LocalTestRecordTests
{
    [Theory]
    [InlineData(TestOutcome.Passed, OutcomeCodes.Passed)]
    [InlineData(TestOutcome.Failed, OutcomeCodes.Failed)]
    [InlineData(TestOutcome.Skipped, OutcomeCodes.Skipped)]
    [InlineData(TestOutcome.Inconclusive, OutcomeCodes.Inconclusive)]
    [InlineData(TestOutcome.NotExecuted, OutcomeCodes.NotExecuted)]
    public void ProjectsEveryOutcome(TestOutcome outcome, string expected)
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(outcome)
            .Build();

        Assert.Equal(expected, LocalTestRecord.FromExecution(execution).Outcome);
    }

    [Fact]
    public void ProjectsDurationAsWholeMilliseconds()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(1234.9))
            .Build();

        Assert.Equal(1234, LocalTestRecord.FromExecution(execution).DurationMs);
    }

    [Fact]
    public void DefaultsToFirstAttemptWhenThereIsNoRetryMetadata()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(TestOutcome.Passed)
            .Build();

        var record = LocalTestRecord.FromExecution(execution);

        Assert.Equal(1, record.Attempt);
        Assert.False(record.PassedOnRetry);
    }

    [Fact]
    public void ThrowsOnNullExecution()
    {
        Assert.Throws<ArgumentNullException>(() => LocalTestRecord.FromExecution(null!));
    }

    [Fact]
    public void FallsBackToTestNameWhenThereIsNoDisplayName()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("Fallback.Name")
            .WithOutcome(TestOutcome.Passed)
            .Build();

        Assert.Equal("Fallback.Name", LocalTestRecord.FromExecution(execution).Name);
    }

    [Fact]
    public void TruncatesTheErrorHashToEightCharacters()
    {
        // The error hash only has to group similar failures, so a short prefix is enough.
        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(TestOutcome.Failed)
            .WithException("InvalidOperationException", "boom")
            .WithErrorMessageHash(new string('a', 64))
            .Build();

        Assert.Equal(new string('a', 8), LocalTestRecord.FromExecution(execution).ErrorHash);
    }

    [Fact]
    public void PreservesTheFullFingerprint()
    {
        // The fingerprint has to join against cloud identity, so unlike the error hash it must
        // survive the projection intact.
        var identity = new TestIdentityBuilder()
            .WithTestFingerprint(new string('c', 64))
            .WithFullyQualifiedName("My.Namespace.Class.Method")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(TestOutcome.Passed)
            .WithIdentity(identity)
            .Build();

        var record = LocalTestRecord.FromExecution(execution);

        Assert.Equal(identity.TestFingerprint, record.Fingerprint);
        Assert.Equal(64, record.Fingerprint.Length);
    }

    [Fact]
    public void CarriesRetryMetadataThroughToTheRecord()
    {
        // PassedOnRetry drives both the SDK's one-line hint and the CLI's strongest flakiness
        // signal, so it has to survive the projection.
        var retry = new RetryMetadataBuilder()
            .WithAttemptNumber(3)
            .WithPassedOnRetry(true)
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("T")
            .WithOutcome(TestOutcome.Passed)
            .WithRetry(retry)
            .Build();

        var record = LocalTestRecord.FromExecution(execution);

        Assert.Equal(3, record.Attempt);
        Assert.True(record.PassedOnRetry);
    }
}
