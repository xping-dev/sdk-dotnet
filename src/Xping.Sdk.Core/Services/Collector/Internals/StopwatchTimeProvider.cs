/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;

namespace Xping.Sdk.Core.Services.Collector.Internals;

/// <summary>
/// Production implementation of <see cref="ITimeProvider"/> backed by <see cref="Stopwatch"/>.
/// </summary>
internal sealed class StopwatchTimeProvider : ITimeProvider
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();
    public long Frequency => Stopwatch.Frequency;
}
