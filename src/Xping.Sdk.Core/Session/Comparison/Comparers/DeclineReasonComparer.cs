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
public class DeclineReasonComparer : ITestSessionComparer
{
    /// <summary>
    /// Compares <see cref="TestSession.DeclineReason"/> property of two <see cref="TestSession"/> instances and identifies 
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

        if (session1.DeclineReason == session2.DeclineReason)
        {
            return DiffResult.Empty;
        }

        var result = new DiffResult();

        if (!string.IsNullOrEmpty(session1.DeclineReason) && string.IsNullOrEmpty(session2.DeclineReason))
        {
            result.AddDifference(GetDifference(session1.DeclineReason, null, DifferenceType.Removed));
        }
        else if (string.IsNullOrEmpty(session1.DeclineReason) && !string.IsNullOrEmpty(session2.DeclineReason))
        {
            result.AddDifference(GetDifference(null, session2.DeclineReason, DifferenceType.Added));
        }
        else if(!string.IsNullOrEmpty(session1.DeclineReason) && !string.IsNullOrEmpty(session2.DeclineReason))
        {
            result.AddDifference(
                GetDifference(session1.DeclineReason, session2.DeclineReason, DifferenceType.Changed));
        }

        return result;
    }

    private static Difference GetDifference(object? value1, object? value2, DifferenceType type) => new(
            PropertyName: nameof(TestSession.DeclineReason),
            Value1: value1,
            Value2: value2,
            Type: type);
}
