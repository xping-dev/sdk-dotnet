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
