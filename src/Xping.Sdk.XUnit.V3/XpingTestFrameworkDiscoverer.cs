/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Statistics;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Wraps xUnit's discoverer to count the test cases a whole-assembly discovery finds.
/// </summary>
/// <remarks>
/// <para>
/// Discovery itself is untouched: every test case is handed to the runner as it is found. xUnit v3
/// applies a run's filter in the runner's own callback, after the discoverer has found a case, so what
/// passes through here is the whole assembly whether or not the run is filtered.
/// </para>
/// <para>
/// Only a search of the whole assembly is counted; one limited to some types passes straight through.
/// </para>
/// </remarks>
internal sealed class XpingTestFrameworkDiscoverer(ITestFrameworkDiscoverer inner, TestCaseCensus census)
    : ITestFrameworkDiscoverer, IAsyncDisposable
{
    /// <inheritdoc/>
    public ITestAssembly TestAssembly => inner.TestAssembly;

    /// <inheritdoc/>
    public async ValueTask Find(
        Func<ITestCase, ValueTask<bool>> callback,
        ITestFrameworkDiscoveryOptions discoveryOptions,
        Type[]? types = null,
        CancellationToken? cancellationToken = null)
    {
        if (types != null)
        {
            await inner.Find(callback, discoveryOptions, types, cancellationToken).ConfigureAwait(false);
            return;
        }

        int count = 0;
        bool stopped = false;

        await inner.Find(
            async testCase =>
            {
                Interlocked.Increment(ref count);

                bool keepGoing = await callback(testCase).ConfigureAwait(false);
                if (!keepGoing)
                    Volatile.Write(ref stopped, true);

                return keepGoing;
            },
            discoveryOptions,
            types: null,
            cancellationToken).ConfigureAwait(false);

        // A runner that stopped discovery, or cancelled it, saw part of the assembly. A missing count
        // falls back to the report's threshold, where a short one would be trusted as fact.
        if (!Volatile.Read(ref stopped) && cancellationToken?.IsCancellationRequested != true)
            census.CompleteDiscovery(Volatile.Read(ref count));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        switch (inner)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
