/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Reporting.Internals;

namespace Xping.Sdk.Core.Tests.Reporting;

public sealed class RetryFlakeHintTests
{
    private static List<TestExecution> Executions(params bool[] passedOnRetry) =>
        passedOnRetry.Select((flaked, i) => new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(new TestIdentityBuilder()
                .WithTestFingerprint($"fp{i}")
                .WithDisplayName($"T{i}")
                .Build())
            .WithTestName($"T{i}")
            .WithOutcome(TestOutcome.Passed)
            .WithRetry(new RetryMetadataBuilder()
                .WithAttemptNumber(flaked ? 2 : 1)
                .WithPassedOnRetry(flaked)
                .Build())
            .Build()).ToList();

    [Fact]
    public void SilentWhenNothingFlaked()
    {
        Assert.Null(RetryFlakeHint.Build(Executions(false, false), isCi: false, suppressed: false));
    }

    [Fact]
    public void SilentOnAnEmptyRun()
    {
        Assert.Null(RetryFlakeHint.Build([], isCi: false, suppressed: false));
    }

    [Fact]
    public void ReportsASingleFlakeInTheSingular()
    {
        string? hint = RetryFlakeHint.Build(Executions(true, false), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.Contains("1 test flaked on retry", hint, StringComparison.Ordinal);
        Assert.Contains("dotnet xping report", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsMultipleFlakesInThePlural()
    {
        string? hint = RetryFlakeHint.Build(Executions(true, true, false), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.Contains("2 tests flaked on retry", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void SilentInCi()
    {
        // CI logs are read when something breaks; a pointer to a local tool is noise there.
        Assert.Null(RetryFlakeHint.Build(Executions(true), isCi: true, suppressed: false));
    }

    [Fact]
    public void SilentWhenSuppressed()
    {
        Assert.Null(RetryFlakeHint.Build(Executions(true), isCi: false, suppressed: true));
    }

    [Fact]
    public void StaysOnOneLine()
    {
        // The hint's whole justification is that it is not a report. If it grows past a line it has
        // become the thing this refactor moved into the CLI.
        string? hint = RetryFlakeHint.Build(Executions(true, true), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.DoesNotContain('\n', hint);
        Assert.True(hint!.Length <= 100, $"Hint is {hint.Length} characters.");
    }
}
