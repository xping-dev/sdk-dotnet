/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Whether a keychain can be used by this process right now (cli-auth-cli-spec §7.4).
/// </summary>
/// <param name="Available">Whether sign-ins can be read from and written to it.</param>
/// <param name="Reason">
/// Why not, worded for the user ("keychain locked"); <see langword="null"/> when available.
/// </param>
internal sealed record KeychainProbe(bool Available, string? Reason)
{
    /// <summary>
    /// The keychain can be used.
    /// </summary>
    public static KeychainProbe Ok { get; } = new(true, null);

    /// <summary>
    /// The keychain cannot be used, for <paramref name="reason"/>.
    /// </summary>
    public static KeychainProbe Unavailable(string reason) => new(false, reason);
}

/// <summary>
/// The operating system's credential store (cli-auth-cli-spec §7.3).
/// </summary>
internal interface IKeychainCredentialStore : ICredentialStore
{
    /// <summary>
    /// Gets the largest secret, in bytes, the store holds, or <see langword="null"/> when it has no
    /// practical limit.
    /// </summary>
    int? MaxSecretBytes { get; }

    /// <summary>
    /// Looks up the entry for <paramref name="cloudUrl"/> to learn whether the store is usable.
    /// </summary>
    /// <remarks>
    /// The real entry name is used, so "available" also means "readable for this user right now".
    /// Never throws: every failure is a reason to fall back to the file.
    /// </remarks>
    KeychainProbe Probe(string cloudUrl);
}
