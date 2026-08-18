/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Signatures;

/// <summary>
/// What one failure looked like, reduced to something two failures can be compared by.
/// </summary>
/// <remarks>
/// <para>
/// The readable components travel with the hash rather than being recoverable from it. The hash
/// exists only for grouping; a renderer showing a developer why two failures were grouped, and a
/// model asked to reason about them, both need the text.
/// </para>
/// <para>
/// Computed from the raw <c>ErrorMessage</c> and <c>StackTrace</c>, never from the SDK's
/// <c>ErrorMessageHash</c> / <c>StackTraceHash</c>. Those exist so the cloud can group failures it
/// is not allowed to see the text of; using them locally would throw away the entire advantage of
/// running on the machine that has the text.
/// </para>
/// </remarks>
/// <param name="Hash">Stable identity used to group failures. Never <see cref="object.GetHashCode"/>.</param>
/// <param name="ExceptionType">The type the adapter recorded, or <see langword="null"/> when it recorded none.</param>
/// <param name="NormalisedMessage">The error message with run-varying detail replaced by tokens.</param>
/// <param name="Frames">The frames that contributed, method signature only.</param>
/// <param name="Degraded">
/// Whether the frames are worse than intended — no user frames were found and framework frames were
/// used instead, or there were no frames at all.
/// </param>
/// <param name="Unavailable">
/// Whether the adapter recorded nothing to build a signature from. Such a signature is keyed on the
/// test itself and can never group with another test's failure.
/// </param>
internal sealed record FailureSignature(
    string Hash,
    string? ExceptionType,
    string NormalisedMessage,
    IReadOnlyList<string> Frames,
    bool Degraded,
    bool Unavailable);
