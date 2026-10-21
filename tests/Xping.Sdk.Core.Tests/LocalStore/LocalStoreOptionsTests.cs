/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class LocalStoreOptionsTests
{
    [Fact]
    public void DefaultsKeepFiftyRunsOneHundredMegabytesAndThirtyDays()
    {
        var options = new LocalStoreOptions();

        Assert.Equal(50, options.MaxRuns);
        Assert.Equal(100L * 1024 * 1024, options.MaxBytes);
        Assert.Equal(TimeSpan.FromDays(30), options.MaxAge);
    }

    [Fact]
    public void DefaultsCoverTheReportsDefaultWindow()
    {
        // Retention that keeps less than the report reads leaves the default window permanently short.
        var options = new LocalStoreOptions();

        Assert.True(options.MaxRuns >= ReportWindow.DefaultRuns);
        Assert.True(options.MaxAge >= TimeSpan.FromDays(ReportWindow.DefaultDays));
    }

    [Fact]
    public void SummaryTypesRejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStoreAssembly(null!, 1, DateTime.UtcNow));
        Assert.Throws<ArgumentNullException>(() => new LocalStoreSummary(null!, 0));
    }
}
