/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Decides, once per process, which credential stores this machine offers (cli-auth-cli-spec §7.4).
/// </summary>
/// <remarks>
/// Only the file store exists so far; the OS keychains and their availability probes come in front
/// of it in the read order when they are added.
/// </remarks>
internal sealed class CredentialStoreSelector(FileCredentialStore file)
{
    private readonly CredentialStores _stores = new(file, [file], fallbackReason: null);

    /// <summary>
    /// Returns the stores of this process.
    /// </summary>
    public CredentialStores Select() => _stores;
}
