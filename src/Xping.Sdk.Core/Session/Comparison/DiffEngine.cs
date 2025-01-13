/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Orchestrates the comparison process for TestSession instances.
/// </summary>
public class DiffEngine
{
    private readonly List<ITestSessionComparer> _comparers = [];

    /// <summary>
    /// Gets a read only collection of registered test session comparers.
    /// </summary>
    public IReadOnlyCollection<ITestSessionComparer> Comparers => _comparers;

    /// <summary>
    /// Initializes a new instance of the DiffEngine class.
    /// </summary>
    /// <param name="comparers">The comparers to use for TestSession comparison.</param>
    public DiffEngine(params ITestSessionComparer[] comparers)
    {
        if (comparers != null)
        {
            foreach (var comparer in comparers)
            {
                this._comparers.Add(comparer);
            }
        }
    }

    /// <summary>
    /// Executes the comparison between two TestSession instances.
    /// </summary>
    /// <param name="session1">The first TestSession instance.</param>
    /// <param name="session2">The second TestSession instance.</param>
    /// <returns>A DiffResult object containing the comparison outcome.</returns>
    public DiffResult ExecuteDiff(TestSession? session1, TestSession? session2)
    {
        if (session1 == null || session2 == null || session1 == session2)
        {
            return DiffResult.Empty;
        }

        var diffResult = new DiffResult(session1, session2);

        foreach (var comparer in _comparers)
        {
            diffResult.Add(comparer.Compare(session1, session2));
        }

        return diffResult;
    }
}
