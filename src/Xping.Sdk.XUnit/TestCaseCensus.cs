/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;

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
/// Both sides count test cases by <c>UniqueID</c> rather than by message, so a runner that asks
/// twice counts each case once. Discovery counts only once a whole-assembly search has completed:
/// a search of one class is not a census of the assembly, and a count taken midway is not a count.
/// </para>
/// </remarks>
internal sealed class TestCaseCensus
{
    private readonly ConcurrentDictionary<string, byte> _discovered = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _selected = new(StringComparer.Ordinal);
    private int _discoveryCompleted;

    /// <summary>
    /// Gets how many test cases a completed whole-assembly discovery found, or <see langword="null"/>
    /// when none completed in this process — as when an IDE runs test cases it discovered earlier.
    /// </summary>
    public int? Discovered => Volatile.Read(ref _discoveryCompleted) == 1 ? _discovered.Count : null;

    /// <summary>
    /// Gets how many test cases the framework has been asked to run.
    /// </summary>
    public int Selected => _selected.Count;

    /// <summary>Records one test case found by a whole-assembly discovery.</summary>
    public void AddDiscovered(string uniqueId) => _discovered.TryAdd(uniqueId, 0);

    /// <summary>Marks a whole-assembly discovery as complete.</summary>
    public void CompleteDiscovery() => Volatile.Write(ref _discoveryCompleted, 1);

    /// <summary>Records one test case the framework was asked to run.</summary>
    public void AddSelected(string uniqueId) => _selected.TryAdd(uniqueId, 0);
}
