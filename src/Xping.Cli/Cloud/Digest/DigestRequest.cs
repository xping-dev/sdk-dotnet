/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud.Digest;

/// <summary>
/// The body of <c>POST /v1/projects/{projectKey}/tests/digest</c> (contract §6.1).
/// </summary>
/// <param name="Tests">The tests asked about, 1 to <c>MaxDigestBatchSize</c>, no fingerprint twice.</param>
/// <param name="SessionKeys">Sessions this machine recorded, whose upload state Cloud reports.</param>
internal sealed record DigestRequest(IReadOnlyList<DigestRequestTest> Tests, IReadOnlyList<string> SessionKeys);

/// <summary>
/// One test in a <see cref="DigestRequest"/>.
/// </summary>
/// <param name="Fingerprint">The test's fingerprint.</param>
/// <param name="ErrorMessageHashes">
/// The signatures this machine saw for the test; Cloud answers each, even with a count of zero.
/// </param>
internal sealed record DigestRequestTest(string Fingerprint, IReadOnlyList<string> ErrorMessageHashes);
