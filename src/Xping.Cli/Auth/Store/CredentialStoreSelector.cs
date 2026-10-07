/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Decides, once per process, which credential stores this machine offers (cli-auth-cli-spec §7.4).
/// </summary>
/// <remarks>
/// The OS keychain is used when its probe succeeds; otherwise sign-ins go to the file, with the
/// reason shown to the user. A keychain whose probe failed is left out of the read order: reading it
/// would fail the same way, and <c>auth status</c> would report a store failure on every machine
/// without a keyring. It is still tried by <c>logout</c>.
/// </remarks>
/// <param name="file">The file store, always present.</param>
/// <param name="keychain">The OS keychain, or <see langword="null"/> where there is none.</param>
internal sealed class CredentialStoreSelector(FileCredentialStore file, IKeychainCredentialStore? keychain)
{
    private readonly Lock _gate = new();
    private CredentialStores? _stores;

    /// <summary>
    /// Returns the stores of this process, probing the keychain on the first call.
    /// </summary>
    /// <param name="cloudUrl">
    /// The normalized Cloud URL; the probe looks up its real entry, so "available" also means
    /// "readable for this user right now".
    /// </param>
    public CredentialStores Select(string cloudUrl)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);

        lock (_gate)
            return _stores ??= Probe(cloudUrl);
    }

    private CredentialStores Probe(string cloudUrl)
    {
        if (keychain is null)
            return new CredentialStores(file, [file], fallbackReason: null);

        KeychainProbe probe = keychain.Probe(cloudUrl);
        if (probe.Available)
            return new CredentialStores(keychain, [keychain, file], fallbackReason: null);

        // Still offered for deletion: a sign-in made in a desktop session must not survive a
        // logout over SSH unnoticed (§7.4).
        return new CredentialStores(file, [file], probe.Reason, probe.MayHoldSignIns ? [keychain] : []);
    }
}
