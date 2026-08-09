/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.Reporting.Internals;

namespace Xping.Sdk.Core.Tests.Reporting;

public sealed class RetryFlakeHintTests
{
    private static List<LocalTestRecord> Records(params bool[] passedOnRetry) =>
        passedOnRetry.Select((flaked, i) => new LocalTestRecord
        {
            Fingerprint = $"fp{i}",
            Name = $"T{i}",
            Outcome = OutcomeCodes.Passed,
            PassedOnRetry = flaked,
            Attempt = flaked ? 2 : 1
        }).ToList();

    [Fact]
    public void SilentWhenNothingFlaked()
    {
        Assert.Null(RetryFlakeHint.Build(Records(false, false), isCi: false, suppressed: false));
    }

    [Fact]
    public void SilentOnAnEmptyRun()
    {
        Assert.Null(RetryFlakeHint.Build([], isCi: false, suppressed: false));
    }

    [Fact]
    public void ReportsASingleFlakeInTheSingular()
    {
        string? hint = RetryFlakeHint.Build(Records(true, false), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.Contains("1 test flaked on retry", hint, StringComparison.Ordinal);
        Assert.Contains("dotnet xping report", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsMultipleFlakesInThePlural()
    {
        string? hint = RetryFlakeHint.Build(Records(true, true, false), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.Contains("2 tests flaked on retry", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void SilentInCi()
    {
        // CI logs are read when something breaks; a pointer to a local tool is noise there.
        Assert.Null(RetryFlakeHint.Build(Records(true), isCi: true, suppressed: false));
    }

    [Fact]
    public void SilentWhenSuppressed()
    {
        Assert.Null(RetryFlakeHint.Build(Records(true), isCi: false, suppressed: true));
    }

    [Fact]
    public void StaysOnOneLine()
    {
        // The hint's whole justification is that it is not a report. If it grows past a line it has
        // become the thing this refactor moved into the CLI.
        string? hint = RetryFlakeHint.Build(Records(true, true), isCi: false, suppressed: false);

        Assert.NotNull(hint);
        Assert.DoesNotContain('\n', hint);
        Assert.True(hint!.Length <= 100, $"Hint is {hint.Length} characters.");
    }
}
