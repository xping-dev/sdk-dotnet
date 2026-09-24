/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

/// <summary>
/// What one test framework instance discovered in its assembly, and what it was then asked to run.
/// </summary>
/// <remarks>
/// <para>
/// The two counts are what tells a run under <c>dotnet test --filter</c> from one whose tests were
/// deleted. Under <c>dotnet test</c> the runner discovers the whole assembly in the test process and
/// only then applies the filter, so a filtered run discovers more test cases than it selects and a
/// run after a deletion does not.
/// </para>
/// <para>
/// Counts rather than sets of ids, because two integers are all anything reads and an assembly of
/// pre-enumerated theories can hold a hundred thousand cases. A discovery replaces the one before
/// it — xUnit already reports each case once per search — and selections add up, since each call to
/// run hands the executor cases it was not handed before.
/// </para>
/// </remarks>
internal sealed class TestCaseCensus
{
    private int _discovered = -1;
    private int _selected;

    /// <summary>
    /// Gets how many test cases the last complete whole-assembly discovery found, or
    /// <see langword="null"/> when none completed in this process — as when an IDE runs test cases
    /// it discovered earlier.
    /// </summary>
    public int? Discovered => Volatile.Read(ref _discovered) is int discovered and >= 0 ? discovered : null;

    /// <summary>
    /// Gets how many test cases the framework has been asked to run.
    /// </summary>
    public int Selected => Volatile.Read(ref _selected);

    /// <summary>Records a whole-assembly discovery that ran to completion.</summary>
    /// <param name="count">The test cases it found.</param>
    public void CompleteDiscovery(int count) => Volatile.Write(ref _discovered, count);

    /// <summary>Records test cases the framework was asked to run.</summary>
    /// <param name="count">How many.</param>
    public void AddSelected(int count) => Interlocked.Add(ref _selected, count);
}
