/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Signatures;

/// <summary>
/// Builds the signature one failure is grouped by.
/// </summary>
/// <remarks>
/// Hashing is SHA-256 over a canonical string, never <see cref="object.GetHashCode"/>, which is
/// randomised per process on .NET Core and would give the same failure a different signature on
/// every invocation — making the report's byte-identical-output requirement unmeetable.
/// </remarks>
internal static class FailureSignatureFactory
{
    // Sixteen hex characters. Long enough that two unrelated failures colliding is not something a
    // developer will ever see; short enough to read out of a report.
    private const int HexLength = 16;

    // Unit and record separators. Neither can appear in a type name, a normalised message or a frame
    // signature, so no two different component sets can produce the same canonical string.
    private const char FieldSeparator = '\u001f';
    private const char FrameSeparator = '\u001e';

    // Distinguishes a signature that stands for "nothing was recorded" from one built out of real
    // components that happened to be short.
    private const string UnsignablePrefix = "unsignable";

    /// <summary>
    /// Builds the signature for one failed execution.
    /// </summary>
    /// <param name="execution">The failed execution.</param>
    /// <param name="fingerprint">The failing test's stable identity.</param>
    /// <returns>The signature.</returns>
    /// <remarks>
    /// When the adapter recorded no exception type, no message and no stack trace there is nothing to
    /// compare against another failure, and the honest signature is one that groups with nothing. It
    /// is keyed on the test itself, so a suite whose adapter records no failure detail at all cannot
    /// collapse into a single false shared cause — every one of its tests would otherwise carry the
    /// same empty signature and cluster together.
    /// </remarks>
    public static FailureSignature Create(TestExecution execution, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(execution);

        string? exceptionType = string.IsNullOrWhiteSpace(execution.ExceptionType)
            ? null
            : execution.ExceptionType.Trim();

        string message = MessageNormaliser.Normalise(execution.ErrorMessage);
        FrameExtraction frames = StackFrameExtractor.Extract(execution.StackTrace);

        if (exceptionType == null && message.Length == 0 && frames.Frames.Count == 0)
            return Unsignable(fingerprint);

        var canonical = new StringBuilder();
        canonical.Append(exceptionType).Append(FieldSeparator);
        canonical.Append(message).Append(FieldSeparator);

        for (int i = 0; i < frames.Frames.Count; i++)
        {
            if (i > 0)
                canonical.Append(FrameSeparator);

            canonical.Append(frames.Frames[i]);
        }

        return new FailureSignature(
            Hash(canonical.ToString()),
            exceptionType,
            message,
            frames.Frames,
            frames.Degraded,
            Unavailable: false);
    }

    /// <summary>
    /// Builds the signature for a failure the adapter recorded no detail about.
    /// </summary>
    /// <param name="fingerprint">The failing test's stable identity.</param>
    /// <returns>A signature that can only ever group with this same test's other blank failures.</returns>
    private static FailureSignature Unsignable(string fingerprint) =>
        new(
            Hash(string.Create(
                CultureInfo.InvariantCulture, $"{UnsignablePrefix}{FieldSeparator}{fingerprint}")),
            ExceptionType: null,
            NormalisedMessage: string.Empty,
            Frames: [],
            Degraded: true,
            Unavailable: true);

    private static string Hash(string canonical)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        var builder = new StringBuilder(HexLength);
        for (int i = 0; i < HexLength / 2; i++)
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

        return builder.ToString();
    }
}
