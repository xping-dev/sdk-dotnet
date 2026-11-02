/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;

namespace Xping.Cli.Cloud;

/// <summary>
/// Answers whether Xping Cloud has seen one local failure's signature for its test
/// (cloud-data-in-cli-cli-spec §5.3, contract §4.7).
/// </summary>
internal static class SignatureMatcher
{
    /// <summary>Cloud has the signature for the test.</summary>
    public const string Matched = "matched";

    /// <summary>Cloud has the test and not the signature.</summary>
    public const string Unseen = "unseen";

    /// <summary>The local failure has no signature.</summary>
    public const string Unsignable = "unsignable";

    /// <summary>No answer to match against.</summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// Matches one local failure against its test's digest.
    /// </summary>
    /// <param name="errorMessageHash">The failure's message hash as stored, or null when it has none.</param>
    /// <param name="digest">The test's digest, or null when Cloud did not answer for it.</param>
    /// <param name="algorithmMismatch">
    /// Whether the project answered under another signature algorithm, so no hash can be compared
    /// (contract §11).
    /// </param>
    /// <returns>The match, with the signature when matched.</returns>
    /// <remarks>
    /// A requested hash is always in the digest's signatures, with zero runs when Cloud never saw it
    /// (contract §6.2). One that is absent was not requested, being beyond the hashes a target
    /// carries, and reads as unseen; that is the one approximation here.
    /// </remarks>
    public static CloudMatchDto Match(string? errorMessageHash, CloudDigestDto? digest, bool algorithmMismatch)
    {
        if (algorithmMismatch || digest is not { State: "ok" })
            return new CloudMatchDto(Unknown, null);

        if (errorMessageHash is null)
            return new CloudMatchDto(Unsignable, null);

        CloudSignatureDto? signature = digest.Signatures.FirstOrDefault(s => string.Equals(s.ErrorMessageHash, errorMessageHash, StringComparison.Ordinal));
        return signature is { Runs: > 0 } ? new CloudMatchDto(Matched, signature) : new CloudMatchDto(Unseen, null);
    }
}
