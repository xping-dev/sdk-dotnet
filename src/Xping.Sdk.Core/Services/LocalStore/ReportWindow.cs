/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// The default window <c>xping report</c> analyses: the newest runs of one test assembly, kept to a
/// recent span of days, whichever yields fewer.
/// </summary>
/// <remarks>
/// Defined here rather than in the CLI because it is the floor <see cref="LocalStoreOptions"/> must
/// cover. Retention that kept fewer runs, or a shorter span, than the report reads would leave the
/// default window permanently short, and nothing would say so.
/// </remarks>
public static class ReportWindow
{
    /// <summary>
    /// Runs in the default window (20).
    /// </summary>
    public const int DefaultRuns = 20;

    /// <summary>
    /// Days in the default window (14).
    /// </summary>
    public const int DefaultDays = 14;
}
