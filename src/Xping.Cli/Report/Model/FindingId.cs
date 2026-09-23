/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Xping.Cli.Report.Model;

/// <summary>
/// Computes the stable short identity carried by every finding.
/// </summary>
/// <remarks>
/// <para>
/// The id is a function of what the finding claims and what it claims it about — nothing else. It
/// identifies the <em>claim</em>, not the measurement behind it: the same id can appear in two
/// reports quoting different failure rates, because a rate that moved as runs accumulated is the
/// same finding observed again, not a new one. That is what lets a consumer — a person comparing
/// two reports, an agent, or <c>xping report --id &lt;id&gt;</c> — say "still the same finding"
/// across runs rather than only across two renders of an unchanged store.
/// </para>
/// <para>
/// The window is deliberately not an input. Folding it in would re-hash every id the moment a run
/// was appended, which is precisely when a reader most wants to know whether they are looking at
/// something they have already seen.
/// </para>
/// <para>
/// Hashing is SHA-256, never <see cref="object.GetHashCode"/>, which is randomised per process on
/// .NET Core and would produce a different id on every invocation.
/// </para>
/// </remarks>
internal static class FindingId
{
    // Eight hex characters. A shorter id makes collisions plausible once a report carries a few
    // dozen findings, and two findings sharing an id is indistinguishable from one finding to any
    // consumer keyed on it.
    private const int HexLength = 8;

    /// <summary>
    /// Computes the identity for a finding.
    /// </summary>
    /// <param name="kind">What the finding claims.</param>
    /// <param name="subjectKey">Identity of the test or group the finding is about.</param>
    /// <returns>An identifier of the form <c>f_2a91c0de</c>.</returns>
    /// <remarks>
    /// The pair is unique within a report: every provider emits at most one candidate per kind for
    /// a given subject, so no two findings in one render can collide on these inputs.
    /// </remarks>
    public static string Compute(FindingKind kind, string subjectKey)
    {
        // The separator cannot appear in an enum name or a fingerprint, so no pair of different
        // inputs can produce the same canonical string.
        string canonical = string.Create(CultureInfo.InvariantCulture, $"{kind} {subjectKey}");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        var builder = new StringBuilder("f_", 2 + HexLength);
        for (int i = 0; i < HexLength / 2; i++)
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

        return builder.ToString();
    }
}
