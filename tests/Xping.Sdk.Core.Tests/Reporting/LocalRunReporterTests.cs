/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;
using Xping.Sdk.Core.Services.Reporting.Internals;

namespace Xping.Sdk.Core.Tests.Reporting;

// Mutates XPING_LOCAL_STORE / XPING_NO_BANNER, which are process-wide state.
[Collection("Sequential")]
public sealed class LocalRunReporterTests : IDisposable
{
    private readonly string _root;

    public LocalRunReporterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-reporter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, _root);
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressBannerVariable, null);
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressReportVariable, null);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressBannerVariable, null);
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressReportVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static LocalRunReporter CreateReporter(
        XpingMode mode = XpingMode.LocalOnly,
        bool isCi = false,
        ILocalRunStore? store = null) =>
        new(store ?? new JsonLinesRunStore(new LocalStoreOptions(), NullLogger.Instance),
            new LocalStoreOptions(),
            mode,
            isCi,
            NullLogger<LocalRunReporter>.Instance);

    private static LocalRun BuildRun(string outcome = OutcomeCodes.Passed) =>
        new(
            new LocalRunHeader { SessionId = Guid.NewGuid().ToString("N"), StartedAtUtc = DateTime.UtcNow },
            [new LocalTestRecord { Fingerprint = "fp", Name = "T", Outcome = outcome }]);

    private static QuickStatistics Stats() =>
        new() { Total = 1, Passed = 1, WallClockDurationMs = 10 };

    [Fact]
    public void ReportPersistsTheRun()
    {
        // Arrange
        var store = new JsonLinesRunStore(new LocalStoreOptions(), NullLogger.Instance);
        var reporter = CreateReporter(store: store);

        // Act
        reporter.Report(BuildRun(), Stats());

        // Assert
        Assert.Single(store.ReadRecent(10));
    }

    [Fact]
    public void ReportStillPersistsWhenTheReportIsSuppressed()
    {
        // Arrange — turning off the display must not turn off history collection.
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressReportVariable, "off");
        var store = new JsonLinesRunStore(new LocalStoreOptions(), NullLogger.Instance);
        var reporter = CreateReporter(store: store);

        // Act
        reporter.Report(BuildRun(), Stats());

        // Assert
        Assert.Single(store.ReadRecent(10));
    }

    [Fact]
    public void ReportIsResilientToAnUnavailableStore()
    {
        // Arrange
        var reporter = CreateReporter(store: new UnavailableStore());

        // Act & Assert — a broken store must never fail the test run.
        reporter.Report(BuildRun(), Stats());
    }

    [Fact]
    public void ReportThrowsOnNullRun()
    {
        Assert.Throws<ArgumentNullException>(() => CreateReporter().Report(null!, Stats()));
    }

    // -----------------------------------------------------------------------
    // CTA throttling. Every rule here exists to stop the invitation becoming
    // noise, which is what earns the right to print anything at all.
    // -----------------------------------------------------------------------

    [Fact]
    public void CtaIsWrittenAtMostOncePerDay()
    {
        // Arrange
        var reporter = CreateReporter();
        var analysis = new LocalAnalysis
        {
            UnstableTests = [new UnstableTest { Name = "T", Kind = InstabilityKind.FlakedInRun }],
            RunsAnalysed = 5,
            MinimumRunsForHistory = 3
        };

        // Act
        bool first = InvokeShouldShowCta(reporter, analysis);
        bool second = InvokeShouldShowCta(reporter, analysis);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void CtaIsSuppressedInCi()
    {
        // CI logs are read when something breaks; a signup pitch there is pure noise.
        var reporter = CreateReporter(isCi: true);
        Assert.False(InvokeShouldShowCta(reporter, AnalysisWithFindings()));
    }

    [Fact]
    public void CtaIsSuppressedForConnectedUsers()
    {
        // They already signed up.
        var reporter = CreateReporter(mode: XpingMode.Connected);
        Assert.False(InvokeShouldShowCta(reporter, AnalysisWithFindings()));
    }

    [Fact]
    public void CtaIsSuppressedByEnvironmentVariable()
    {
        System.Environment.SetEnvironmentVariable(LocalRunReporter.SuppressBannerVariable, "1");
        var reporter = CreateReporter();
        Assert.False(InvokeShouldShowCta(reporter, AnalysisWithFindings()));
    }

    [Fact]
    public void CtaIsSuppressedAfterACleanRun()
    {
        // Never pitch when nothing was found; the ask has to be tied to a problem just shown.
        var reporter = CreateReporter();
        var clean = new LocalAnalysis { RunsAnalysed = 5, MinimumRunsForHistory = 3 };

        Assert.False(InvokeShouldShowCta(reporter, clean));
    }

    private static LocalAnalysis AnalysisWithFindings() => new()
    {
        UnstableTests = [new UnstableTest { Name = "T", Kind = InstabilityKind.FlakedInRun }],
        RunsAnalysed = 5,
        MinimumRunsForHistory = 3
    };

    private static bool InvokeShouldShowCta(LocalRunReporter reporter, LocalAnalysis analysis)
    {
        var method = typeof(LocalRunReporter).GetMethod(
            "ShouldShowCta",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        return (bool)method!.Invoke(reporter, [analysis])!;
    }

    private sealed class UnavailableStore : ILocalRunStore
    {
        public bool IsAvailable => false;

        public string? StorePath => null;

        public bool Write(LocalRun run) => false;

        public IReadOnlyList<LocalRun> ReadRecent(int maxRuns, string? assembly = null) => [];
    }
}
