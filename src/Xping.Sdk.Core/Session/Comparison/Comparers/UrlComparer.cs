/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Comparers;

/// <summary>
/// Implements comparison logic for <see cref="TestSession.Url"/> property.
/// </summary>
public class UrlComparer : ITestSessionComparer
{
    /// <summary>
    /// Compares <see cref="TestSession.Url"/> property of two <see cref="TestSession"/> instances and identifies 
    /// differences.
    /// </summary>
    /// <param name="session1">The first TestSession instance.</param>
    /// <param name="session2">The second TestSession instance.</param>
    /// <returns>A DiffResult object containing the differences.</returns>
    public DiffResult Compare(TestSession session1, TestSession session2)
    {
        if (ReferenceEquals(session1, session2))
        {
            return DiffResult.Empty;
        }

        if (session1 == null || session2 == null)
        {
            return DiffResult.Empty;
        }

        if (session1.Url == session2.Url)
        {
            return DiffResult.Empty;
        }

        var result = new DiffResult();
        result.AddDifference(new Difference(
            PropertyName: nameof(TestSession.Url),
            Value1: session1.Url,
            Value2: session2.Url,
            Type: DifferenceType.Changed));

        return result;
    }
}
