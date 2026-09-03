/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// One observation of a binary outcome at an ordered exposure, together with the occasion it came
/// from.
/// </summary>
/// <param name="Level">
/// Where on the ordered axis this observation sits. The values are read as numbers rather than as
/// ranks — <see cref="CochranArmitage"/> scores the spacing between them, so a jump from 1 to 8
/// counts for more than a step from 7 to 8.
/// </param>
/// <param name="Occurred">Whether the behaviour being tested for happened here.</param>
/// <param name="Cluster">
/// The occasion this observation belongs to. Observations sharing a cluster are not independent, and
/// <see cref="CochranArmitage"/> says so by taking its variance over clusters rather than over
/// observations. Any stable integer identifier will do; the report passes a session's index in the
/// window, which is already the key every other ordering in the pipeline is settled by.
/// </param>
/// <remarks>
/// Deliberately not a rate table. A caller holding <c>(level, trials, successes)</c> rows has already
/// summed away which occasion each observation came from, and that is the one fact the clustered
/// variance needs. Building the level table is cheap and internal to each statistic — which is why
/// <see cref="KendallTau"/> takes the same type although it has no use for the cluster; recovering
/// the clusters from a table is impossible.
/// </remarks>
internal readonly record struct TrendPoint(int Level, bool Occurred, int Cluster);
