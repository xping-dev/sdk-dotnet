/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Formats the differences between two TestSession instances into a readable format.
/// </summary>
public interface IDiffPresenter
{
    /// <summary>
    /// Formats the given DiffResult into a human-readable string.
    /// </summary>
    /// <param name="diffResult">The DiffResult to format.</param>
    /// <returns>A string representing the formatted differences.</returns>
    string FormatDiff(DiffResult diffResult);
}
