/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// One page of a DataGateway list (<c>PagedResultResponse</c>), with only the members the CLI reads.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="TotalCount">How many items there are on all pages.</param>
/// <param name="PageNumber">This page, from 1.</param>
/// <param name="PageSize">The page size the server used.</param>
internal sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    /// <summary>
    /// Gets whether a later page exists.
    /// </summary>
    public bool HasNextPage => PageSize > 0 && (long)PageNumber * PageSize < TotalCount;
}
