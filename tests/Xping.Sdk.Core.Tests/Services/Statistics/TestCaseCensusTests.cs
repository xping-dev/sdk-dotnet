/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Statistics;

namespace Xping.Sdk.Core.Tests.Services.Statistics;

public sealed class TestCaseCensusTests
{
    [Fact]
    public void Discovered_BeforeAnyDiscoveryCompletes_IsNullNotZero()
    {
        Assert.Null(new TestCaseCensus().Discovered);
    }

    [Fact]
    public void Discovered_CompletedWithNothingFound_IsZeroNotNull()
    {
        var census = new TestCaseCensus();
        census.CompleteDiscovery(0);

        Assert.Equal(0, census.Discovered);
    }

    [Fact]
    public void Discovered_ALaterDiscoveryReplacesAnEarlierOne()
    {
        // Each search reports every case once, so a second search is a recount, not more cases.
        var census = new TestCaseCensus();
        census.CompleteDiscovery(17);
        census.CompleteDiscovery(17);

        Assert.Equal(17, census.Discovered);
    }

    [Fact]
    public void Selected_AddsUpAcrossRuns()
    {
        var census = new TestCaseCensus();
        census.AddSelected(3);
        census.AddSelected(2);

        Assert.Equal(5, census.Selected);
    }
}
