/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Defines the interface for comparing two TestSession instances.
/// </summary>
public interface ITestSessionComparer
{
    /// <summary>
    /// Compares two TestSession instances and returns the result.
    /// </summary>
    /// <param name="session1">The first TestSession instance.</param>
    /// <param name="session2">The second TestSession instance.</param>
    /// <returns>A DiffResult object containing the comparison outcome.</returns>
    DiffResult Compare(TestSession session1, TestSession session2);
}
