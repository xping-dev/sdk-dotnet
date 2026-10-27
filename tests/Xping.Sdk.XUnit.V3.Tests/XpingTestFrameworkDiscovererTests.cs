/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Tests;

public sealed class XpingTestFrameworkDiscovererTests
{
    [Fact]
    public async Task Find_WholeAssembly_CountsEveryCaseAndHandsEachToTheRunner()
    {
        var census = new TestCaseCensus();
        var handed = new List<ITestCase>();

        await FindAsync(census, Callback(handed));

        Assert.Equal(3, census.Discovered);
        Assert.Equal(3, handed.Count);
    }

    [Fact]
    public async Task Find_StoppedByTheRunner_RecordsNoCount()
    {
        var census = new TestCaseCensus();

        await FindAsync(census, _ => new ValueTask<bool>(false));

        Assert.Null(census.Discovered);
    }

    [Fact]
    public async Task Find_Cancelled_RecordsNoCount()
    {
        var census = new TestCaseCensus();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await FindAsync(census, Callback([]), cancellationToken: cancellation.Token);

        Assert.Null(census.Discovered);
    }

    [Fact]
    public async Task Find_SomeTypes_IsNotACensusOfTheAssembly()
    {
        var census = new TestCaseCensus();
        var handed = new List<ITestCase>();

        await FindAsync(census, Callback(handed), types: [typeof(XpingTestFrameworkDiscovererTests)]);

        Assert.Null(census.Discovered);
        Assert.Equal(3, handed.Count);
    }

    [Fact]
    public async Task TestAssembly_IsTheInnerDiscoverers()
    {
        ITestAssembly assembly = Mock.Of<ITestAssembly>();
        var discoverer = new XpingTestFrameworkDiscoverer(Mock.Of<ITestFrameworkDiscoverer>(d => d.TestAssembly == assembly), new TestCaseCensus());
        await using (discoverer.ConfigureAwait(true))
        {
            Assert.Same(assembly, discoverer.TestAssembly);
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesAnInnerDiscovererThatIsOnlyDisposable()
    {
        var inner = new Mock<ITestFrameworkDiscoverer>();
        Mock<IDisposable> disposable = inner.As<IDisposable>();

        await new XpingTestFrameworkDiscoverer(inner.Object, new TestCaseCensus()).DisposeAsync();

        disposable.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAnAsyncDisposableInnerDiscoverer()
    {
        var inner = new Mock<ITestFrameworkDiscoverer>();
        Mock<IAsyncDisposable> disposable = inner.As<IAsyncDisposable>();
        disposable.Setup(d => d.DisposeAsync()).Returns(default(ValueTask));

        await new XpingTestFrameworkDiscoverer(inner.Object, new TestCaseCensus()).DisposeAsync();

        disposable.Verify(d => d.DisposeAsync(), Times.Once);
    }

    /// <summary>Searches three discovered cases through a discoverer counting into <paramref name="census"/>.</summary>
    private static async Task FindAsync(
        TestCaseCensus census,
        Func<ITestCase, ValueTask<bool>> callback,
        Type[]? types = null,
        CancellationToken? cancellationToken = null)
    {
        var discoverer = new XpingTestFrameworkDiscoverer(Discovering(3), census);
        await using (discoverer.ConfigureAwait(true))
        {
            await discoverer.Find(callback, Mock.Of<ITestFrameworkDiscoveryOptions>(), types, cancellationToken).ConfigureAwait(true);
        }
    }

    private static Func<ITestCase, ValueTask<bool>> Callback(List<ITestCase> handed) => testCase =>
    {
        handed.Add(testCase);
        return new ValueTask<bool>(true);
    };

    /// <summary>A discoverer that finds <paramref name="count"/> cases, and stops when the runner asks.</summary>
    private static ITestFrameworkDiscoverer Discovering(int count)
    {
        var inner = new Mock<ITestFrameworkDiscoverer>();
        inner
            .Setup(d => d.Find(
                It.IsAny<Func<ITestCase, ValueTask<bool>>>(),
                It.IsAny<ITestFrameworkDiscoveryOptions>(),
                It.IsAny<Type[]?>(),
                It.IsAny<CancellationToken?>()))
            .Returns<Func<ITestCase, ValueTask<bool>>, ITestFrameworkDiscoveryOptions, Type[]?, CancellationToken?>(
                async (callback, _, _, _) =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!await callback(Mock.Of<ITestCase>()).ConfigureAwait(true))
                            return;
                    }
                });
        return inner.Object;
    }
}
