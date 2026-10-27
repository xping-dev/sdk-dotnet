/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>One run of every probe test, shared by the tests that only read what it stored.</summary>
public sealed class WholeProbeRun : IAsyncLifetime
{
    internal ProbeRun Run { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Run = await ProbeRun.RunAsync().ConfigureAwait(true);

    public ValueTask DisposeAsync()
    {
        Run.Dispose();
        return default;
    }
}
