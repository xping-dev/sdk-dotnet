/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Xping.Cli.Auth.Loopback;

/// <summary>
/// The PKCE verifier and challenge, and the <c>state</c>, of one sign-in (cli-auth-cli-spec §4.1).
/// </summary>
/// <remarks>
/// Held in memory only and never logged: the verifier is what turns an intercepted code into tokens.
/// <see cref="Dispose"/> clears the bytes; the strings cannot be cleared, and live only as long as the
/// sign-in does.
/// </remarks>
internal sealed class Pkce : IDisposable
{
    /// <summary>The <c>code_challenge_method</c>; the server accepts no other.</summary>
    public const string ChallengeMethod = "S256";

    private const int EntropyBytes = 32;

    private readonly byte[] _verifierBytes;
    private readonly byte[] _stateBytes;
    private bool _disposed;

    private Pkce(byte[] verifierBytes, byte[] stateBytes)
    {
        _verifierBytes = verifierBytes;
        _stateBytes = stateBytes;

        CodeVerifier = Base64Url.EncodeToString(verifierBytes);
        CodeChallenge = Challenge(CodeVerifier);
        State = Base64Url.EncodeToString(stateBytes);

        Redaction.AddSecret(CodeVerifier);
        Redaction.AddSecret(State);
    }

    /// <summary>
    /// Gets the verifier: 43 Base64URL characters from 32 random bytes (RFC 7636 §4.1).
    /// </summary>
    public string CodeVerifier { get; }

    /// <summary>
    /// Gets <c>Base64Url(SHA256(ASCII(verifier)))</c>.
    /// </summary>
    public string CodeChallenge { get; }

    /// <summary>
    /// Gets the <c>state</c>: 43 Base64URL characters from 32 random bytes.
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Creates fresh values from the system's cryptographic random source.
    /// </summary>
    public static Pkce Create()
    {
        byte[] verifier = new byte[EntropyBytes];
        byte[] state = new byte[EntropyBytes];
        RandomNumberGenerator.Fill(verifier);
        RandomNumberGenerator.Fill(state);

        return new Pkce(verifier, state);
    }

    /// <summary>
    /// Returns the challenge of <paramref name="verifier"/>.
    /// </summary>
    public static string Challenge(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>
    /// Returns whether <paramref name="candidate"/> is this sign-in's <c>state</c>.
    /// </summary>
    /// <remarks>
    /// Constant time after the length check, so the comparison does not tell a caller how many
    /// leading characters it got right (contract §10.1). After <see cref="Dispose"/> nothing matches,
    /// so a second redirect that arrives late cannot be accepted.
    /// </remarks>
    public bool StateMatches(string? candidate)
    {
        if (_disposed || candidate is null)
            return false;

        byte[] expected = Encoding.UTF8.GetBytes(State);
        byte[] actual = Encoding.UTF8.GetBytes(candidate);

        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CryptographicOperations.ZeroMemory(_verifierBytes);
        CryptographicOperations.ZeroMemory(_stateBytes);
    }

    /// <summary>
    /// Gets whether the verifier bytes are cleared; for tests.
    /// </summary>
    internal bool IsCleared => _verifierBytes.All(b => b == 0) && _stateBytes.All(b => b == 0);
}
