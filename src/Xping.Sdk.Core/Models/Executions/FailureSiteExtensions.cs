/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Questions about a <see cref="FailureSite"/> that more than one caller needs to ask.
/// </summary>
public static class FailureSiteExtensions
{
    /// <summary>
    /// Returns whether the failure happened in shared lifecycle code rather than in a test body.
    /// </summary>
    /// <param name="site">The site to classify.</param>
    /// <returns>
    /// <see langword="true"/> for every site except <see cref="FailureSite.TestBody"/> and
    /// <see cref="FailureSite.Unknown"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the question the report actually asks, and it is deliberately broader than "was it a
    /// one-time fixture". A <c>[SetUp]</c> that throws for every test in a class produces the same
    /// symptom a broken <c>[OneTimeSetUp]</c> does — a page of identical failures whose defect lives in
    /// one shared member — and the fix is the same. Keying on fixture scope alone would answer the
    /// narrower question and miss most of the cases that occur.
    /// </para>
    /// <para>
    /// <see cref="FailureSite.Unknown"/> is false because it is an admission, not an observation. A
    /// site the adapter could not resolve must not be counted as evidence that lifecycle code failed.
    /// </para>
    /// </remarks>
    public static bool IsLifecycle(this FailureSite site) =>
        site != FailureSite.TestBody && site != FailureSite.Unknown;
}
